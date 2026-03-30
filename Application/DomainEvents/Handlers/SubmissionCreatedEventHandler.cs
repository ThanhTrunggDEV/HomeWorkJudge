using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Domain.Event;
using Ports.DTO.Common;
using Ports.OutBoundPorts.Queue;

namespace Application.DomainEvents.Handlers;

public sealed class SubmissionCreatedEventHandler : IDomainEventHandler<SubmissionCreatedEvent>
{
    private readonly IBackgroundJobQueuePort _backgroundJobQueuePort;

    public SubmissionCreatedEventHandler(IBackgroundJobQueuePort backgroundJobQueuePort)
    {
        _backgroundJobQueuePort = backgroundJobQueuePort;
    }

    public Task HandleAsync(SubmissionCreatedEvent domainEvent, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new
        {
            SubmissionId = domainEvent.SubmissionId.Value,
            AssignmentId = domainEvent.AssignmentId.Value
        });

        var envelope = new JobEnvelopeDto(
            JobName: "submission.grade",
            Payload: payload,
            CorrelationId: Guid.NewGuid().ToString("N"),
            CreatedAt: domainEvent.OccurredOn,
            RetryCount: 0);

        return _backgroundJobQueuePort.EnqueueAsync(envelope, ct);
    }
}
