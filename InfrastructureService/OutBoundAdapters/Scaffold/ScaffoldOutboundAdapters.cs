using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using InfrastructureService.Common.Errors;
using Microsoft.Extensions.Logging;
using Ports.DTO.AI;
using Ports.DTO.Common;
using Ports.DTO.Report;
using Ports.DTO.Rubric;
using Ports.DTO.Submission;
using Ports.OutBoundPorts.AI;
using Ports.OutBoundPorts.Judging;
using Ports.OutBoundPorts.Plagiarism;
using Ports.OutBoundPorts.Queue;
using Ports.OutBoundPorts.Report;
using Ports.OutBoundPorts.RubricGrading;
using Ports.OutBoundPorts.Storage;

namespace InfrastructureService.OutBoundAdapters.Scaffold;

internal sealed class ScaffoldBackgroundJobQueueAdapter : IBackgroundJobQueuePort
{
    private readonly ILogger<ScaffoldBackgroundJobQueueAdapter> _logger;

    public ScaffoldBackgroundJobQueueAdapter(ILogger<ScaffoldBackgroundJobQueueAdapter> logger)
    {
        _logger = logger;
    }

    public Task EnqueueAsync(JobEnvelopeDto envelope, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Scaffold queue adapter accepted job {JobName} with correlation {CorrelationId}.", envelope.JobName, envelope.CorrelationId);
        return Task.CompletedTask;
    }
}

internal sealed class ScaffoldFileStoragePort : IFileStoragePort
{
    public Task<string> UploadAsync(string fileName, Stream content, string contentType, CancellationToken cancellationToken = default)
        => throw ScaffoldErrors.NotImplemented("FileStorage.UploadAsync");

    public Task<Stream> DownloadAsync(string fileKey, CancellationToken cancellationToken = default)
        => throw ScaffoldErrors.NotImplemented("FileStorage.DownloadAsync");
}

internal sealed class ScaffoldReportExportPort : IReportExportPort
{
    public Task<ExportScoreReportResponseDto> ExportScoreboardAsync(IReadOnlyList<ScoreboardItemDto> items, string format, CancellationToken cancellationToken = default)
    => throw ScaffoldErrors.NotImplemented("Report.ExportScoreboardAsync");
}

internal sealed class ScaffoldCodeCompilationPort : ICodeCompilationPort
{
    public Task<CodeCompilationResultDto> CompileAsync(string sourceCode, string language, CancellationToken cancellationToken = default)
    => throw ScaffoldErrors.NotImplemented("Judging.CompileAsync");
}

internal sealed class ScaffoldCodeExecutionPort : ICodeExecutionPort
{
    public Task<CodeExecutionResultDto> ExecuteAsync(CodeExecutionRequestDto request, CancellationToken cancellationToken = default)
    => throw ScaffoldErrors.NotImplemented("Judging.ExecuteAsync");
}

internal sealed class ScaffoldTestCaseJudgePort : ITestCaseJudgePort
{
    public Task<IReadOnlyList<TestCaseExecutionResultDto>> JudgeAsync(TestCaseJudgeRequestDto request, CancellationToken cancellationToken = default)
    => throw ScaffoldErrors.NotImplemented("Judging.JudgeAsync");
}

internal sealed class ScaffoldAiGradingPort : IAiGradingPort
{
    public Task<AiGradeSubmissionResponseDto> GradeSubmissionAsync(AiGradeSubmissionRequestDto request, CancellationToken cancellationToken = default)
    => throw ScaffoldErrors.NotImplemented("AI.GradeSubmissionAsync");
}

internal sealed class ScaffoldRubricGradingPort : IRubricGradingPort
{
    public Task<IReadOnlyList<RubricScoreDto>> GradeByRubricAsync(string sourceCode, IReadOnlyList<RubricCriteriaDto> criteria, CancellationToken cancellationToken = default)
    => throw ScaffoldErrors.NotImplemented("Rubric.GradeByRubricAsync");
}

internal sealed class ScaffoldPlagiarismDetectionPort : IPlagiarismDetectionPort
{
    public Task<double> CalculateSimilarityAsync(string leftSourceCode, string rightSourceCode, CancellationToken cancellationToken = default)
    => throw ScaffoldErrors.NotImplemented("Plagiarism.CalculateSimilarityAsync");
}

internal static class ScaffoldErrors
{
    public static InfrastructureException NotImplemented(string capability)
        => new("INFRA_SCAFFOLD_ONLY", $"{capability} is scaffolded in Phase 1 and has not been implemented yet.");
}
