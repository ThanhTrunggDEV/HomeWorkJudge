using System.Threading;
using System.Threading.Tasks;
using Domain.Event;
using Microsoft.Extensions.Logging;

namespace Application.DomainEvents.Handlers;

public sealed class SubmissionGradingCompletedEventHandler : IDomainEventHandler<SubmissionGradingCompletedEvent>
{
    private readonly ILogger<SubmissionGradingCompletedEventHandler> _logger;

    public SubmissionGradingCompletedEventHandler(ILogger<SubmissionGradingCompletedEventHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(SubmissionGradingCompletedEvent domainEvent, CancellationToken ct)
    {
        _logger.LogInformation(
            "Submission {SubmissionId} grading completed. Total score: {TotalScore}.",
            domainEvent.SubmissionId.Value,
            domainEvent.TotalScore);

        return Task.CompletedTask;
    }
}
