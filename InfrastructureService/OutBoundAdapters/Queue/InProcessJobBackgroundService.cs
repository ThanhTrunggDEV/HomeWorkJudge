using System;
using System.Threading;
using System.Threading.Tasks;
using InfrastructureService.Common.Observability;
using InfrastructureService.Common.Queue;
using InfrastructureService.Configuration.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InfrastructureService.OutBoundAdapters.Queue;

public sealed class InProcessJobBackgroundService : BackgroundService
{
    private readonly IJobEnvelopeQueue _queue;
    private readonly IBackgroundJobProcessor _processor;
    private readonly IDeadLetterJobSink _deadLetterSink;
    private readonly ICorrelationIdAccessor _correlationIdAccessor;
    private readonly QueueOptions _queueOptions;
    private readonly ILogger<InProcessJobBackgroundService> _logger;

    public InProcessJobBackgroundService(
        IJobEnvelopeQueue queue,
        IBackgroundJobProcessor processor,
        IDeadLetterJobSink deadLetterSink,
        ICorrelationIdAccessor correlationIdAccessor,
        IOptions<QueueOptions> queueOptions,
        ILogger<InProcessJobBackgroundService> logger)
    {
        _queue = queue;
        _processor = processor;
        _deadLetterSink = deadLetterSink;
        _correlationIdAccessor = correlationIdAccessor;
        _queueOptions = queueOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("In-process background job consumer is running.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var envelope = await _queue.DequeueAsync(stoppingToken);
            var maxAttempts = Math.Max(1, _queueOptions.MaxRetryCount + 1);

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    _correlationIdAccessor.Set(envelope.CorrelationId);
                    await _processor.ProcessAsync(envelope, stoppingToken);
                    break;
                }
                catch (Exception ex) when (attempt < maxAttempts)
                {
                    _logger.LogWarning(
                        ex,
                        "Background job {JobName} failed on attempt {Attempt}/{MaxAttempts}. Retrying.",
                        envelope.JobName,
                        attempt,
                        maxAttempts);
                }
                catch (Exception ex)
                {
                    var deadLetterEnvelope = envelope with { RetryCount = envelope.RetryCount + maxAttempts };
                    try
                    {
                        await _deadLetterSink.SaveAsync(deadLetterEnvelope, ex.Message, stoppingToken);
                    }
                    catch (Exception sinkEx)
                    {
                        _logger.LogError(
                            sinkEx,
                            "Dead-letter sink failed for job {JobName} with correlation {CorrelationId}.",
                            deadLetterEnvelope.JobName,
                            deadLetterEnvelope.CorrelationId);
                    }

                    _logger.LogError(
                        ex,
                        "Background job {JobName} moved to dead-letter path after {MaxAttempts} attempts.",
                        envelope.JobName,
                        maxAttempts);
                }
                finally
                {
                    _correlationIdAccessor.Clear();
                }
            }
        }
    }
}
