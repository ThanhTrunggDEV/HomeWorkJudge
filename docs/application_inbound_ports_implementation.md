# Ke hoach trien khai InBoundPorts trong Application

Tai lieu nay mo ta cach trien khai toan bo InBoundPorts trong du an Application theo huong Clean Architecture + DDD, kem DomainEventDispatcher va EventHandlers.

## 1. Muc tieu

- Implement day du cac use case interface trong `Ports/InBoundPorts` tai project Application.
- Dong bo voi Domain model va OutboundPorts da co.
- Co co che dispatch Domain Event sau khi commit thanh cong.
- Co mau EventHandler de xu ly side-effects ro rang, mo rong duoc.

## 2. Danh sach InBoundPorts can implement

### 2.1 User

- `IRegisterUserUseCase`
- `ILoginUseCase`
- `IAssignUserRoleUseCase`

### 2.2 Classroom

- `ICreateClassroomUseCase`
- `IJoinClassroomUseCase`

### 2.3 Assignment

- `ICreateAssignmentUseCase`
- `IUpdateAssignmentUseCase`
- `IListAssignmentsUseCase`
- `IPublishAssignmentUseCase`
- `IAddAssignmentTestCaseUseCase`
- `IUpdateAssignmentTestCaseUseCase`
- `IDeleteAssignmentTestCaseUseCase`
- `ICreateAssignmentRubricUseCase`
- `IUpdateAssignmentRubricUseCase`
- `IRejudgeAssignmentUseCase`

### 2.4 Submission

- `ISubmitCodeUseCase`

### 2.5 Grading

- `IGradeSubmissionByTestCaseUseCase`
- `IGradeSubmissionByRubricUseCase`
- `IOverrideSubmissionScoreUseCase`
- `IReviewAiRubricResultUseCase`
- `IOverrideRubricCriteriaScoreUseCase`
- `IExplainRubricDeductionUseCase`

### 2.6 Query

- `IGetSubmissionDetailUseCase`
- `IGetScoreboardUseCase`
- `IGetSubmissionHistoryUseCase`

### 2.7 Report

- `IExportScoreReportUseCase`

## 3. Cau truc de xuat cho project Application

```text
Application/
  UseCases/
    User/
    Classroom/
    Assignment/
    Submission/
    Grading/
    Query/
    Report/
  DomainEvents/
    IDomainEventHandler.cs
    DomainEventDispatcher.cs
    Handlers/
      AssignmentPublishedEventHandler.cs
      SubmissionCreatedEventHandler.cs
      SubmissionGradingCompletedEventHandler.cs
  Mapping/
  DependencyInjection/
    ServiceCollectionExtensions.cs
```

## 4. Pattern xu ly use case trong Application

Moi use case nen theo flow thong nhat:

1. Validate input DTO.
2. Load tat ca aggregate can thay doi tu repository (Domain.Ports).
3. Goi method domain de thay doi trang thai.
4. Thu thap DomainEvents tu tat ca aggregate da thay doi (snapshot ra list).
5. Persist qua repository + `IUnitOfWork.SaveChangesAsync`.
6. Sau khi save thanh cong moi goi `DomainEventDispatcher`.
7. Neu handler fail: dua vao retry nen (khong mat side-effect sau commit).
8. Clear domain events tren cac aggregate da thu thap.
9. Map ket qua ve DTO response.

Pseudo-flow:

```csharp
public async Task<SomeResponseDto> HandleAsync(SomeRequestDto request, CancellationToken ct)
{
    // 1) validate
    // 2) load all changed aggregates
    // 3) execute domain behavior
    // 4) collect domain events snapshot from all changed aggregates
    // 5) save changes via unit of work
    // 6) dispatch events after successful commit
    // 7) clear domain events
    // 8) return response
}
```

### 4.1 Mau thu tu SaveChanges truoc, Dispatch sau

```csharp
public async Task HandleAsync(SomeRequestDto request, CancellationToken ct)
{
    var aggregateA = await assignmentRepository.GetByIdAsync(new AssignmentId(request.AssignmentId));
    var aggregateB = await submissionRepository.GetByIdAsync(new SubmissionId(request.SubmissionId));

    // domain behavior
    aggregateA!.Publish(DateTime.UtcNow);
    aggregateB!.MarkExecuting();

    var changedAggregates = new EntityBase[] { aggregateA, aggregateB };
    var domainEvents = changedAggregates
        .SelectMany(x => x.DomainEvents)
        .ToList();

    await unitOfWork.SaveChangesAsync(ct);
    await domainEventDispatcher.DispatchAsync(domainEvents, ct);

    foreach (var aggregate in changedAggregates)
    {
        aggregate.ClearDomainEvents();
    }
}
```

## 5. Domain Event abstractions trong Application

### 5.1 Interface handler

```csharp
using System.Threading;
using System.Threading.Tasks;
using Domain.Event;

namespace Application.DomainEvents;

public interface IDomainEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken ct);
}
```

### 5.2 DomainEventDispatcher (mau reflection nhu yeu cau)

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Domain.Event;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Application.DomainEvents;

public sealed class DomainEventDispatcher(
    IServiceProvider serviceProvider,
    IDomainEventRetryScheduler retryScheduler,
    ILogger<DomainEventDispatcher> logger)
{
    public async Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken ct)
    {
        foreach (var domainEvent in events)
        {
            var handlerType = typeof(IDomainEventHandler<>)
                .MakeGenericType(domainEvent.GetType());

            var handlers = serviceProvider.GetServices(handlerType);

            foreach (var handler in handlers)
            {
                var method = handlerType.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.HandleAsync))!;
                try
                {
                    await (Task)method.Invoke(handler, new object[] { domainEvent, ct })!;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Domain event handler failed for {EventType}. Scheduling background retry.", domainEvent.GetType().Name);
                    await retryScheduler.ScheduleAsync(domainEvent, handlerType.FullName ?? handlerType.Name, ex, ct);
                }
            }
        }
    }
}
```

Ghi chu:
- Dispatcher bat buoc duoc goi sau `SaveChangesAsync` thanh cong.
- Handler nao fail thi dua vao retry nen de tranh mat side-effect sau commit.
- Neu can hieu nang cao hon, co the cache `MethodInfo` theo event type.

### 5.3 Retry nen cho EventHandler

```csharp
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Domain.Event;
using Ports.DTO.Common;
using Ports.OutBoundPorts.Queue;

namespace Application.DomainEvents;

public interface IDomainEventRetryScheduler
{
    Task ScheduleAsync(IDomainEvent domainEvent, string handlerName, Exception error, CancellationToken ct);
}

public sealed class QueueDomainEventRetryScheduler(
    IBackgroundJobQueuePort backgroundJobQueuePort)
    : IDomainEventRetryScheduler
{
    public Task ScheduleAsync(IDomainEvent domainEvent, string handlerName, Exception error, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new
        {
            EventType = domainEvent.GetType().FullName,
            HandlerName = handlerName,
            Error = error.Message,
            OccurredOn = domainEvent.OccurredOn,
            DomainEvent = domainEvent
        });

        var envelope = new JobEnvelopeDto(
            JobName: "domain-event.retry",
            Payload: payload,
            CorrelationId: Guid.NewGuid().ToString("N"),
            CreatedAt: DateTimeOffset.UtcNow,
            RetryCount: 0);

        return backgroundJobQueuePort.EnqueueAsync(envelope, ct);
    }
}
```

Nguyen tac retry nen:
- Handler phai idempotent (co the chay lai nhieu lan ma khong gay sai du lieu).
- Dung exponential backoff + max retry count trong queue worker.
- Qua max retry thi dua vao dead-letter de dieu tra.

## 6. EventHandlers de xai ngay

Duoi day la 3 handler phu hop voi events hien co trong Domain:
- `AssignmentPublishedEvent`
- `SubmissionCreatedEvent`
- `SubmissionGradingCompletedEvent`

### 6.1 AssignmentPublishedEventHandler

Muc tieu: enqueue job rejudge assignment sau khi publish.

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Application.DomainEvents;
using Domain.Event;
using Ports.DTO.Common;
using Ports.OutBoundPorts.Queue;

namespace Application.DomainEvents.Handlers;

public sealed class AssignmentPublishedEventHandler(
    IBackgroundJobQueuePort backgroundJobQueuePort)
    : IDomainEventHandler<AssignmentPublishedEvent>
{
    public Task HandleAsync(AssignmentPublishedEvent domainEvent, CancellationToken ct)
    {
        var envelope = new JobEnvelopeDto(
            JobName: "assignment.rejudge",
            Payload: $"{{\"assignmentId\":\"{domainEvent.AssignmentId.Value}\"}}",
            CorrelationId: Guid.NewGuid().ToString("N"),
            CreatedAt: domainEvent.OccurredOn,
            RetryCount: 0);

        return backgroundJobQueuePort.EnqueueAsync(envelope, ct);
    }
}
```

### 6.2 SubmissionCreatedEventHandler

Muc tieu: enqueue grading job sau khi tao submission.

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Application.DomainEvents;
using Domain.Event;
using Ports.DTO.Common;
using Ports.OutBoundPorts.Queue;

namespace Application.DomainEvents.Handlers;

public sealed class SubmissionCreatedEventHandler(
    IBackgroundJobQueuePort backgroundJobQueuePort)
    : IDomainEventHandler<SubmissionCreatedEvent>
{
    public Task HandleAsync(SubmissionCreatedEvent domainEvent, CancellationToken ct)
    {
        var envelope = new JobEnvelopeDto(
            JobName: "submission.grade",
            Payload: $"{{\"submissionId\":\"{domainEvent.SubmissionId.Value}\",\"assignmentId\":\"{domainEvent.AssignmentId.Value}\"}}",
            CorrelationId: Guid.NewGuid().ToString("N"),
            CreatedAt: domainEvent.OccurredOn,
            RetryCount: 0);

        return backgroundJobQueuePort.EnqueueAsync(envelope, ct);
    }
}
```

### 6.3 SubmissionGradingCompletedEventHandler

Muc tieu: hook diem sau grading (co the notify, index, audit...).

```csharp
using System.Threading;
using System.Threading.Tasks;
using Application.DomainEvents;
using Domain.Event;
using Microsoft.Extensions.Logging;

namespace Application.DomainEvents.Handlers;

public sealed class SubmissionGradingCompletedEventHandler(
    ILogger<SubmissionGradingCompletedEventHandler> logger)
    : IDomainEventHandler<SubmissionGradingCompletedEvent>
{
    public Task HandleAsync(SubmissionGradingCompletedEvent domainEvent, CancellationToken ct)
    {
        logger.LogInformation(
            "Submission {SubmissionId} grading completed. Total score: {TotalScore}.",
            domainEvent.SubmissionId.Value,
            domainEvent.TotalScore);

        return Task.CompletedTask;
    }
}
```

## 7. Dang ky DI trong Application

```csharp
using Application.DomainEvents;
using Application.DomainEvents.Handlers;
using Domain.Event;
using Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationUseCases(this IServiceCollection services)
    {
        // Use cases
        // services.AddScoped<ISubmitCodeUseCase, SubmitCodeUseCase>();
        // services.AddScoped<IRejudgeAssignmentUseCase, RejudgeAssignmentUseCase>();
        // ... dang ky day du tat ca InBoundPorts

        // Domain events
        services.AddScoped<DomainEventDispatcher>();
        services.AddScoped<IDomainEventRetryScheduler, QueueDomainEventRetryScheduler>();
        services.AddScoped<IDomainEventHandler<AssignmentPublishedEvent>, AssignmentPublishedEventHandler>();
        services.AddScoped<IDomainEventHandler<SubmissionCreatedEvent>, SubmissionCreatedEventHandler>();
        services.AddScoped<IDomainEventHandler<SubmissionGradingCompletedEvent>, SubmissionGradingCompletedEventHandler>();

        return services;
    }
}
```

## 8. Mapping de xuat InBoundPort -> UseCase class

- `ISubmitCodeUseCase` -> `SubmitCodeUseCase`
- `IRejudgeAssignmentUseCase` -> `RejudgeAssignmentUseCase`
- `IGradeSubmissionByTestCaseUseCase` -> `GradeSubmissionByTestCaseUseCase`
- `IGradeSubmissionByRubricUseCase` -> `GradeSubmissionByRubricUseCase`
- `IGetSubmissionDetailUseCase` -> `GetSubmissionDetailUseCase`
- `IGetScoreboardUseCase` -> `GetScoreboardUseCase`
- `IExportScoreReportUseCase` -> `ExportScoreReportUseCase`
- Cac use case con lai ap dung cung naming convention.

## 9. Thu tu trien khai de xong nhanh luong nghiep vu

1. Submission + Assignment.Rejudge
2. Grading (testcase/rubric)
3. Query + Report
4. User/Classroom/Assignment CRUD con lai
5. Explain/Override cac use case quan tri

## 10. Definition of Done cho Application InBoundPorts

- Tat ca interface trong `Ports/InBoundPorts` deu co implementation.
- Moi use case co validate input + goi domain + collect events + save + dispatch event (neu co event).
- Dispatch chi duoc goi sau khi `SaveChangesAsync` thanh cong.
- Neu handler loi thi da co co che retry nen va dead-letter strategy.
- DomainEventDispatcher, retry scheduler va 3 event handlers ben tren da duoc register DI.
- Co unit test cho path thanh cong + loi nghiep vu chinh cua moi use case quan trong.
- Build solution xanh.
