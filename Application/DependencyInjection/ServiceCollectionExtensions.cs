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
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<DomainEventDispatcher>();

        // Use Case Handlers
        services.AddScoped<IRubricUseCase, RubricUseCaseHandler>();
        services.AddScoped<IGradingSessionUseCase, GradingSessionUseCaseHandler>();
        services.AddScoped<IGradingUseCase, GradingUseCaseHandler>();
        services.AddScoped<IReportUseCase, ReportUseCaseHandler>();

        return services;
    }
}
