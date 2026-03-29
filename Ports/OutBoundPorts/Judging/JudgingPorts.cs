using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ports.DTO.Submission;

namespace Ports.OutBoundPorts.Judging;

public interface ICodeCompilationPort
{
    Task<CodeCompilationResultDto> CompileAsync(
        string sourceCode,
        string language,
        CancellationToken cancellationToken = default);
}

public interface ICodeExecutionPort
{
    Task<CodeExecutionResultDto> ExecuteAsync(
        CodeExecutionRequestDto request,
        CancellationToken cancellationToken = default);
}

public interface ITestCaseJudgePort
{
    Task<IReadOnlyList<TestCaseExecutionResultDto>> JudgeAsync(
        TestCaseJudgeRequestDto request,
        CancellationToken cancellationToken = default);
}
