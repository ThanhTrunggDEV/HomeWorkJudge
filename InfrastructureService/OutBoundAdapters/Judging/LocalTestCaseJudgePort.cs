using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Ports.DTO.Common;
using Ports.DTO.Submission;
using Ports.OutBoundPorts.Judging;

namespace InfrastructureService.OutBoundAdapters.Judging;

public sealed class LocalTestCaseJudgePort : ITestCaseJudgePort
{
    private static readonly string WorkspaceRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "homeworkjudge", "judging"));

    private readonly ICodeCompilationPort _compilationPort;
    private readonly ICodeExecutionPort _executionPort;
    private readonly ILogger<LocalTestCaseJudgePort> _logger;

    public LocalTestCaseJudgePort(
        ICodeCompilationPort compilationPort,
        ICodeExecutionPort executionPort,
        ILogger<LocalTestCaseJudgePort> logger)
    {
        _compilationPort = compilationPort;
        _executionPort = executionPort;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TestCaseExecutionResultDto>> JudgeAsync(
        TestCaseJudgeRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var compilationResult = await _compilationPort.CompileAsync(
            request.SourceCode,
            request.Language,
            cancellationToken);

        if (!compilationResult.Success || string.IsNullOrWhiteSpace(compilationResult.ArtifactPath))
        {
            var failedResults = new List<TestCaseExecutionResultDto>(request.TestCases.Count);
            foreach (var testCase in request.TestCases)
            {
                failedResults.Add(new TestCaseExecutionResultDto(
                    testCase.TestCaseId,
                    TestCaseExecutionStatusDto.RuntimeError,
                    compilationResult.CompilerOutput,
                    0,
                    0));
            }

            return failedResults;
        }

        var results = new List<TestCaseExecutionResultDto>(request.TestCases.Count);
        try
        {
            foreach (var testCase in request.TestCases)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var executionRequest = new CodeExecutionRequestDto(
                    compilationResult.ArtifactPath,
                    testCase.Input,
                    request.TimeLimitMs,
                    request.MemoryLimitKb);

                var execution = await _executionPort.ExecuteAsync(executionRequest, cancellationToken);
                var status = ResolveStatus(execution, testCase.ExpectedOutput);

                results.Add(new TestCaseExecutionResultDto(
                    testCase.TestCaseId,
                    status,
                    execution.ActualOutput,
                    execution.ExecutionTimeMs,
                    execution.MemoryUsedKb));
            }

            return results;
        }
        finally
        {
            TryCleanupWorkspaceFromArtifact(compilationResult.ArtifactPath);
        }
    }

    private static TestCaseExecutionStatusDto ResolveStatus(
        CodeExecutionResultDto execution,
        string expectedOutput)
    {
        if (execution.TimedOut)
        {
            return TestCaseExecutionStatusDto.TimeOut;
        }

        if (execution.RuntimeError)
        {
            return TestCaseExecutionStatusDto.RuntimeError;
        }

        var normalizedExpected = NormalizeOutput(expectedOutput);
        var normalizedActual = NormalizeOutput(execution.ActualOutput);

        return string.Equals(normalizedExpected, normalizedActual, StringComparison.Ordinal)
            ? TestCaseExecutionStatusDto.Passed
            : TestCaseExecutionStatusDto.Failed;
    }

    private static string NormalizeOutput(string output)
        => (output ?? string.Empty).Replace("\r\n", "\n").TrimEnd('\n', '\r', ' ', '\t');

    private void TryCleanupWorkspaceFromArtifact(string artifactPath)
    {
        if (string.IsNullOrWhiteSpace(artifactPath))
        {
            return;
        }

        var workspacePath = ResolveWorkspacePath(artifactPath);
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            return;
        }

        var normalizedWorkspacePath = Path.GetFullPath(workspacePath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (!IsPathUnderRoot(normalizedWorkspacePath, WorkspaceRoot))
        {
            _logger.LogWarning("Skip workspace cleanup because path {WorkspacePath} is outside allowed root.", normalizedWorkspacePath);
            return;
        }

        try
        {
            if (Directory.Exists(normalizedWorkspacePath))
            {
                Directory.Delete(normalizedWorkspacePath, recursive: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete judging workspace {WorkspacePath}.", normalizedWorkspacePath);
        }
    }

    private static string? ResolveWorkspacePath(string artifactPath)
    {
        var artifactFullPath = Path.GetFullPath(artifactPath);
        var artifactDirectory = Path.GetDirectoryName(artifactFullPath);
        if (string.IsNullOrWhiteSpace(artifactDirectory))
        {
            return null;
        }

        var netDirectory = new DirectoryInfo(artifactDirectory);
        var releaseDirectory = netDirectory.Parent;
        var binDirectory = releaseDirectory?.Parent;
        var workspaceDirectory = binDirectory?.Parent;

        if (workspaceDirectory is null)
        {
            return null;
        }

        if (!string.Equals(binDirectory?.Name, "bin", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!Guid.TryParseExact(workspaceDirectory.Name, "N", out _))
        {
            return null;
        }

        return workspaceDirectory.FullName;
    }

    private static bool IsPathUnderRoot(string candidatePath, string rootPath)
    {
        var normalizedRootPath = Path.GetFullPath(rootPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedCandidatePath = Path.GetFullPath(candidatePath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        var rootWithSeparator = normalizedRootPath + Path.DirectorySeparatorChar;

        return normalizedCandidatePath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
    }
}