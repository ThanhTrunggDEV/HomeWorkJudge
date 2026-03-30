using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Domain.Event;
using Ports.DTO.Common;
using Ports.OutBoundPorts.Queue;

namespace Application.DomainEvents;

public sealed class QueueDomainEventRetryScheduler : IDomainEventRetryScheduler
{
    private readonly IBackgroundJobQueuePort _backgroundJobQueuePort;

    public QueueDomainEventRetryScheduler(IBackgroundJobQueuePort backgroundJobQueuePort)
    {
        _backgroundJobQueuePort = backgroundJobQueuePort;
    }

    public Task ScheduleAsync(IDomainEvent domainEvent, string handlerName, Exception error, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new
        {
            EventType = domainEvent.GetType().FullName,
            HandlerName = handlerName,
            Error = error.Message,
            OccurredOn = domainEvent.OccurredOn,
            DomainEvent = domainEvent
        });

        var envelope = new JobEnvelopeDto(
            JobName: "domain-event.retry",
            Payload: payload,
            CorrelationId: Guid.NewGuid().ToString("N"),
            CreatedAt: DateTimeOffset.UtcNow,
            RetryCount: 0);

        return _backgroundJobQueuePort.EnqueueAsync(envelope, ct);
    }
}
