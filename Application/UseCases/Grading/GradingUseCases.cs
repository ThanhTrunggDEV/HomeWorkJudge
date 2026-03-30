using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Application.Common;
using Application.DomainEvents;
using Domain.Exception;
using Domain.Ports;
using Domain.ValueObject;
using Ports.DTO.AI;
using Ports.DTO.Common;
using Ports.DTO.Rubric;
using Ports.DTO.Submission;
using Ports.InBoundPorts.Grading;
using Ports.OutBoundPorts.Judging;
using Ports.OutBoundPorts.Queue;
using Ports.OutBoundPorts.RubricGrading;

namespace Application.UseCases.Grading;

public sealed class GradeSubmissionByTestCaseUseCase : IGradeSubmissionByTestCaseUseCase
{
    private readonly ISubmissionRepository _submissionRepository;
    private readonly IAssignmentRepository _assignmentRepository;
    private readonly ITestCaseJudgePort _testCaseJudgePort;
    private readonly IUnitOfWork _unitOfWork;
    private readonly DomainEventDispatcher _domainEventDispatcher;

    public GradeSubmissionByTestCaseUseCase(
        ISubmissionRepository submissionRepository,
        IAssignmentRepository assignmentRepository,
        ITestCaseJudgePort testCaseJudgePort,
        IUnitOfWork unitOfWork,
        DomainEventDispatcher domainEventDispatcher)
    {
        _submissionRepository = submissionRepository;
        _assignmentRepository = assignmentRepository;
        _testCaseJudgePort = testCaseJudgePort;
        _unitOfWork = unitOfWork;
        _domainEventDispatcher = domainEventDispatcher;
    }

    public async Task HandleAsync(Guid submissionId, CancellationToken cancellationToken = default)
    {
        var submission = await _submissionRepository.GetByIdAsync(new SubmissionId(submissionId))
            ?? throw new DomainException("Submission not found.");

        var assignment = await _assignmentRepository.GetByIdWithTestCasesAsync(submission.AssignmentId)
            ?? throw new DomainException("Assignment not found.");

        if (submission.Status == SubmissionStatus.Pending)
        {
            submission.ChangeStatusToExecuting();
        }
        else if (submission.Status != SubmissionStatus.Executing)
        {
            throw new DomainException($"Submission status {submission.Status} is not valid for grading.");
        }

        var request = new TestCaseJudgeRequestDto(
            submission.Id.Value,
            assignment.Id.Value,
            submission.Language,
            submission.SourceCode,
            assignment.TestCases
                .Select(testCase => new TestCaseJudgeItemDto(testCase.Id.Value, testCase.InputData, testCase.ExpectedOutput))
                .ToList(),
            assignment.TimeLimitMs,
            assignment.MemoryLimitKb);

        var executionResults = await _testCaseJudgePort.JudgeAsync(request, cancellationToken);
        var domainResults = executionResults
            .Select(result => new TestCaseResult(
                new TestCaseId(result.TestCaseId),
                result.ActualOutput,
                result.ExecutionTimeMs,
                result.MemoryUsedKb,
                ToDomainTestCaseStatus(result.Status)))
            .ToList();

        submission.AttachTestCaseResults(domainResults);

        var events = DomainEventUtilities.SnapshotEvents(submission);

        await _submissionRepository.UpdateAsync(submission);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _domainEventDispatcher.DispatchAsync(events, cancellationToken);
        DomainEventUtilities.ClearEvents(submission);
    }

    private static TestCaseStatus ToDomainTestCaseStatus(TestCaseExecutionStatusDto status)
        => status switch
        {
            TestCaseExecutionStatusDto.Passed => TestCaseStatus.Passed,
            TestCaseExecutionStatusDto.Failed => TestCaseStatus.Failed,
            TestCaseExecutionStatusDto.TimeOut => TestCaseStatus.TimeOut,
            TestCaseExecutionStatusDto.RuntimeError => TestCaseStatus.RuntimeError,
            _ => TestCaseStatus.RuntimeError
        };
}

public sealed class GradeSubmissionByRubricUseCase : IGradeSubmissionByRubricUseCase
{
    private readonly ISubmissionRepository _submissionRepository;
    private readonly IAssignmentRepository _assignmentRepository;
    private readonly IRubricGradingPort _rubricGradingPort;
    private readonly IUnitOfWork _unitOfWork;
    private readonly DomainEventDispatcher _domainEventDispatcher;

    public GradeSubmissionByRubricUseCase(
        ISubmissionRepository submissionRepository,
        IAssignmentRepository assignmentRepository,
        IRubricGradingPort rubricGradingPort,
        IUnitOfWork unitOfWork,
        DomainEventDispatcher domainEventDispatcher)
    {
        _submissionRepository = submissionRepository;
        _assignmentRepository = assignmentRepository;
        _rubricGradingPort = rubricGradingPort;
        _unitOfWork = unitOfWork;
        _domainEventDispatcher = domainEventDispatcher;
    }

    public async Task HandleAsync(Guid submissionId, CancellationToken cancellationToken = default)
    {
        var submission = await _submissionRepository.GetByIdAsync(new SubmissionId(submissionId))
            ?? throw new DomainException("Submission not found.");

        var assignment = await _assignmentRepository.GetByIdWithTestCasesAsync(submission.AssignmentId)
            ?? throw new DomainException("Assignment not found.");

        if (assignment.Rubric is null)
        {
            throw new DomainException("Rubric not found for assignment.");
        }

        if (submission.Status == SubmissionStatus.Pending)
        {
            submission.ChangeStatusToExecuting();
        }
        else if (submission.Status != SubmissionStatus.Executing)
        {
            throw new DomainException($"Submission status {submission.Status} is not valid for rubric grading.");
        }

        var criteria = DeserializeCriteria(assignment.Rubric.CriteriaListJson);
        var scores = await _rubricGradingPort.GradeByRubricAsync(submission.SourceCode, criteria, cancellationToken);

        var rubricResults = scores
            .Select(score => new RubricResult(score.CriteriaName, score.GivenScore, score.CommentReason))
            .ToList();

        submission.AttachRubricResults(rubricResults);

        var events = DomainEventUtilities.SnapshotEvents(submission);

        await _submissionRepository.UpdateAsync(submission);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _domainEventDispatcher.DispatchAsync(events, cancellationToken);
        DomainEventUtilities.ClearEvents(submission);
    }

    private static IReadOnlyList<RubricCriteriaDto> DeserializeCriteria(string criteriaListJson)
    {
        if (string.IsNullOrWhiteSpace(criteriaListJson))
        {
            return Array.Empty<RubricCriteriaDto>();
        }

        return JsonSerializer.Deserialize<IReadOnlyList<RubricCriteriaDto>>(criteriaListJson)
            ?? Array.Empty<RubricCriteriaDto>();
    }
}

public sealed class OverrideSubmissionScoreUseCase : IOverrideSubmissionScoreUseCase
{
    private readonly ISubmissionRepository _submissionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public OverrideSubmissionScoreUseCase(ISubmissionRepository submissionRepository, IUnitOfWork unitOfWork)
    {
        _submissionRepository = submissionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task HandleAsync(Guid submissionId, double newTotalScore, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("Reason is required when overriding score.");
        }

        var submission = await _submissionRepository.GetByIdAsync(new SubmissionId(submissionId))
            ?? throw new DomainException("Submission not found.");

        submission.OverrideTotalScore(newTotalScore);
        await _submissionRepository.UpdateAsync(submission);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class ReviewAiRubricResultUseCase : IReviewAiRubricResultUseCase
{
    private readonly ISubmissionRepository _submissionRepository;
    private readonly IBackgroundJobQueuePort _backgroundJobQueuePort;

    public ReviewAiRubricResultUseCase(
        ISubmissionRepository submissionRepository,
        IBackgroundJobQueuePort backgroundJobQueuePort)
    {
        _submissionRepository = submissionRepository;
        _backgroundJobQueuePort = backgroundJobQueuePort;
    }

    public async Task HandleAsync(RubricReviewDecisionDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var submission = await _submissionRepository.GetByIdAsync(new SubmissionId(request.SubmissionId))
            ?? throw new DomainException("Submission not found.");

        if (!request.IsApproved && string.IsNullOrWhiteSpace(request.TeacherComment))
        {
            throw new DomainException("Teacher comment is required when AI rubric result is not approved.");
        }

        if (request.IsApproved)
        {
            return;
        }

        var payload = JsonSerializer.Serialize(new
        {
            SubmissionId = submission.Id.Value,
            TeacherComment = request.TeacherComment,
            Action = "manual-rubric-regrade"
        });

        var envelope = new JobEnvelopeDto(
            JobName: "submission.rubric.review",
            Payload: payload,
            CorrelationId: Guid.NewGuid().ToString("N"),
            CreatedAt: DateTimeOffset.UtcNow,
            RetryCount: 0);

        await _backgroundJobQueuePort.EnqueueAsync(envelope, cancellationToken);
    }
}

public sealed class OverrideRubricCriteriaScoreUseCase : IOverrideRubricCriteriaScoreUseCase
{
    private readonly ISubmissionRepository _submissionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public OverrideRubricCriteriaScoreUseCase(ISubmissionRepository submissionRepository, IUnitOfWork unitOfWork)
    {
        _submissionRepository = submissionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task HandleAsync(OverrideRubricCriteriaScoreRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new DomainException("Reason is required when overriding rubric criteria score.");
        }

        var submission = await _submissionRepository.GetByIdAsync(new SubmissionId(request.SubmissionId))
            ?? throw new DomainException("Submission not found.");

        submission.OverrideRubricCriteriaScore(request.CriteriaName, request.NewScore);

        await _submissionRepository.UpdateAsync(submission);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class ExplainRubricDeductionUseCase : IExplainRubricDeductionUseCase
{
    private readonly ISubmissionRepository _submissionRepository;

    public ExplainRubricDeductionUseCase(ISubmissionRepository submissionRepository)
    {
        _submissionRepository = submissionRepository;
    }

    public async Task<ExplainRubricDeductionResponseDto> HandleAsync(ExplainRubricDeductionRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var submission = await _submissionRepository.GetByIdAsync(new SubmissionId(request.SubmissionId))
            ?? throw new DomainException("Submission not found.");

        var rubricResult = submission.RubricResults
            .FirstOrDefault(x => string.Equals(x.CriteriaName, request.CriteriaName, StringComparison.OrdinalIgnoreCase));

        var explanation = rubricResult is null
            ? $"No rubric deduction information found for criteria '{request.CriteriaName}'."
            : $"Criteria '{rubricResult.CriteriaName}' received {rubricResult.GivenScore:0.##} points. Reviewer note: {rubricResult.CommentReason}. Student question: {request.StudentQuestion}";

        return new ExplainRubricDeductionResponseDto(request.SubmissionId, request.CriteriaName, explanation);
    }
}
