using System;
using System.Collections.Generic;
using Ports.DTO.Rubric;

namespace Ports.DTO.AI;

public sealed record AiGradeSubmissionRequestDto(
    Guid SubmissionId,
    string AssignmentTitle,
    string AssignmentDescription,
    string SourceCode,
    string Language,
    IReadOnlyList<RubricCriteriaDto> Criteria);

public sealed record AiRubricScoreDto(
    string CriteriaName,
    double Score,
    string Comment);

public sealed record AiFeedbackDto(
    string Summary,
    IReadOnlyList<string> Suggestions);

public sealed record AiGradeSubmissionResponseDto(
    Guid SubmissionId,
    double TotalScore,
    IReadOnlyList<AiRubricScoreDto> Scores,
    AiFeedbackDto Feedback);

public sealed record ExplainRubricDeductionRequestDto(
    Guid SubmissionId,
    string CriteriaName,
    string StudentQuestion);

public sealed record ExplainRubricDeductionResponseDto(
    Guid SubmissionId,
    string CriteriaName,
    string Explanation);
