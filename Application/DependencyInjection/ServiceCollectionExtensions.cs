using Application.DomainEvents;
using Application.UseCases;
using Microsoft.Extensions.DependencyInjection;
using Ports.InBoundPorts.GradingSession;
using Ports.InBoundPorts.Grading;
using Ports.InBoundPorts.Report;
using Ports.InBoundPorts.Rubric;

namespace Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Domain Event Dispatcher
        services.AddTransient<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddTransient<DomainEventDispatcher>();

        // Use Case Handlers
        services.AddTransient<IRubricUseCase, RubricUseCaseHandler>();
        services.AddTransient<IGradingSessionUseCase, GradingSessionUseCaseHandler>();
        services.AddTransient<IGradingUseCase, GradingUseCaseHandler>();
        services.AddTransient<IReportUseCase, ReportUseCaseHandler>();

        return services;
    }
}
