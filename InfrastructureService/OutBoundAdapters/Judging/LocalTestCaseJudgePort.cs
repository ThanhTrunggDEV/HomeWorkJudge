using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ports.DTO.Common;
using Ports.DTO.Submission;
using Ports.OutBoundPorts.Judging;

namespace InfrastructureService.OutBoundAdapters.Judging;

public sealed class LocalTestCaseJudgePort : ITestCaseJudgePort
{
    private readonly ICodeCompilationPort _compilationPort;
    private readonly ICodeExecutionPort _executionPort;

    public LocalTestCaseJudgePort(
        ICodeCompilationPort compilationPort,
        ICodeExecutionPort executionPort)
    {
        _compilationPort = compilationPort;
        _executionPort = executionPort;
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
}