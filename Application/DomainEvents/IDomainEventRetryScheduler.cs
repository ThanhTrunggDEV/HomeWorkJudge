using System;
using System.Threading;
using System.Threading.Tasks;
using Domain.Event;

namespace Application.DomainEvents;

public interface IDomainEventRetryScheduler
{
    Task ScheduleAsync(IDomainEvent domainEvent, string handlerName, Exception error, CancellationToken ct);
}
