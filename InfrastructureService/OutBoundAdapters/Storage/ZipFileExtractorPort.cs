using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using InfrastructureService.Common.Errors;
using Ports.DTO.Submission;
using Ports.OutBoundPorts.Storage;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace InfrastructureService.OutBoundAdapters.Storage;

/// <summary>
/// Implements IFileExtractorPort: giải nén file zip/rar/7z,
/// lọc chỉ lấy các file source code theo whitelist extension.
/// </summary>
public sealed class ZipFileExtractorPort : IFileExtractorPort
{
    // Whitelist các extension source code hợp lệ
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        // ── C / C++ ───────────────────────────────────────────────────────────
        ".c", ".cpp", ".h", ".hpp",

        // ── Java ──────────────────────────────────────────────────────────────
        ".java",

        // ── Python ────────────────────────────────────────────────────────────
        ".py", ".pyx",

        // ── C# / .NET ─────────────────────────────────────────────────────────
        ".cs",
        // Project / solution files (cần để dotnet build hoạt động)
        ".csproj", ".sln", ".slnx",
        // WPF / MAUI / UWP markup
        ".xaml",
        // ASP.NET Razor views & Blazor components
        ".cshtml", ".razor",
        // Resource files (WPF / Windows Forms cần)
        ".resx",
        // Config & data files
        ".json", ".xml", ".config",

        // ── Web / JS / TS ─────────────────────────────────────────────────────
        ".js", ".ts", ".html", ".css",

        // ── Go ────────────────────────────────────────────────────────────────
        ".go",

        // ── Rust ──────────────────────────────────────────────────────────────
        ".rs",

        // ── Ruby ──────────────────────────────────────────────────────────────
        ".rb",

        // ── PHP ───────────────────────────────────────────────────────────────
        ".php",

        // ── Swift / Kotlin ────────────────────────────────────────────────────
        ".swift", ".kt",

        // ── Docs ──────────────────────────────────────────────────────────────
        ".txt", ".md"
    };

    // Thư mục cần bỏ qua
    private static readonly HashSet<string> IgnoredDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", "node_modules", ".git", "__pycache__", ".idea", ".vs"
    };

    private const long MaxFileSizeBytes = 1024 * 1024; // 1 MB per file

    public async Task<IReadOnlyList<SourceFileDto>> ExtractAsync(string archivePath, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (!File.Exists(archivePath))
            throw new InfrastructureException("FILE_NOT_FOUND", $"File không tồn tại: {archivePath}");

        var ext = Path.GetExtension(archivePath).ToLowerInvariant();
        return ext switch
        {
            ".zip" => await ExtractZipAsync(archivePath, ct),
            ".rar" or ".7z" or ".tar" or ".gz" => await ExtractWithSharpCompressAsync(archivePath, ct),
            _ => throw new InfrastructureException("UNSUPPORTED_FORMAT", $"Định dạng '{ext}' không được hỗ trợ.")
        };
    }

    // ── ZIP (built-in) ───────────────────────────────────────────────────────

    private static async Task<IReadOnlyList<SourceFileDto>> ExtractZipAsync(string path, CancellationToken ct)
    {
        var result = new List<SourceFileDto>();

        using var archive = ZipFile.OpenRead(path);
        foreach (var entry in archive.Entries)
        {
            ct.ThrowIfCancellationRequested();

            if (!IsValidEntry(entry.FullName, entry.Length)) continue;

            await using var stream = entry.Open();
            using var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync(ct);

            result.Add(new SourceFileDto(NormalizePath(entry.FullName), content));
        }

        return result;
    }

    // ── RAR / 7z / tar (SharpCompress) ───────────────────────────────────────

    private static async Task<IReadOnlyList<SourceFileDto>> ExtractWithSharpCompressAsync(string path, CancellationToken ct)
    {
        var result = new List<SourceFileDto>();

        using var archive = ArchiveFactory.Open(path);
        foreach (var entry in archive.Entries)
        {
            ct.ThrowIfCancellationRequested();

            if (entry.IsDirectory) continue;
            if (!IsValidEntry(entry.Key ?? "", entry.Size)) continue;

            await using var entryStream = entry.OpenEntryStream();
            using var reader = new StreamReader(entryStream);
            var content = await reader.ReadToEndAsync(ct);

            result.Add(new SourceFileDto(NormalizePath(entry.Key!), content));
        }

        return result;
    }

    // ── Validation helpers ───────────────────────────────────────────────────

    private static bool IsValidEntry(string fullName, long size)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return false;
        if (size > MaxFileSizeBytes) return false;

        var normalized = fullName.Replace('\\', '/');
        var parts = normalized.Split('/');

        // Bỏ qua các thư mục trong blacklist
        if (parts.Any(p => IgnoredDirs.Contains(p))) return false;

        var ext = Path.GetExtension(normalized);
        return AllowedExtensions.Contains(ext);
    }

    private static string NormalizePath(string p) => p.Replace('\\', '/').TrimStart('/');
}
