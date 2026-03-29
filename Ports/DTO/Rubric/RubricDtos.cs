namespace Ports.DTO.Rubric;

public sealed record RubricCriteriaDto(
    string Name,
    string Description,
    double Weight);

public sealed record RubricScoreDto(
    string CriteriaName,
    double GivenScore,
    string CommentReason);

public sealed record RubricReviewDecisionDto(
    System.Guid SubmissionId,
    bool IsApproved,
    string? TeacherComment);

public sealed record OverrideRubricCriteriaScoreRequestDto(
    System.Guid SubmissionId,
    string CriteriaName,
    double NewScore,
    string Reason);
