using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace InfrastructureService.OutBoundAdapters.Judging;

internal sealed record ProcessExecutionResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool TimedOut,
    bool MemoryLimitExceeded,
    long PeakMemoryKb,
    long DurationMs);

internal static class ProcessExecutionHelper
{
    public static async Task<ProcessExecutionResult> RunAsync(
        string fileName,
        string arguments,
        string workingDirectory,
        string? standardInput,
        TimeSpan timeout,
        long memoryLimitKb,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        var stopwatch = Stopwatch.StartNew();

        if (!process.Start())
        {
            throw new InvalidOperationException($"Failed to start process {fileName} {arguments}.");
        }

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        if (!string.IsNullOrEmpty(standardInput))
        {
            await process.StandardInput.WriteAsync(standardInput);
        }

        process.StandardInput.Close();

        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var timedOut = false;
        var memoryLimitExceeded = false;
        var memoryMonitorTask = memoryLimitKb > 0
            ? MonitorMemoryLimitAsync(process, memoryLimitKb, () => memoryLimitExceeded = true, linkedCts.Token)
            : Task.CompletedTask;

        try
        {
            await process.WaitForExitAsync(linkedCts.Token);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            timedOut = true;
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Best effort kill.
            }

            await process.WaitForExitAsync(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Best effort kill.
            }

            throw;
        }

        await memoryMonitorTask;

        var standardOutput = await outputTask;
        var standardError = await errorTask;

        stopwatch.Stop();

        var peakMemoryKb = process.HasExited
            ? process.PeakWorkingSet64 / 1024
            : 0;

        var exitCode = process.HasExited
            ? process.ExitCode
            : -1;

        return new ProcessExecutionResult(
            exitCode,
            standardOutput,
            standardError,
            timedOut,
            memoryLimitExceeded,
            peakMemoryKb,
            stopwatch.ElapsedMilliseconds);
    }

    private static async Task MonitorMemoryLimitAsync(
        Process process,
        long memoryLimitKb,
        Action onExceeded,
        CancellationToken cancellationToken)
    {
        while (!process.HasExited && !cancellationToken.IsCancellationRequested)
        {
            long currentMemoryKb;
            try
            {
                currentMemoryKb = GetProcessTreeWorkingSetKb(process);
            }
            catch
            {
                break;
            }

            if (currentMemoryKb > memoryLimitKb)
            {
                onExceeded();
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch
                {
                    // Best effort kill.
                }

                break;
            }

            try
            {
                await Task.Delay(50, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private static long GetProcessTreeWorkingSetKb(Process rootProcess)
    {
        if (rootProcess.HasExited)
        {
            return 0;
        }

        var processTree = BuildProcessTree();
        var queue = new Queue<int>();
        var visited = new HashSet<int>();

        queue.Enqueue(rootProcess.Id);

        long totalWorkingSetKb = 0;
        while (queue.Count > 0)
        {
            var processId = queue.Dequeue();
            if (!visited.Add(processId))
            {
                continue;
            }

            try
            {
                using var process = Process.GetProcessById(processId);
                if (!process.HasExited)
                {
                    totalWorkingSetKb += process.WorkingSet64 / 1024;
                }
            }
            catch
            {
                // Best effort metrics; ignore process that disappeared.
            }

            if (processTree.TryGetValue(processId, out var childProcessIds))
            {
                foreach (var childProcessId in childProcessIds)
                {
                    queue.Enqueue(childProcessId);
                }
            }
        }

        return totalWorkingSetKb;
    }

    private static Dictionary<int, List<int>> BuildProcessTree()
    {
        var processTree = new Dictionary<int, List<int>>();

        foreach (var process in Process.GetProcesses())
        {
            try
            {
                var parentProcessId = TryGetParentProcessId(process.Id);
                if (!parentProcessId.HasValue || parentProcessId <= 0)
                {
                    continue;
                }

                if (!processTree.TryGetValue(parentProcessId.Value, out var childProcessIds))
                {
                    childProcessIds = [];
                    processTree[parentProcessId.Value] = childProcessIds;
                }

                childProcessIds.Add(process.Id);
            }
            catch
            {
                // Best effort metrics.
            }
            finally
            {
                process.Dispose();
            }
        }

        return processTree;
    }

    private static int? TryGetParentProcessId(int processId)
    {
        if (OperatingSystem.IsWindows())
        {
            return TryGetParentProcessIdWindows(processId);
        }

        if (OperatingSystem.IsLinux())
        {
            return TryGetParentProcessIdLinux(processId);
        }

        return null;
    }

    private static int? TryGetParentProcessIdLinux(int processId)
    {
        try
        {
            var statPath = $"/proc/{processId}/stat";
            if (!File.Exists(statPath))
            {
                return null;
            }

            var content = File.ReadAllText(statPath);
            var closeParenthesisIndex = content.LastIndexOf(')');
            if (closeParenthesisIndex < 0 || closeParenthesisIndex + 2 >= content.Length)
            {
                return null;
            }

            var remaining = content.Substring(closeParenthesisIndex + 2);
            var parts = remaining.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                return null;
            }

            return int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parentProcessId)
                ? parentProcessId
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static int? TryGetParentProcessIdWindows(int processId)
    {
        var snapshotHandle = CreateToolhelp32Snapshot(Th32csSnapprocess, 0);
        if (snapshotHandle == InvalidHandleValue)
        {
            return null;
        }

        try
        {
            var processEntry = new ProcessEntry32
            {
                DwSize = (uint)Marshal.SizeOf<ProcessEntry32>()
            };

            if (!Process32First(snapshotHandle, ref processEntry))
            {
                return null;
            }

            do
            {
                if (processEntry.Th32ProcessId == processId)
                {
                    return processEntry.Th32ParentProcessId;
                }
            }
            while (Process32Next(snapshotHandle, ref processEntry));

            return null;
        }
        finally
        {
            CloseHandle(snapshotHandle);
        }
    }

    private const uint Th32csSnapprocess = 0x00000002;
    private static readonly IntPtr InvalidHandleValue = new(-1);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct ProcessEntry32
    {
        public uint DwSize;
        public uint CntUsage;
        public int Th32ProcessId;
        public IntPtr Th32DefaultHeapId;
        public uint Th32ModuleId;
        public uint CntThreads;
        public int Th32ParentProcessId;
        public int PcPriClassBase;
        public uint DwFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string SzExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessId);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool Process32First(IntPtr hSnapshot, ref ProcessEntry32 lppe);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool Process32Next(IntPtr hSnapshot, ref ProcessEntry32 lppe);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);
}