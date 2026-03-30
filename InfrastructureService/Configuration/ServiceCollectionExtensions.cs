using InfrastructureService.Common.Observability;
using InfrastructureService.Common.Queue;
using InfrastructureService.Common.Resilience;
using InfrastructureService.Configuration.Options;
using InfrastructureService.OutBoundAdapters.AI;
using InfrastructureService.OutBoundAdapters.Judging;
using InfrastructureService.OutBoundAdapters.Plagiarism;
using InfrastructureService.OutBoundAdapters.Queue;
using InfrastructureService.OutBoundAdapters.Report;
using InfrastructureService.OutBoundAdapters.RubricGrading;
using InfrastructureService.OutBoundAdapters.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
        IConfiguration configuration,
        IHostEnvironment? hostEnvironment = null)
    {
        var infrastructureSection = configuration.GetSection("Infrastructure");
        var queueOptions = infrastructureSection.GetSection("Queue").Get<QueueOptions>() ?? new QueueOptions();
        var aiOptions = infrastructureSection.GetSection("AI").Get<AiOptions>() ?? new AiOptions();
        var queueProvider = queueOptions.Provider?.Trim() ?? "InMemory";
        var aiProvider = aiOptions.Provider?.Trim() ?? "OpenAI";
        var isDevelopment = hostEnvironment?.IsDevelopment() ??
            string.Equals(
                Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                Environments.Development,
                StringComparison.OrdinalIgnoreCase);

        var isInMemoryProvider = string.Equals(queueProvider, "InMemory", StringComparison.OrdinalIgnoreCase);
        var isRabbitProvider = string.Equals(queueProvider, "RabbitMq", StringComparison.OrdinalIgnoreCase);

        if (!isInMemoryProvider && !isRabbitProvider)
        {
            throw new InvalidOperationException($"Unsupported Queue.Provider '{queueProvider}'. Supported values: InMemory, RabbitMq.");
        }

        var fallbackToInProcessInDevelopment =
            isRabbitProvider && isDevelopment && queueOptions.AllowInProcessInDevelopment;
        var useInMemoryQueue = isInMemoryProvider || fallbackToInProcessInDevelopment;

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

        if (useInMemoryQueue)
        {
            services.AddSingleton<IJobEnvelopeQueue, InMemoryJobEnvelopeQueue>();
            services.AddSingleton<IBackgroundJobProcessor, LoggingBackgroundJobProcessor>();
            services.AddSingleton<IDeadLetterJobSink, InMemoryDeadLetterJobSink>();
            services.AddScoped<IBackgroundJobQueuePort, InMemoryBackgroundJobQueueAdapter>();
        }
        else
        {
            services.AddScoped<IBackgroundJobQueuePort, RabbitMqBackgroundJobQueueAdapter>();
        }

        services.AddScoped<IFileStoragePort, LocalFileStoragePort>();
        services.AddScoped<IReportExportPort, CsvReportExportPort>();
        services.AddScoped<ICodeCompilationPort, LocalCodeCompilationPort>();
        services.AddScoped<ICodeExecutionPort, LocalCodeExecutionPort>();
        services.AddScoped<ITestCaseJudgePort, LocalTestCaseJudgePort>();

        if (string.Equals(aiProvider, "OpenAI", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<OpenAiGradingPort>(client =>
            {
                var timeoutSeconds = Math.Max(1, aiOptions.TimeoutSeconds);
                client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            });
            services.AddScoped<IAiGradingPort>(sp => sp.GetRequiredService<OpenAiGradingPort>());
        }
        else if (string.Equals(aiProvider, "Gemini", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<GeminiGradingPort>(client =>
            {
                var timeoutSeconds = Math.Max(1, aiOptions.TimeoutSeconds);
                client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            });
            services.AddScoped<IAiGradingPort>(sp => sp.GetRequiredService<GeminiGradingPort>());
        }
        else
        {
            throw new InvalidOperationException($"Unsupported AI.Provider '{aiProvider}'. Supported values: OpenAI, Gemini.");
        }

        services.AddScoped<IRubricGradingPort, HybridRubricGradingPort>();
        services.AddScoped<IPlagiarismDetectionPort, LocalPlagiarismDetectionPort>();

        var useInProcessConsumer =
            useInMemoryQueue && (
                string.Equals(queueOptions.ConsumerMode, "InProcess", StringComparison.OrdinalIgnoreCase) ||
                (isDevelopment && queueOptions.AllowInProcessInDevelopment));

        if (useInProcessConsumer)
        {
            services.AddHostedService<InProcessJobBackgroundService>();
        }

        return services;
    }
}
