using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
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

public sealed class OpenAiGradingPort : IAiGradingPort
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly IOperationExecutor _operationExecutor;
    private readonly AiOptions _options;
    private readonly ILogger<OpenAiGradingPort> _logger;

    public OpenAiGradingPort(
        HttpClient httpClient,
        IOperationExecutor operationExecutor,
        IOptions<AiOptions> options,
        ILogger<OpenAiGradingPort> logger)
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
            "ai.openai.grade-submission",
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

        if (!string.Equals(_options.Provider, "OpenAI", StringComparison.OrdinalIgnoreCase))
        {
            throw new InfrastructureException(
                "AI_PROVIDER_UNSUPPORTED",
                $"AI provider '{_options.Provider}' is not supported. Supported values: OpenAI.");
        }

        var providerOptions = _options.OpenAI;

        if (string.IsNullOrWhiteSpace(providerOptions.ApiKey))
        {
            throw new InfrastructureException("AI_CONFIGURATION_INVALID", "OpenAI ApiKey is missing.");
        }

        if (string.IsNullOrWhiteSpace(providerOptions.Model))
        {
            throw new InfrastructureException("AI_CONFIGURATION_INVALID", "OpenAI Model is missing.");
        }

        var uri = BuildChatCompletionsUri(providerOptions.BaseUrl);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, uri);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", providerOptions.ApiKey);

        var payload = BuildChatCompletionsPayload(request, providerOptions.Model, providerOptions.Temperature);
        httpRequest.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var requestId = TryGetHeader(response, "x-request-id") ?? "n/a";
            _logger.LogError(
                "OpenAI request failed with status {StatusCode}. RequestId={RequestId}. ResponseLength={ResponseLength}.",
                (int)response.StatusCode,
                requestId,
                responseContent.Length);

            throw new InfrastructureException(
                "AI_PROVIDER_REQUEST_FAILED",
                $"OpenAI request failed with status {(int)response.StatusCode}.");
        }

        try
        {
            var modelJson = ExtractModelJson(responseContent);
            return ParseGradeResponse(request, modelJson);
        }
        catch (Exception ex) when (ex is not InfrastructureException)
        {
            _logger.LogError(ex, "Failed to parse OpenAI grading response.");
            throw new InfrastructureException(
                "AI_RESPONSE_INVALID",
                "OpenAI response payload is invalid for grading result.",
                ex);
        }
    }

    private static Uri BuildChatCompletionsUri(string baseUrl)
    {
        var normalizedBaseUrl = string.IsNullOrWhiteSpace(baseUrl)
            ? "https://api.openai.com"
            : baseUrl.Trim().TrimEnd('/');

        if (!Uri.TryCreate(normalizedBaseUrl + "/v1/chat/completions", UriKind.Absolute, out var uri))
        {
            throw new InfrastructureException("AI_CONFIGURATION_INVALID", "OpenAI BaseUrl is invalid.");
        }

        return uri;
    }

    private static string BuildChatCompletionsPayload(
        AiGradeSubmissionRequestDto request,
        string model,
        double temperature)
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
            model,
            temperature = Math.Clamp(temperature, 0, 2),
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            }
        };

        return JsonSerializer.Serialize(body, JsonOptions);
    }

    private static string ExtractModelJson(string openAiResponseJson)
    {
        using var document = JsonDocument.Parse(openAiResponseJson);

        if (!document.RootElement.TryGetProperty("choices", out var choices) ||
            choices.ValueKind != JsonValueKind.Array ||
            choices.GetArrayLength() == 0)
        {
            throw new InfrastructureException("AI_RESPONSE_INVALID", "OpenAI response does not contain choices.");
        }

        foreach (var choice in choices.EnumerateArray())
        {
            if (!choice.TryGetProperty("message", out var message))
            {
                continue;
            }

            var content = TryExtractMessageContent(message);
            if (!string.IsNullOrWhiteSpace(content))
            {
                return StripCodeFence(content.Trim());
            }
        }

        throw new InfrastructureException("AI_RESPONSE_INVALID", "OpenAI response does not contain readable assistant content.");
    }

    private static string? TryExtractMessageContent(JsonElement message)
    {
        if (!message.TryGetProperty("content", out var contentElement))
        {
            return null;
        }

        if (contentElement.ValueKind == JsonValueKind.String)
        {
            return contentElement.GetString();
        }

        if (contentElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var parts = new List<string>();
        foreach (var item in contentElement.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var textValue = item.GetString();
                if (!string.IsNullOrWhiteSpace(textValue))
                {
                    parts.Add(textValue);
                }

                continue;
            }

            if (item.ValueKind == JsonValueKind.Object &&
                item.TryGetProperty("text", out var textElement) &&
                textElement.ValueKind == JsonValueKind.String)
            {
                var textValue = textElement.GetString();
                if (!string.IsNullOrWhiteSpace(textValue))
                {
                    parts.Add(textValue);
                }
            }
        }

        return parts.Count == 0 ? null : string.Join("\n", parts);
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
