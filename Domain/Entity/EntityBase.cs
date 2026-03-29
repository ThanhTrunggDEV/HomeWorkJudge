using System.Collections.Generic;
using Domain.Event;

namespace Domain.Entity;

public abstract class EntityBase
{
    private readonly List<IDomainEvent> _events = [];
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _events;

    protected void Raise(IDomainEvent @event) => _events.Add(@event);

    public void ClearDomainEvents() => _events.Clear();
}