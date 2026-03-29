using InfrastructureService.Common.Observability;
using InfrastructureService.Common.Resilience;
using InfrastructureService.Configuration.Options;
using InfrastructureService.OutBoundAdapters.Scaffold;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ports.OutBoundPorts.AI;
using Ports.OutBoundPorts.Judging;
using Ports.OutBoundPorts.Plagiarism;
using Ports.OutBoundPorts.Queue;
using Ports.OutBoundPorts.Report;
using Ports.OutBoundPorts.RubricGrading;
using Ports.OutBoundPorts.Storage;

namespace InfrastructureService.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServiceFoundation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var infrastructureSection = configuration.GetSection("Infrastructure");

        services.Configure<InfrastructureOptions>(infrastructureSection);
        services.Configure<QueueOptions>(infrastructureSection.GetSection("Queue"));
        services.Configure<StorageOptions>(infrastructureSection.GetSection("Storage"));
        services.Configure<ReportOptions>(infrastructureSection.GetSection("Report"));
        services.Configure<JudgingOptions>(infrastructureSection.GetSection("Judging"));
        services.Configure<AiOptions>(infrastructureSection.GetSection("AI"));
        services.Configure<RubricOptions>(infrastructureSection.GetSection("Rubric"));
        services.Configure<PlagiarismOptions>(infrastructureSection.GetSection("Plagiarism"));
        services.Configure<ResilienceOptions>(infrastructureSection.GetSection("Resilience"));

        services.AddSingleton<ICorrelationIdAccessor, CorrelationIdAccessor>();
        services.AddSingleton<IOperationExecutor, DefaultOperationExecutor>();

        services.AddScoped<IBackgroundJobQueuePort, ScaffoldBackgroundJobQueueAdapter>();
        services.AddScoped<IFileStoragePort, ScaffoldFileStoragePort>();
        services.AddScoped<IReportExportPort, ScaffoldReportExportPort>();
        services.AddScoped<ICodeCompilationPort, ScaffoldCodeCompilationPort>();
        services.AddScoped<ICodeExecutionPort, ScaffoldCodeExecutionPort>();
        services.AddScoped<ITestCaseJudgePort, ScaffoldTestCaseJudgePort>();
        services.AddScoped<IAiGradingPort, ScaffoldAiGradingPort>();
        services.AddScoped<IRubricGradingPort, ScaffoldRubricGradingPort>();
        services.AddScoped<IPlagiarismDetectionPort, ScaffoldPlagiarismDetectionPort>();

        return services;
    }
}
