using System;
using System.Threading;

namespace InfrastructureService.Common.Observability;

public sealed class CorrelationIdAccessor : ICorrelationIdAccessor
{
    private static readonly AsyncLocal<string?> Current = new();

    public string GetOrCreate()
    {
        if (string.IsNullOrWhiteSpace(Current.Value))
        {
            Current.Value = Guid.NewGuid().ToString("N");
        }

        return Current.Value;
    }

    public void Set(string correlationId)
    {
        Current.Value = correlationId;
    }

    public void Clear()
    {
        Current.Value = null;
    }
}
