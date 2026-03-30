using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Domain.Event;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Application.DomainEvents;

public sealed class DomainEventDispatcher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IDomainEventRetryScheduler _retryScheduler;
    private readonly ILogger<DomainEventDispatcher> _logger;

    public DomainEventDispatcher(
        IServiceProvider serviceProvider,
        IDomainEventRetryScheduler retryScheduler,
        ILogger<DomainEventDispatcher> logger)
    {
        _serviceProvider = serviceProvider;
        _retryScheduler = retryScheduler;
        _logger = logger;
    }

    public async Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken ct)
    {
        foreach (var domainEvent in events)
        {
            var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());
            var handlers = _serviceProvider.GetServices(handlerType);

            foreach (var handler in handlers)
            {
                var method = handlerType.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.HandleAsync));
                if (method is null)
                {
                    continue;
                }

                try
                {
                    await (Task)method.Invoke(handler, new object[] { domainEvent, ct })!;
                }
                catch (TargetInvocationException ex) when (ex.InnerException is not null)
                {
                    _logger.LogError(ex.InnerException, "Domain event handler failed for {EventType}. Scheduling background retry.", domainEvent.GetType().Name);
                    await TryScheduleRetryAsync(domainEvent, handlerType.FullName ?? handlerType.Name, ex.InnerException);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Domain event handler failed for {EventType}. Scheduling background retry.", domainEvent.GetType().Name);
                    await TryScheduleRetryAsync(domainEvent, handlerType.FullName ?? handlerType.Name, ex);
                }
            }
        }
    }

    private async Task TryScheduleRetryAsync(IDomainEvent domainEvent, string handlerName, Exception error)
    {
        try
        {
            await _retryScheduler.ScheduleAsync(domainEvent, handlerName, error, CancellationToken.None);
        }
        catch (Exception enqueueException)
        {
            _logger.LogError(
                enqueueException,
                "Failed to enqueue domain event retry for {EventType} and handler {HandlerName}.",
                domainEvent.GetType().Name,
                handlerName);
        }
    }
}
