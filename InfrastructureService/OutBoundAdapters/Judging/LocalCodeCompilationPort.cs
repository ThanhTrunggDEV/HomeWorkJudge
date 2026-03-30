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
    private static readonly string WorkspaceRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "homeworkjudge", "judging"));

    private readonly IOperationExecutor _operationExecutor;
    private readonly JudgingOptions _options;
    private readonly ILogger<LocalCodeCompilationPort> _logger;
    private readonly TimeSpan _workspaceRetention;

    public LocalCodeCompilationPort(
        IOperationExecutor operationExecutor,
        IOptions<JudgingOptions> options,
        ILogger<LocalCodeCompilationPort> logger)
    {
        _operationExecutor = operationExecutor;
        _options = options.Value;
        _logger = logger;
        _workspaceRetention = TimeSpan.FromMinutes(Math.Max(1, _options.WorkspaceRetentionMinutes));

        Directory.CreateDirectory(WorkspaceRoot);
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
        if (!_options.AllowUnsafeLocalExecution)
        {
            throw new InfrastructureException(
                "JUDGING_UNSAFE_EXECUTION_BLOCKED",
                "Local code compilation/execution is disabled. Set Infrastructure:Judging:AllowUnsafeLocalExecution=true only in trusted environments.");
        }

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

            CleanupStaleWorkspaces();

            var workspacePath = Path.Combine(WorkspaceRoot, Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(workspacePath);
        var keepWorkspace = false;

        try
        {
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
                0,
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

            keepWorkspace = true;
            return new CodeCompilationResultDto(true, BuildProcessOutput(processResult), artifactPath);
        }
        finally
        {
            if (!keepWorkspace)
            {
                TryDeleteWorkspace(workspacePath);
            }
        }
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

    private void TryDeleteWorkspace(string workspacePath)
    {
        if (!IsWorkspacePathAllowed(workspacePath))
        {
            _logger.LogWarning("Skip cleanup for workspace path {WorkspacePath} because it is outside allowed judging root.", workspacePath);
            return;
        }

        try
        {
            if (Directory.Exists(workspacePath))
            {
                Directory.Delete(workspacePath, recursive: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clean compilation workspace {WorkspacePath}.", workspacePath);
        }
    }

    private void CleanupStaleWorkspaces()
    {
        try
        {
            var cutoffUtc = DateTime.UtcNow.Subtract(_workspaceRetention);

            foreach (var directoryPath in Directory.EnumerateDirectories(WorkspaceRoot))
            {
                if (!IsWorkspacePathAllowed(directoryPath))
                {
                    continue;
                }

                var lastWriteUtc = Directory.GetLastWriteTimeUtc(directoryPath);
                if (lastWriteUtc >= cutoffUtc)
                {
                    continue;
                }

                Directory.Delete(directoryPath, recursive: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup stale judging workspaces under {WorkspaceRoot}.", WorkspaceRoot);
        }
    }

    private static bool IsWorkspacePathAllowed(string workspacePath)
    {
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            return false;
        }

        var normalizedRootPath = Path.GetFullPath(WorkspaceRoot)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedWorkspacePath = Path.GetFullPath(workspacePath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        var rootWithSeparator = normalizedRootPath + Path.DirectorySeparatorChar;
        var isUnderRoot = normalizedWorkspacePath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
        if (!isUnderRoot)
        {
            return false;
        }

        var directoryName = Path.GetFileName(normalizedWorkspacePath);
        return Guid.TryParseExact(directoryName, "N", out _);
    }
}