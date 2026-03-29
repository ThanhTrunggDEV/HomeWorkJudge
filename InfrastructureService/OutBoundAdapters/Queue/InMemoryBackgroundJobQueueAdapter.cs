using System;
using System.Threading;
using System.Threading.Tasks;
using InfrastructureService.Common.Observability;
using InfrastructureService.Common.Queue;
using Microsoft.Extensions.Logging;
using Ports.DTO.Common;
using Ports.OutBoundPorts.Queue;

namespace InfrastructureService.OutBoundAdapters.Queue;

public sealed class InMemoryBackgroundJobQueueAdapter : IBackgroundJobQueuePort
{
    private readonly IJobEnvelopeQueue _queue;
    private readonly ICorrelationIdAccessor _correlationIdAccessor;
    private readonly ILogger<InMemoryBackgroundJobQueueAdapter> _logger;

    public InMemoryBackgroundJobQueueAdapter(
        IJobEnvelopeQueue queue,
        ICorrelationIdAccessor correlationIdAccessor,
        ILogger<InMemoryBackgroundJobQueueAdapter> logger)
    {
        _queue = queue;
        _correlationIdAccessor = correlationIdAccessor;
        _logger = logger;
    }

    public async Task EnqueueAsync(JobEnvelopeDto envelope, CancellationToken cancellationToken = default)
    {
        if (envelope is null)
        {
            throw new ArgumentNullException(nameof(envelope));
        }

        var correlationId = string.IsNullOrWhiteSpace(envelope.CorrelationId)
            ? _correlationIdAccessor.GetOrCreate()
            : envelope.CorrelationId;

        var normalized = envelope with
        {
            CorrelationId = correlationId,
            CreatedAt = envelope.CreatedAt == default ? DateTimeOffset.UtcNow : envelope.CreatedAt
        };

        await _queue.EnqueueAsync(normalized, cancellationToken);

        _logger.LogInformation(
            "Enqueued background job {JobName} with correlation {CorrelationId}.",
            normalized.JobName,
            normalized.CorrelationId);
    }
}
