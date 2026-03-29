using System;
using Domain.ValueObject;

namespace Domain.Event;

public interface IDomainEvent
{
	DateTimeOffset OccurredOn { get; }
}

public sealed record AssignmentPublishedEvent(
	AssignmentId AssignmentId,
	DateTime PublishedAt,
	DateTimeOffset OccurredOn
) : IDomainEvent;

public sealed record SubmissionCreatedEvent(
	SubmissionId SubmissionId,
	AssignmentId AssignmentId,
	DateTimeOffset OccurredOn
) : IDomainEvent;

public sealed record SubmissionGradingCompletedEvent(
	SubmissionId SubmissionId,
	double TotalScore,
	DateTimeOffset OccurredOn
) : IDomainEvent;
