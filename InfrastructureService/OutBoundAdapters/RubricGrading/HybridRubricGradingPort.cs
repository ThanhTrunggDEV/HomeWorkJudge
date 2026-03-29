using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using InfrastructureService.Configuration.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ports.DTO.AI;
using Ports.DTO.Rubric;
using Ports.OutBoundPorts.AI;
using Ports.OutBoundPorts.RubricGrading;

namespace InfrastructureService.OutBoundAdapters.RubricGrading;

public sealed class HybridRubricGradingPort : IRubricGradingPort
{
    private readonly IAiGradingPort _aiGradingPort;
    private readonly RubricOptions _options;
    private readonly ILogger<HybridRubricGradingPort> _logger;

    public HybridRubricGradingPort(
        IAiGradingPort aiGradingPort,
        IOptions<RubricOptions> options,
        ILogger<HybridRubricGradingPort> logger)
    {
        _aiGradingPort = aiGradingPort;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RubricScoreDto>> GradeByRubricAsync(
        string sourceCode,
        IReadOnlyList<RubricCriteriaDto> criteria,
        CancellationToken cancellationToken = default)
    {
        if (criteria is null)
        {
            throw new ArgumentNullException(nameof(criteria));
        }

        var baselineScores = GradeRuleBased(sourceCode, criteria);

        var mode = _options.Mode?.Trim() ?? "Hybrid";
        var canUseAi =
            _options.AI.Enabled &&
            string.Equals(mode, "Hybrid", StringComparison.OrdinalIgnoreCase) &&
            criteria.Count > 0;

        if (!canUseAi)
        {
            return baselineScores;
        }

        try
        {
            var aiRequest = new AiGradeSubmissionRequestDto(
                Guid.Empty,
                "Rubric Evaluation",
                "Local rubric grading",
                sourceCode,
                "text",
                criteria);

            var aiResponse = await _aiGradingPort.GradeSubmissionAsync(aiRequest, cancellationToken);
            return MergeWithAiAdjustments(criteria, baselineScores, aiResponse.Scores);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI augmentation for rubric grading failed. Falling back to rule-based baseline.");
            return baselineScores;
        }
    }

    private static IReadOnlyList<RubricScoreDto> GradeRuleBased(
        string sourceCode,
        IReadOnlyList<RubricCriteriaDto> criteria)
    {
        var qualityFactor = EstimateQualityFactor(sourceCode);

        return criteria
            .Select(item =>
            {
                var weight = item.Weight > 0 ? item.Weight : 1.0;
                var score = Math.Round(weight * qualityFactor, 2);

                return new RubricScoreDto(
                    item.Name,
                    score,
                    BuildBaselineComment(item.Name, qualityFactor));
            })
            .ToList();
    }

    private IReadOnlyList<RubricScoreDto> MergeWithAiAdjustments(
        IReadOnlyList<RubricCriteriaDto> criteria,
        IReadOnlyList<RubricScoreDto> baseline,
        IReadOnlyList<AiRubricScoreDto> aiScores)
    {
        var aiByCriteria = aiScores.ToDictionary(s => s.CriteriaName, StringComparer.OrdinalIgnoreCase);
        var maxAdjustmentPercent = Math.Max(0, _options.AI.MaxAdjustmentPercent);

        var merged = new List<RubricScoreDto>(baseline.Count);
        for (var i = 0; i < baseline.Count; i++)
        {
            var baseScore = baseline[i];
            var criterion = criteria[i];

            if (!aiByCriteria.TryGetValue(baseScore.CriteriaName, out var aiScore))
            {
                merged.Add(baseScore);
                continue;
            }

            var maxReference = criterion.Weight > 0 ? criterion.Weight : Math.Max(baseScore.GivenScore, 1.0);
            var allowedDelta = maxReference * maxAdjustmentPercent;
            var requestedDelta = aiScore.Score - baseScore.GivenScore;
            var appliedDelta = Math.Clamp(requestedDelta, -allowedDelta, allowedDelta);
            var finalScore = Math.Round(Math.Max(0, baseScore.GivenScore + appliedDelta), 2);

            merged.Add(new RubricScoreDto(
                baseScore.CriteriaName,
                finalScore,
                $"{baseScore.CommentReason} AI note: {aiScore.Comment}"));
        }

        return merged;
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

        var hasValidation =
            sourceCode.Contains("if", StringComparison.OrdinalIgnoreCase) ||
            sourceCode.Contains("switch", StringComparison.OrdinalIgnoreCase);
        var validationScore = hasValidation ? 0.15 : 0;

        var quality = lineScore * 0.6 + structureScore + validationScore;
        return Math.Clamp(quality, 0.1, 1.0);
    }

    private static string BuildBaselineComment(string criteriaName, double qualityFactor)
    {
        if (qualityFactor >= 0.8)
        {
            return $"{criteriaName}: baseline rule checks passed with strong quality signals.";
        }

        if (qualityFactor >= 0.55)
        {
            return $"{criteriaName}: baseline rule checks are acceptable but can be improved.";
        }

        return $"{criteriaName}: baseline rule checks indicate insufficient structure.";
    }
}