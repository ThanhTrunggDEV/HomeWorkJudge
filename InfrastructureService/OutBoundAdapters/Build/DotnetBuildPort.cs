using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ports.DTO.Submission;
using Ports.OutBoundPorts.Build;

namespace InfrastructureService.OutBoundAdapters.Build;

/// <summary>
/// Implements ICSharpBuildPort: ghi source files ra thư mục temp, chạy "dotnet build",
/// trả về kết quả và full build log.
/// </summary>
public sealed class DotnetBuildPort : ICSharpBuildPort
{
    private const int BuildTimeoutSeconds = 120;
    private const int MaxLogLines = 150;    // Giữ tối đa 150 dòng cuối để tránh freeze UI

    public async Task<BuildResult> BuildAsync(
        IReadOnlyList<SourceFileDto> sourceFiles,
        string submissionId,
        CancellationToken ct = default)
    {
        var tempDir = Path.Combine(
            Path.GetTempPath(), "hwjudge", SanitizeFolderName(submissionId));

        try
        {
            // 1. Ghi source files vào thư mục temp
            WriteSourceFiles(tempDir, sourceFiles);

            // 2. Tìm file entry-point để build (.sln, .slnx, hoặc .csproj)
            var target = FindBuildTarget(tempDir);
            if (target is null)
            {
                return new BuildResult(false,
                    "Không tìm thấy file .sln, .slnx hoặc .csproj trong bài nộp.");
            }

            // 3. Chạy dotnet build
            var log = await RunDotnetBuildAsync(target, ct);
            bool success = log.ExitCode == 0;

            return new BuildResult(success, log.Output);
        }
        finally
        {
            // 4. Dọn dẹp thư mục temp
            TryDeleteDirectory(tempDir);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static void WriteSourceFiles(string targetDir, IReadOnlyList<SourceFileDto> files)
    {
        // Xoá và tạo lại thư mục sạch
        if (Directory.Exists(targetDir))
            Directory.Delete(targetDir, recursive: true);
        Directory.CreateDirectory(targetDir);

        foreach (var file in files)
        {
            // Normalize path separator, strip leading slash
            var relativePath = file.FileName.Replace('\\', '/').TrimStart('/');
            var fullPath = Path.Combine(targetDir, relativePath.Replace('/', Path.DirectorySeparatorChar));

            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(fullPath, file.Content, Encoding.UTF8);
        }
    }

    /// <summary>
    /// Ưu tiên: .sln hoặc .slnx → .csproj.
    /// Nếu có nhiều .csproj, chọn cái đầu tiên tìm được.
    /// </summary>
    private static string? FindBuildTarget(string dir)
    {
        // Tìm solution file (bao gồm cả thư mục con 1 cấp)
        var slns = Directory.GetFiles(dir, "*.sln", SearchOption.AllDirectories)
                  .Concat(Directory.GetFiles(dir, "*.slnx", SearchOption.AllDirectories))
                  .ToList();

        if (slns.Count > 0)
            return slns[0];

        var csproj = Directory.GetFiles(dir, "*.csproj", SearchOption.AllDirectories);
        return csproj.Length > 0 ? csproj[0] : null;
    }

    private static async Task<(int ExitCode, string Output)> RunDotnetBuildAsync(
        string target, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"build \"{target}\" --nologo -v q",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(target) ?? "."
        };

        using var process = new Process { StartInfo = psi };
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(BuildTimeoutSeconds));

        process.Start();

        // Đọc stdout và stderr song song — không dùng event-based (race condition với WaitForExitAsync)
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
        var stderrTask = process.StandardError.ReadToEndAsync(cts.Token);

        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
            var partialOut = string.Empty;
            try { partialOut = await stdoutTask; } catch { /* ignore */ }
            return (-1, partialOut + $"\n[Build bị hủy sau {BuildTimeoutSeconds} giây timeout]");
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        // Gộp stdout + stderr
        var combined = (stdout + stderr).Trim();
        if (string.IsNullOrEmpty(combined))
            combined = process.ExitCode == 0
                ? "Build succeeded."
                : $"Build failed (exit code {process.ExitCode}). Không có output từ dotnet build.";

        // Truncate để tránh WPF TextBox render text quá lớn → freeze UI
        var finalLog = TruncateLog(combined, process.ExitCode);
        return (process.ExitCode, finalLog);
    }

    /// <summary>
    /// Giữ tối đa MaxLogLines dòng CUỐI (errors của dotnet build luôn ở cuối).
    /// Thêm header tóm tắt để GV đọc ngay kết quả.
    /// </summary>
    private static string TruncateLog(string log, int exitCode)
    {
        var lines = log.Split('\n');
        var header = exitCode == 0
            ? $"✅ Build succeeded  ({lines.Length} lines)"
            : $"❌ Build FAILED (exit {exitCode})  |  {lines.Length} lines total";

        if (lines.Length <= MaxLogLines)
            return header + "\n" + new string('─', 40) + "\n" + log;

        // Chỉ lấy MaxLogLines dòng cuối
        var tail = string.Join("\n", lines.TakeLast(MaxLogLines));
        return header + "\n"
            + $"[... {lines.Length - MaxLogLines} dòng đầu bị ẩn, hiển thị {MaxLogLines} dòng cuối ...]\n"
            + new string('─', 40) + "\n"
            + tail;
    }

    private static string SanitizeFolderName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
    }

    private static void TryDeleteDirectory(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
        catch { /* best-effort cleanup */ }
    }
}
