using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Domain.Event;
using Ports.DTO.Common;
using Ports.OutBoundPorts.Queue;

namespace Application.DomainEvents.Handlers;

public sealed class AssignmentPublishedEventHandler : IDomainEventHandler<AssignmentPublishedEvent>
{
    private readonly IBackgroundJobQueuePort _backgroundJobQueuePort;

    public AssignmentPublishedEventHandler(IBackgroundJobQueuePort backgroundJobQueuePort)
    {
        _backgroundJobQueuePort = backgroundJobQueuePort;
    }

    public Task HandleAsync(AssignmentPublishedEvent domainEvent, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new
        {
            AssignmentId = domainEvent.AssignmentId.Value
        });

        var envelope = new JobEnvelopeDto(
            JobName: "assignment.rejudge",
            Payload: payload,
            CorrelationId: Guid.NewGuid().ToString("N"),
            CreatedAt: domainEvent.OccurredOn,
            RetryCount: 0);

        return _backgroundJobQueuePort.EnqueueAsync(envelope, ct);
    }
}
