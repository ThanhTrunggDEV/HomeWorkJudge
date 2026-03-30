using System.Threading;
using System.Threading.Tasks;
using Domain.Event;

namespace Application.DomainEvents;

public interface IDomainEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken ct);
}
