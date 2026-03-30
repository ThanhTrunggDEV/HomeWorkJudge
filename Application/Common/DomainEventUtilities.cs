using System.Collections.Generic;
using System.Linq;
using Domain.Entity;
using Domain.Event;

namespace Application.Common;

internal static class DomainEventUtilities
{
    public static List<IDomainEvent> SnapshotEvents(params EntityBase?[] entities)
        => entities
            .Where(entity => entity is not null)
            .SelectMany(entity => entity!.DomainEvents)
            .ToList();

    public static void ClearEvents(params EntityBase?[] entities)
    {
        foreach (var entity in entities)
        {
            entity?.ClearDomainEvents();
        }
    }
}
