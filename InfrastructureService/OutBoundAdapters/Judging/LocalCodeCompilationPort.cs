using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using InfrastructureService.Common.Errors;
using InfrastructureService.Common.Resilience;
using InfrastructureService.Configuration.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ports.DTO.Submission;
using Ports.OutBoundPorts.Judging;

namespace InfrastructureService.OutBoundAdapters.Judging;

public sealed class LocalCodeCompilationPort : ICodeCompilationPort
{
    private readonly IOperationExecutor _operationExecutor;
    private readonly JudgingOptions _options;
    private readonly ILogger<LocalCodeCompilationPort> _logger;

    public LocalCodeCompilationPort(
        IOperationExecutor operationExecutor,
        IOptions<JudgingOptions> options,
        ILogger<LocalCodeCompilationPort> logger)
    {
        _operationExecutor = operationExecutor;
        _options = options.Value;
        _logger = logger;
    }

    public Task<CodeCompilationResultDto> CompileAsync(
        string sourceCode,
        string language,
        CancellationToken cancellationToken = default)
        => _operationExecutor.ExecuteAsync(
            "judging.compile",
            ct => CompileInternalAsync(sourceCode, language, ct),
            cancellationToken);

    private async Task<CodeCompilationResultDto> CompileInternalAsync(
        string sourceCode,
        string language,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            throw new InfrastructureException("JUDGING_EMPTY_SOURCE", "Source code cannot be empty.");
        }

        if (!string.Equals(language, "csharp", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(language, "cs", StringComparison.OrdinalIgnoreCase))
        {
            return new CodeCompilationResultDto(
                false,
                $"Language '{language}' is not supported by local compiler.",
                null);
        }

        var workspacePath = Path.Combine(
            Path.GetTempPath(),
            "homeworkjudge",
            "judging",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(workspacePath);

        var programPath = Path.Combine(workspacePath, "Program.cs");
        var csprojPath = Path.Combine(workspacePath, "Submission.csproj");

        await File.WriteAllTextAsync(programPath, sourceCode, Encoding.UTF8, cancellationToken);
        await File.WriteAllTextAsync(csprojPath, BuildCsproj(), Encoding.UTF8, cancellationToken);

        var timeout = TimeSpan.FromSeconds(Math.Max(1, _options.CompileTimeoutSeconds));
        var processResult = await ProcessExecutionHelper.RunAsync(
            "dotnet",
            "build --configuration Release --nologo",
            workspacePath,
            null,
            timeout,
            cancellationToken);

        if (processResult.TimedOut)
        {
            _logger.LogWarning("Compilation timed out in {TimeoutSeconds}s.", _options.CompileTimeoutSeconds);
            return new CodeCompilationResultDto(false, "Compilation timed out.", null);
        }

        if (processResult.ExitCode != 0)
        {
            var compilerOutput = BuildProcessOutput(processResult);
            return new CodeCompilationResultDto(false, compilerOutput, null);
        }

        var artifactPath = Path.Combine(workspacePath, "bin", "Release", "net9.0", "Submission.dll");
        if (!File.Exists(artifactPath))
        {
            return new CodeCompilationResultDto(false, "Compilation did not produce Submission.dll artifact.", null);
        }

        return new CodeCompilationResultDto(true, BuildProcessOutput(processResult), artifactPath);
    }

    private static string BuildCsproj() =>
        """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <OutputType>Exe</OutputType>
            <TargetFramework>net9.0</TargetFramework>
            <ImplicitUsings>enable</ImplicitUsings>
            <Nullable>enable</Nullable>
          </PropertyGroup>
        </Project>
        """;

    private static string BuildProcessOutput(ProcessExecutionResult processResult)
    {
        if (string.IsNullOrWhiteSpace(processResult.StandardError))
        {
            return processResult.StandardOutput;
        }

        if (string.IsNullOrWhiteSpace(processResult.StandardOutput))
        {
            return processResult.StandardError;
        }

        return processResult.StandardOutput + Environment.NewLine + processResult.StandardError;
    }
}