using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using InfrastructureService.Common.Errors;
using InfrastructureService.Common.Resilience;
using InfrastructureService.Configuration.Options;
using Microsoft.Extensions.Options;
using Ports.DTO.Submission;
using Ports.OutBoundPorts.Judging;

namespace InfrastructureService.OutBoundAdapters.Judging;

public sealed class LocalCodeExecutionPort : ICodeExecutionPort
{
    private readonly IOperationExecutor _operationExecutor;
    private readonly JudgingOptions _options;

    public LocalCodeExecutionPort(
        IOperationExecutor operationExecutor,
        IOptions<JudgingOptions> options)
    {
        _operationExecutor = operationExecutor;
        _options = options.Value;
    }

    public Task<CodeExecutionResultDto> ExecuteAsync(
        CodeExecutionRequestDto request,
        CancellationToken cancellationToken = default)
        => _operationExecutor.ExecuteAsync(
            "judging.execute",
            ct => ExecuteInternalAsync(request, ct),
            cancellationToken);

    private async Task<CodeExecutionResultDto> ExecuteInternalAsync(
        CodeExecutionRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.ArtifactPath))
        {
            throw new InfrastructureException("JUDGING_EMPTY_ARTIFACT", "Artifact path cannot be empty.");
        }

        if (!File.Exists(request.ArtifactPath))
        {
            throw new InfrastructureException("JUDGING_ARTIFACT_NOT_FOUND", $"Artifact '{request.ArtifactPath}' was not found.");
        }

        var timeoutMs = request.TimeLimitMs > 0
            ? request.TimeLimitMs
            : Math.Max(1000, _options.ExecuteTimeoutSeconds * 1000L);

        var timeout = TimeSpan.FromMilliseconds(timeoutMs);
        var workingDirectory = Path.GetDirectoryName(request.ArtifactPath) ?? Directory.GetCurrentDirectory();

        var execution = BuildExecution(request.ArtifactPath);
        var processResult = await ProcessExecutionHelper.RunAsync(
            execution.FileName,
            execution.Arguments,
            workingDirectory,
            request.Input,
            timeout,
            cancellationToken);

        if (processResult.TimedOut)
        {
            return new CodeExecutionResultDto(
                string.Empty,
                processResult.DurationMs,
                processResult.PeakMemoryKb,
                true,
                false,
                "Execution timed out.");
        }

        var runtimeError = processResult.ExitCode != 0;
        var runtimeMessage = runtimeError
            ? BuildRuntimeMessage(processResult.StandardError, processResult.StandardOutput)
            : null;

        return new CodeExecutionResultDto(
            NormalizeOutput(processResult.StandardOutput),
            processResult.DurationMs,
            processResult.PeakMemoryKb,
            false,
            runtimeError,
            runtimeMessage);
    }

    private static (string FileName, string Arguments) BuildExecution(string artifactPath)
    {
        if (artifactPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            return ("dotnet", Quote(artifactPath));
        }

        if (artifactPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return (artifactPath, string.Empty);
        }

        throw new InfrastructureException(
            "JUDGING_UNSUPPORTED_ARTIFACT",
            $"Unsupported artifact type for '{artifactPath}'. Supported extensions: .dll, .exe.");
    }

    private static string Quote(string value)
        => value.Contains(' ') ? $"\"{value}\"" : value;

    private static string BuildRuntimeMessage(string standardError, string standardOutput)
    {
        if (!string.IsNullOrWhiteSpace(standardError))
        {
            return standardError.Trim();
        }

        if (!string.IsNullOrWhiteSpace(standardOutput))
        {
            return standardOutput.Trim();
        }

        return "Runtime process exited with non-zero code.";
    }

    private static string NormalizeOutput(string output)
        => output.Replace("\r\n", "\n").TrimEnd('\n', '\r', ' ', '\t');
}