using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Domain.Event;

namespace Application.DomainEvents;

/// <summary>
/// Contract để dispatch domain events sau khi SaveChanges thành công.
/// Implemented bởi DomainEventDispatcher trong Application layer.
/// Inject vào Use Case handlers để dispatch events.
/// </summary>
public interface IDomainEventDispatcher
{
    Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken ct = default);
}
