using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using InfrastructureService.Common.Errors;
using InfrastructureService.Common.Resilience;
using InfrastructureService.Configuration.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ports.DTO.AI;
using Ports.DTO.Rubric;
using Ports.OutBoundPorts.AI;

namespace InfrastructureService.OutBoundAdapters.AI;

public sealed class GeminiGradingPort : IAiGradingPort
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly IOperationExecutor _operationExecutor;
    private readonly AiOptions _options;
    private readonly ILogger<GeminiGradingPort> _logger;

    public GeminiGradingPort(
        HttpClient httpClient,
        IOperationExecutor operationExecutor,
        IOptions<AiOptions> options,
        ILogger<GeminiGradingPort> logger)
    {
        _httpClient = httpClient;
        _operationExecutor = operationExecutor;
        _options = options.Value;
        _logger = logger;
    }

    public Task<AiGradeSubmissionResponseDto> GradeSubmissionAsync(
        AiGradeSubmissionRequestDto request,
        CancellationToken cancellationToken = default)
        => _operationExecutor.ExecuteAsync(
            "ai.gemini.grade-submission",
            ct => GradeInternalAsync(request, ct),
            cancellationToken);

    private async Task<AiGradeSubmissionResponseDto> GradeInternalAsync(
        AiGradeSubmissionRequestDto request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (!string.Equals(_options.Provider, "Gemini", StringComparison.OrdinalIgnoreCase))
        {
            throw new InfrastructureException(
                "AI_PROVIDER_UNSUPPORTED",
                $"AI provider '{_options.Provider}' is not supported. Supported values: Gemini.");
        }

        var providerOptions = _options.Gemini;

        if (string.IsNullOrWhiteSpace(providerOptions.ApiKey))
        {
            throw new InfrastructureException("AI_CONFIGURATION_INVALID", "Gemini ApiKey is missing.");
        }

        if (string.IsNullOrWhiteSpace(providerOptions.Model))
        {
            throw new InfrastructureException("AI_CONFIGURATION_INVALID", "Gemini Model is missing.");
        }

        var uri = BuildGeminiUri(providerOptions.BaseUrl, providerOptions.Model, providerOptions.ApiKey);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, uri);

        var payload = BuildGeminiPayload(request, providerOptions.Temperature);
        httpRequest.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var responseId = TryGetHeader(response, "x-request-id") ?? "n/a";
            _logger.LogError(
                "Gemini request failed with status {StatusCode}. RequestId={RequestId}. ResponseLength={ResponseLength}.",
                (int)response.StatusCode,
                responseId,
                responseContent.Length);

            throw new InfrastructureException(
                "AI_PROVIDER_REQUEST_FAILED",
                $"Gemini request failed with status {(int)response.StatusCode}.");
        }

        try
        {
            var modelJson = ExtractModelJson(responseContent);
            return ParseGradeResponse(request, modelJson);
        }
        catch (Exception ex) when (ex is not InfrastructureException)
        {
            _logger.LogError(ex, "Failed to parse Gemini grading response.");
            throw new InfrastructureException(
                "AI_RESPONSE_INVALID",
                "Gemini response payload is invalid for grading result.",
                ex);
        }
    }

    private static Uri BuildGeminiUri(string baseUrl, string model, string apiKey)
    {
        var normalizedBaseUrl = string.IsNullOrWhiteSpace(baseUrl)
            ? "https://generativelanguage.googleapis.com"
            : baseUrl.Trim().TrimEnd('/');

        var encodedModel = Uri.EscapeDataString(model.Trim());
        var encodedKey = Uri.EscapeDataString(apiKey.Trim());
        var fullUrl = $"{normalizedBaseUrl}/v1beta/models/{encodedModel}:generateContent?key={encodedKey}";

        if (!Uri.TryCreate(fullUrl, UriKind.Absolute, out var uri))
        {
            throw new InfrastructureException("AI_CONFIGURATION_INVALID", "Gemini BaseUrl or Model is invalid.");
        }

        return uri;
    }

    private static string BuildGeminiPayload(AiGradeSubmissionRequestDto request, double temperature)
    {
        var systemPrompt =
            "You are an objective programming assignment grader. " +
            "Return strictly valid JSON only, no markdown. " +
            "Schema: {\"scores\":[{\"criteriaName\":string,\"score\":number,\"comment\":string}],\"feedback\":{\"summary\":string,\"suggestions\":[string]}}. " +
            "Score each criterion in range [0, criterion.weight].";

        var rubricText = string.Join(
            "\n",
            request.Criteria.Select(c => $"- {c.Name} (weight={c.Weight.ToString(CultureInfo.InvariantCulture)}): {c.Description}"));

        var userPrompt =
            "Evaluate this submission with the rubric and produce JSON.\n" +
            $"Assignment title: {request.AssignmentTitle}\n" +
            $"Assignment description: {request.AssignmentDescription}\n" +
            $"Language: {request.Language}\n" +
            "Rubric criteria:\n" + rubricText + "\n" +
            "Source code:\n" + request.SourceCode;

        var body = new
        {
            systemInstruction = new
            {
                parts = new[] { new { text = systemPrompt } }
            },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = userPrompt } }
                }
            },
            generationConfig = new
            {
                temperature = Math.Clamp(temperature, 0, 2),
                responseMimeType = "application/json"
            }
        };

        return JsonSerializer.Serialize(body, JsonOptions);
    }

    private static string ExtractModelJson(string geminiResponseJson)
    {
        using var document = JsonDocument.Parse(geminiResponseJson);

        if (!document.RootElement.TryGetProperty("candidates", out var candidates) ||
            candidates.ValueKind != JsonValueKind.Array ||
            candidates.GetArrayLength() == 0)
        {
            throw new InfrastructureException("AI_RESPONSE_INVALID", "Gemini response does not contain candidates.");
        }

        foreach (var candidate in candidates.EnumerateArray())
        {
            var text = TryExtractCandidateText(candidate);
            if (!string.IsNullOrWhiteSpace(text))
            {
                return StripCodeFence(text.Trim());
            }
        }

        throw new InfrastructureException("AI_RESPONSE_INVALID", "Gemini response does not contain readable text output.");
    }

    private static string? TryExtractCandidateText(JsonElement candidate)
    {
        if (!candidate.TryGetProperty("content", out var content) ||
            !content.TryGetProperty("parts", out var parts) ||
            parts.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var part in parts.EnumerateArray())
        {
            if (part.ValueKind == JsonValueKind.Object &&
                part.TryGetProperty("text", out var textElement) &&
                textElement.ValueKind == JsonValueKind.String)
            {
                var text = textElement.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }

        return null;
    }

    private static string? TryGetHeader(HttpResponseMessage response, string headerName)
    {
        if (response.Headers.TryGetValues(headerName, out var values))
        {
            return values.FirstOrDefault();
        }

        if (response.Content.Headers.TryGetValues(headerName, out values))
        {
            return values.FirstOrDefault();
        }

        return null;
    }

    private static string StripCodeFence(string text)
    {
        if (!text.StartsWith("```", StringComparison.Ordinal))
        {
            return text;
        }

        var firstNewLine = text.IndexOf('\n');
        var lastFence = text.LastIndexOf("```", StringComparison.Ordinal);

        if (firstNewLine < 0 || lastFence <= firstNewLine)
        {
            return text;
        }

        return text.Substring(firstNewLine + 1, lastFence - firstNewLine - 1).Trim();
    }

    private static AiGradeSubmissionResponseDto ParseGradeResponse(
        AiGradeSubmissionRequestDto request,
        string gradeJson)
    {
        using var document = JsonDocument.Parse(gradeJson);
        var root = document.RootElement;

        var aiScores = ParseScores(root, request.Criteria);
        var feedback = ParseFeedback(root);

        var totalScore = Math.Round(aiScores.Sum(x => x.Score), 2);

        return new AiGradeSubmissionResponseDto(
            request.SubmissionId,
            totalScore,
            aiScores,
            feedback);
    }

    private static IReadOnlyList<AiRubricScoreDto> ParseScores(JsonElement root, IReadOnlyList<RubricCriteriaDto> criteria)
    {
        var criteriaMap = criteria.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
        var scoreMap = new Dictionary<string, AiRubricScoreDto>(StringComparer.OrdinalIgnoreCase);

        if (root.TryGetProperty("scores", out var scoresElement) && scoresElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in scoresElement.EnumerateArray())
            {
                if (!item.TryGetProperty("criteriaName", out var nameElement) || nameElement.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var criteriaName = nameElement.GetString();
                if (string.IsNullOrWhiteSpace(criteriaName))
                {
                    continue;
                }

                var score = item.TryGetProperty("score", out var scoreElement) && scoreElement.TryGetDouble(out var parsedScore)
                    ? parsedScore
                    : 0;
                var comment = item.TryGetProperty("comment", out var commentElement) && commentElement.ValueKind == JsonValueKind.String
                    ? commentElement.GetString() ?? string.Empty
                    : string.Empty;

                if (criteriaMap.TryGetValue(criteriaName, out var criterion))
                {
                    score = Math.Clamp(score, 0, criterion.Weight);
                }

                scoreMap[criteriaName] = new AiRubricScoreDto(criteriaName, Math.Round(score, 2), comment);
            }
        }

        var ordered = new List<AiRubricScoreDto>(criteria.Count);
        foreach (var criterion in criteria)
        {
            if (scoreMap.TryGetValue(criterion.Name, out var existing))
            {
                ordered.Add(existing);
                continue;
            }

            ordered.Add(new AiRubricScoreDto(criterion.Name, 0, "No AI evaluation generated for this criterion."));
        }

        return ordered;
    }

    private static AiFeedbackDto ParseFeedback(JsonElement root)
    {
        if (!root.TryGetProperty("feedback", out var feedbackElement) || feedbackElement.ValueKind != JsonValueKind.Object)
        {
            return new AiFeedbackDto(
                "AI feedback is unavailable.",
                new[] { "Review rubric criteria manually." });
        }

        var summary = feedbackElement.TryGetProperty("summary", out var summaryElement) && summaryElement.ValueKind == JsonValueKind.String
            ? summaryElement.GetString() ?? "AI feedback is unavailable."
            : "AI feedback is unavailable.";

        var suggestions = new List<string>();
        if (feedbackElement.TryGetProperty("suggestions", out var suggestionsElement) && suggestionsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var suggestion in suggestionsElement.EnumerateArray())
            {
                if (suggestion.ValueKind == JsonValueKind.String)
                {
                    var value = suggestion.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        suggestions.Add(value);
                    }
                }
            }
        }

        if (suggestions.Count == 0)
        {
            suggestions.Add("Review rubric criteria manually.");
        }

        return new AiFeedbackDto(summary, suggestions);
    }
}
