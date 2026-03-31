namespace HomeWorkJudge.UI.ViewModels.Tests.Support;

internal sealed class TestServiceProvider : IServiceProvider
{
    private readonly Dictionary<Type, object?> _map = new();

    public void Register<T>(T instance) where T : class
        => _map[typeof(T)] = instance;

    public object? GetService(Type serviceType)
        => _map.TryGetValue(serviceType, out var value) ? value : null;
}
