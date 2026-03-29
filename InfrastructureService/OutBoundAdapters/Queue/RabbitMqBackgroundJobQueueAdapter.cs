using System.Threading;
using System.Threading.Tasks;
using InfrastructureService.Common.Errors;
using Microsoft.Extensions.Logging;
using Ports.DTO.Common;
using Ports.OutBoundPorts.Queue;

namespace InfrastructureService.OutBoundAdapters.Queue;

public sealed class RabbitMqBackgroundJobQueueAdapter : IBackgroundJobQueuePort
{
    private readonly ILogger<RabbitMqBackgroundJobQueueAdapter> _logger;

    public RabbitMqBackgroundJobQueueAdapter(ILogger<RabbitMqBackgroundJobQueueAdapter> logger)
    {
        _logger = logger;
    }

    public Task EnqueueAsync(JobEnvelopeDto envelope, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "RabbitMQ queue provider is configured but adapter implementation is pending. Job {JobName} was not published.",
            envelope.JobName);

        throw new InfrastructureException(
            "QUEUE_PROVIDER_NOT_IMPLEMENTED",
            "RabbitMq provider is configured but not implemented in this phase. Switch Queue.Provider to InMemory or implement RabbitMq adapter.");
    }
}
