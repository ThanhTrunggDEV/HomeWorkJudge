using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using InfrastructureService.Common.Errors;
using InfrastructureService.Common.Resilience;
using InfrastructureService.Configuration.Options;
using Microsoft.Extensions.Options;
using Ports.DTO.AI;
using Ports.OutBoundPorts.AI;

namespace InfrastructureService.OutBoundAdapters.AI;

public sealed class MockAiGradingPort : IAiGradingPort
{
    private readonly IOperationExecutor _operationExecutor;
    private readonly AiOptions _options;

    public MockAiGradingPort(
        IOperationExecutor operationExecutor,
        IOptions<AiOptions> options)
    {
        _operationExecutor = operationExecutor;
        _options = options.Value;
    }

    public Task<AiGradeSubmissionResponseDto> GradeSubmissionAsync(
        AiGradeSubmissionRequestDto request,
        CancellationToken cancellationToken = default)
        => _operationExecutor.ExecuteAsync(
            "ai.grade-submission",
            ct => GradeInternalAsync(request, ct),
            cancellationToken);

    private Task<AiGradeSubmissionResponseDto> GradeInternalAsync(
        AiGradeSubmissionRequestDto request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (!string.Equals(_options.Provider, "Mock", StringComparison.OrdinalIgnoreCase))
        {
            throw new InfrastructureException(
                "AI_PROVIDER_UNSUPPORTED",
                $"AI provider '{_options.Provider}' is not supported by the current adapter. Configure AI.Provider='Mock' or implement another provider.");
        }

        var quality = EstimateQualityFactor(request.SourceCode);
        var scores = request.Criteria
            .Select(criteria =>
            {
                var maxScore = criteria.Weight > 0 ? criteria.Weight : 1.0;
                var score = Math.Round(maxScore * quality, 2);

                return new AiRubricScoreDto(
                    criteria.Name,
                    score,
                    BuildComment(criteria.Name, quality));
            })
            .ToList();

        var totalScore = scores.Sum(s => s.Score);
        var feedback = new AiFeedbackDto(
            BuildSummary(quality),
            BuildSuggestions(request.SourceCode));

        var response = new AiGradeSubmissionResponseDto(
            request.SubmissionId,
            totalScore,
            scores,
            feedback);

        return Task.FromResult(response);
    }

    private static double EstimateQualityFactor(string sourceCode)
    {
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            return 0;
        }

        var lines = sourceCode.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var lineScore = Math.Min(1.0, lines.Length / 40.0);

        var hasStructure =
            sourceCode.Contains("class ", StringComparison.Ordinal) ||
            sourceCode.Contains("def ", StringComparison.Ordinal) ||
            sourceCode.Contains("function ", StringComparison.Ordinal);
        var structureScore = hasStructure ? 0.2 : 0;

        var hasErrorHandling =
            sourceCode.Contains("try", StringComparison.OrdinalIgnoreCase) ||
            sourceCode.Contains("catch", StringComparison.OrdinalIgnoreCase) ||
            sourceCode.Contains("except", StringComparison.OrdinalIgnoreCase);
        var safetyScore = hasErrorHandling ? 0.15 : 0;

        var hasComments =
            sourceCode.Contains("//", StringComparison.Ordinal) ||
            sourceCode.Contains("/*", StringComparison.Ordinal) ||
            sourceCode.Contains("#", StringComparison.Ordinal);
        var readabilityScore = hasComments ? 0.15 : 0;

        var quality = lineScore * 0.5 + structureScore + safetyScore + readabilityScore;
        return Math.Clamp(quality, 0.1, 1.0);
    }

    private static string BuildComment(string criteriaName, double quality)
    {
        if (quality >= 0.85)
        {
            return $"{criteriaName}: implementation is strong and mostly complete.";
        }

        if (quality >= 0.6)
        {
            return $"{criteriaName}: implementation is acceptable with room for refinement.";
        }

        return $"{criteriaName}: implementation needs more structure and validation.";
    }

    private static string BuildSummary(double quality)
    {
        if (quality >= 0.85)
        {
            return "The submission demonstrates good structure, reasonable safety checks, and clear coding direction.";
        }

        if (quality >= 0.6)
        {
            return "The submission is workable but should improve consistency and robustness.";
        }

        return "The submission appears incomplete and should be revised for correctness and maintainability.";
    }

    private static IReadOnlyList<string> BuildSuggestions(string sourceCode)
    {
        var suggestions = new List<string>();

        if (!sourceCode.Contains("try", StringComparison.OrdinalIgnoreCase) &&
            !sourceCode.Contains("catch", StringComparison.OrdinalIgnoreCase))
        {
            suggestions.Add("Add error handling paths for edge cases and unexpected inputs.");
        }

        if (!sourceCode.Contains("//", StringComparison.Ordinal) &&
            !sourceCode.Contains("/*", StringComparison.Ordinal))
        {
            suggestions.Add("Add concise comments around non-trivial logic to improve readability.");
        }

        if (!sourceCode.Contains("return", StringComparison.OrdinalIgnoreCase))
        {
            suggestions.Add("Make output behavior explicit and predictable.");
        }

        if (suggestions.Count == 0)
        {
            suggestions.Add("Keep consistency in naming, decomposition, and test coverage.");
        }

        return suggestions;
    }
}