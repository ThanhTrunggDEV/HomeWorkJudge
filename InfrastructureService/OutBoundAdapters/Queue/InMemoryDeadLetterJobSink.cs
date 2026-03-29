using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InfrastructureService.Common.Queue;
using Microsoft.Extensions.Logging;
using Ports.DTO.Common;

namespace InfrastructureService.OutBoundAdapters.Queue;

public sealed class InMemoryDeadLetterJobSink : IDeadLetterJobSink
{
    private readonly ConcurrentQueue<JobEnvelopeDto> _deadLetters = new();
    private readonly ILogger<InMemoryDeadLetterJobSink> _logger;

    public InMemoryDeadLetterJobSink(ILogger<InMemoryDeadLetterJobSink> logger)
    {
        _logger = logger;
    }

    public Task SaveAsync(JobEnvelopeDto envelope, string reason, CancellationToken cancellationToken = default)
    {
        _deadLetters.Enqueue(envelope);
        _logger.LogError(
            "Dead-lettered job {JobName} with correlation {CorrelationId}. Reason: {Reason}",
            envelope.JobName,
            envelope.CorrelationId,
            reason);

        return Task.CompletedTask;
    }

    public IReadOnlyCollection<JobEnvelopeDto> Snapshot() => _deadLetters.ToArray();
}
