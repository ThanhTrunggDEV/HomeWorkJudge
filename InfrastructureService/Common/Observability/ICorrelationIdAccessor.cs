namespace InfrastructureService.Common.Observability;

public interface ICorrelationIdAccessor
{
    string GetOrCreate();
    void Set(string correlationId);
    void Clear();
}
