using Application.DomainEvents;
using Application.DomainEvents.Handlers;
using Application.UseCases.Assignment;
using Application.UseCases.Classroom;
using Application.UseCases.Grading;
using Application.UseCases.Query;
using Application.UseCases.Report;
using Application.UseCases.Submission;
using Application.UseCases.User;
using Domain.Event;
using Microsoft.Extensions.DependencyInjection;
using Ports.InBoundPorts.Assignment;
using Ports.InBoundPorts.Classroom;
using Ports.InBoundPorts.Grading;
using Ports.InBoundPorts.Query;
using Ports.InBoundPorts.Report;
using Ports.InBoundPorts.Submission;
using Ports.InBoundPorts.User;

namespace Application.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationUseCases(this IServiceCollection services)
    {
        services.AddScoped<IRegisterUserUseCase, RegisterUserUseCase>();
        services.AddScoped<ILoginUseCase, LoginUseCase>();
        services.AddScoped<IAssignUserRoleUseCase, AssignUserRoleUseCase>();

        services.AddScoped<ICreateClassroomUseCase, CreateClassroomUseCase>();
        services.AddScoped<IJoinClassroomUseCase, JoinClassroomUseCase>();

        services.AddScoped<ICreateAssignmentUseCase, CreateAssignmentUseCase>();
        services.AddScoped<IUpdateAssignmentUseCase, UpdateAssignmentUseCase>();
        services.AddScoped<IListAssignmentsUseCase, ListAssignmentsUseCase>();
        services.AddScoped<IPublishAssignmentUseCase, PublishAssignmentUseCase>();
        services.AddScoped<IAddAssignmentTestCaseUseCase, AddAssignmentTestCaseUseCase>();
        services.AddScoped<IUpdateAssignmentTestCaseUseCase, UpdateAssignmentTestCaseUseCase>();
        services.AddScoped<IDeleteAssignmentTestCaseUseCase, DeleteAssignmentTestCaseUseCase>();
        services.AddScoped<ICreateAssignmentRubricUseCase, CreateAssignmentRubricUseCase>();
        services.AddScoped<IUpdateAssignmentRubricUseCase, UpdateAssignmentRubricUseCase>();
        services.AddScoped<IRejudgeAssignmentUseCase, RejudgeAssignmentUseCase>();

        services.AddScoped<ISubmitCodeUseCase, SubmitCodeUseCase>();

        services.AddScoped<IGradeSubmissionByTestCaseUseCase, GradeSubmissionByTestCaseUseCase>();
        services.AddScoped<IGradeSubmissionByRubricUseCase, GradeSubmissionByRubricUseCase>();
        services.AddScoped<IOverrideSubmissionScoreUseCase, OverrideSubmissionScoreUseCase>();
        services.AddScoped<IReviewAiRubricResultUseCase, ReviewAiRubricResultUseCase>();
        services.AddScoped<IOverrideRubricCriteriaScoreUseCase, OverrideRubricCriteriaScoreUseCase>();
        services.AddScoped<IExplainRubricDeductionUseCase, ExplainRubricDeductionUseCase>();

        services.AddScoped<IGetSubmissionDetailUseCase, GetSubmissionDetailUseCase>();
        services.AddScoped<IGetScoreboardUseCase, GetScoreboardUseCase>();
        services.AddScoped<IGetSubmissionHistoryUseCase, GetSubmissionHistoryUseCase>();

        services.AddScoped<IExportScoreReportUseCase, ExportScoreReportUseCase>();

        services.AddScoped<DomainEventDispatcher>();
        services.AddScoped<IDomainEventRetryScheduler, QueueDomainEventRetryScheduler>();
        services.AddScoped<IDomainEventHandler<AssignmentPublishedEvent>, AssignmentPublishedEventHandler>();
        services.AddScoped<IDomainEventHandler<SubmissionCreatedEvent>, SubmissionCreatedEventHandler>();
        services.AddScoped<IDomainEventHandler<SubmissionGradingCompletedEvent>, SubmissionGradingCompletedEventHandler>();

        return services;
    }
}
