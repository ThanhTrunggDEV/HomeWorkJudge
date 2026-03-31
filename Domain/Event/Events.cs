using System;
using Domain.ValueObject;

namespace Domain.Event;

public interface IDomainEvent
{
    DateTimeOffset OccurredOn { get; }
}

// ── Submission import ────────────────────────────────────────────────────────
public sealed record SubmissionsImportedEvent(
    GradingSessionId SessionId,
    int Count,
    DateTimeOffset OccurredOn
) : IDomainEvent;

// ── AI grading ───────────────────────────────────────────────────────────────
public sealed record SubmissionGradingStartedEvent(
    SubmissionId SubmissionId,
    DateTimeOffset OccurredOn
) : IDomainEvent;

// ── C# Build ─────────────────────────────────────────────────────────────────
public sealed record SubmissionBuildFailedEvent(
    SubmissionId SubmissionId,
    string BuildLog,
    DateTimeOffset OccurredOn
) : IDomainEvent;

public sealed record SubmissionAIGradedEvent(
    SubmissionId SubmissionId,
    double TotalScore,
    DateTimeOffset OccurredOn
) : IDomainEvent;

public sealed record SubmissionAIFailedEvent(
    SubmissionId SubmissionId,
    string ErrorMessage,
    DateTimeOffset OccurredOn
) : IDomainEvent;

// ── Review ───────────────────────────────────────────────────────────────────
public sealed record SubmissionReviewedEvent(
    SubmissionId SubmissionId,
    double FinalScore,
    DateTimeOffset OccurredOn
) : IDomainEvent;
