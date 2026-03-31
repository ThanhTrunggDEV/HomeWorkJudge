using InfrastructureService.Common.Resilience;
using InfrastructureService.Configuration.Options;
using InfrastructureService.OutBoundAdapters.AI;
using InfrastructureService.OutBoundAdapters.Plagiarism;
using InfrastructureService.OutBoundAdapters.Report;
using InfrastructureService.OutBoundAdapters.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ports.OutBoundPorts.AI;
using Ports.OutBoundPorts.Plagiarism;
using Ports.OutBoundPorts.Report;
using Ports.OutBoundPorts.Storage;

namespace InfrastructureService.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Options
        services.Configure<InfrastructureOptions>(configuration.GetSection("Infrastructure"));
        services.Configure<AiOptions>(configuration.GetSection("Infrastructure:AI"));
        services.Configure<ResilienceOptions>(configuration.GetSection("Infrastructure:Resilience"));

        // Resilience
        services.AddSingleton<IOperationExecutor, DefaultOperationExecutor>();

        // AI adapters
        services.AddHttpClient<GeminiGradingPort>();
        services.AddScoped<IAiGradingPort, GeminiGradingPort>();
        services.AddScoped<IAiRubricGeneratorPort, GeminiRubricGeneratorPort>();

        // Plagiarism
        services.AddScoped<IPlagiarismDetectionPort, LocalPlagiarismDetectionPort>();

        // File extraction (zip/rar)
        services.AddScoped<IFileExtractorPort, ZipFileExtractorPort>();

        // Report export (CSV/XLSX)
        services.AddScoped<IReportExportPort, ReportExportPort>();

        return services;
    }
}
