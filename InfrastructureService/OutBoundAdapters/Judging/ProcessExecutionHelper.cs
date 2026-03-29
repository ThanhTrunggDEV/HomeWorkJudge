using System;
using System.Diagnostics;
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
                currentMemoryKb = process.WorkingSet64 / 1024;
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
}