using System;
using System.Threading;
using System.Threading.Tasks;
using Ports.DTO.AI;
using Ports.DTO.Rubric;

namespace Ports.InBoundPorts.Grading;

public interface IGradeSubmissionByTestCaseUseCase
{
    Task HandleAsync(Guid submissionId, CancellationToken cancellationToken = default);
}

public interface IGradeSubmissionByRubricUseCase
{
    Task HandleAsync(Guid submissionId, CancellationToken cancellationToken = default);
}

public interface IOverrideSubmissionScoreUseCase
{
    Task HandleAsync(
        Guid submissionId,
        double newTotalScore,
        string reason,
        CancellationToken cancellationToken = default);
}

public interface IReviewAiRubricResultUseCase
{
    Task HandleAsync(
        RubricReviewDecisionDto request,
        CancellationToken cancellationToken = default);
}

public interface IOverrideRubricCriteriaScoreUseCase
{
    Task HandleAsync(
        OverrideRubricCriteriaScoreRequestDto request,
        CancellationToken cancellationToken = default);
}

public interface IExplainRubricDeductionUseCase
{
    Task<ExplainRubricDeductionResponseDto> HandleAsync(
        ExplainRubricDeductionRequestDto request,
        CancellationToken cancellationToken = default);
}
