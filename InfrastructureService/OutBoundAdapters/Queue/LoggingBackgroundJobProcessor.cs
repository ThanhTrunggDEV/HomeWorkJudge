using System.Threading;
using System.Threading.Tasks;
using InfrastructureService.Common.Queue;
using Microsoft.Extensions.Logging;
using Ports.DTO.Common;

namespace InfrastructureService.OutBoundAdapters.Queue;

public sealed class LoggingBackgroundJobProcessor : IBackgroundJobProcessor
{
    private readonly ILogger<LoggingBackgroundJobProcessor> _logger;

    public LoggingBackgroundJobProcessor(ILogger<LoggingBackgroundJobProcessor> logger)
    {
        _logger = logger;
    }

    public Task ProcessAsync(JobEnvelopeDto envelope, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processed background job {JobName} with correlation {CorrelationId}.",
            envelope.JobName,
            envelope.CorrelationId);

        return Task.CompletedTask;
    }
}
