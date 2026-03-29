using System;
using Domain.ValueObject;

namespace Domain.Event;

public interface IDomainEvent { }

public record AssignmentPublishedEvent(AssignmentId AssignmentId, DateTime PublishedAt) : IDomainEvent;

public record SubmissionCreatedEvent(SubmissionId SubmissionId, AssignmentId AssignmentId) : IDomainEvent;

public record SubmissionGradingCompletedEvent(SubmissionId SubmissionId, double TotalScore) : IDomainEvent;
