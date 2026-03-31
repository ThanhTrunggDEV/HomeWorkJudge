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
using Ports.DTO.Rubric;
using Ports.DTO.Submission;
using Ports.OutBoundPorts.AI;

namespace InfrastructureService.OutBoundAdapters.AI;

/// <summary>
/// Implements IAiGradingPort via Google Gemini API.
/// Gửi source code files + rubric criteria → nhận điểm từng tiêu chí.
/// </summary>
public sealed class GeminiGradingPort : IAiGradingPort
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly HttpClient _httpClient;
    private readonly IOperationExecutor _executor;
    private readonly AiOptions _options;
    private readonly ILogger<GeminiGradingPort> _logger;

    public GeminiGradingPort(
        HttpClient httpClient,
        IOperationExecutor executor,
        IOptions<AiOptions> options,
        ILogger<GeminiGradingPort> logger)
    {
        _httpClient = httpClient;
        _executor   = executor;
        _options    = options.Value;
        _logger     = logger;
    }

    public Task<IReadOnlyList<RubricScoreDto>> GradeAsync(
        IReadOnlyList<SourceFileDto> sourceFiles,
        IReadOnlyList<RubricCriteriaDto> criteria,
        CancellationToken ct = default)
        => _executor.ExecuteAsync(
            "ai.gemini.grade",
            token => GradeInternalAsync(sourceFiles, criteria, token),
            ct);

    private async Task<IReadOnlyList<RubricScoreDto>> GradeInternalAsync(
        IReadOnlyList<SourceFileDto> sourceFiles,
        IReadOnlyList<RubricCriteriaDto> criteria,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var gemini = ValidateAndGetOptions();
        var uri    = BuildUri(gemini);
        var prompt = BuildGradingPrompt(sourceFiles, criteria);
        var body   = BuildRequestBody(prompt, gemini.Temperature);

        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json")
        };

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        var content = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Gemini grading failed {Status}: {Content}", (int)response.StatusCode, content[..Math.Min(200, content.Length)]);
            throw new InfrastructureException("AI_PROVIDER_REQUEST_FAILED", $"Gemini returned HTTP {(int)response.StatusCode}.");
        }

        var modelJson = ExtractText(content);
        return ParseScores(modelJson, criteria);
    }

    // ── IAiRubricGeneratorPort is handled separately, but reuse HTTP helpers ──

    internal async Task<IReadOnlyList<RubricCriteriaDto>> GenerateRubricAsync(
        string assignmentDescription,
        IOptions<AiOptions> options,
        CancellationToken ct)
    {
        var gemini = options.Value.Gemini;
        var uri    = BuildUri(gemini);
        var prompt = BuildRubricPrompt(assignmentDescription);
        var body   = BuildRequestBody(prompt, gemini.Temperature);

        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json")
        };

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        var content = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new InfrastructureException("AI_PROVIDER_REQUEST_FAILED", $"Gemini returned HTTP {(int)response.StatusCode}.");

        var modelJson = ExtractText(content);
        return ParseCriteria(modelJson);
    }

    // ── Prompt builders ──────────────────────────────────────────────────────

    private static string BuildGradingPrompt(IReadOnlyList<SourceFileDto> files, IReadOnlyList<RubricCriteriaDto> criteria)
    {
        var filesText  = string.Join("\n\n", files.Select(f => $"=== {f.FileName} ===\n{f.Content}"));
        var rubricText = string.Join("\n", criteria.Select(c =>
            $"- {c.Name} (maxScore={c.MaxScore.ToString(CultureInfo.InvariantCulture)}): {c.Description}"));

        return "You are an objective programming assignment grader.\n" +
               "Return strictly valid JSON only, no markdown.\n" +
               "Schema: {\"scores\":[{\"criteriaName\":string,\"givenScore\":number,\"comment\":string}]}\n" +
               "Score each criterion in range [0, maxScore].\n\n" +
               "Rubric criteria:\n" + rubricText + "\n\n" +
               "Source files:\n" + filesText;
    }

    private static string BuildRubricPrompt(string assignmentDescription)
        => "You are an academic rubric designer for programming assignments.\n" +
           "Return strictly valid JSON only, no markdown.\n" +
           "Schema: {\"criteria\":[{\"name\":string,\"maxScore\":number,\"description\":string}]}\n" +
           "Create 4-7 criteria. Total maxScore should be 10.\n\n" +
           "Assignment description:\n" + assignmentDescription;

    // ── Request/response helpers ─────────────────────────────────────────────

    private GeminiProviderOptions ValidateAndGetOptions()
    {
        var gemini = _options.Gemini;
        if (string.IsNullOrWhiteSpace(gemini.ApiKey))
            throw new InfrastructureException("AI_CONFIGURATION_INVALID", "Gemini ApiKey is missing.");
        if (string.IsNullOrWhiteSpace(gemini.Model))
            throw new InfrastructureException("AI_CONFIGURATION_INVALID", "Gemini Model is missing.");
        return gemini;
    }

    private static Uri BuildUri(GeminiProviderOptions opts)
    {
        var baseUrl = string.IsNullOrWhiteSpace(opts.BaseUrl)
            ? "https://generativelanguage.googleapis.com"
            : opts.BaseUrl.TrimEnd('/');
        var url = $"{baseUrl}/v1beta/models/{Uri.EscapeDataString(opts.Model)}:generateContent?key={Uri.EscapeDataString(opts.ApiKey)}";
        return new Uri(url);
    }

    private static object BuildRequestBody(string prompt, double temperature) => new
    {
        contents = new[] { new { role = "user", parts = new[] { new { text = prompt } } } },
        generationConfig = new { temperature = Math.Clamp(temperature, 0, 2), responseMimeType = "application/json" }
    };

    private static string ExtractText(string geminiResponseJson)
    {
        using var doc = JsonDocument.Parse(geminiResponseJson);
        var candidates = doc.RootElement.GetProperty("candidates");
        foreach (var c in candidates.EnumerateArray())
            if (c.TryGetProperty("content", out var content) &&
                content.TryGetProperty("parts", out var parts))
                foreach (var p in parts.EnumerateArray())
                    if (p.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String)
                        return StripFence(t.GetString()!.Trim());

        throw new InfrastructureException("AI_RESPONSE_INVALID", "Gemini response has no text.");
    }

    private static string StripFence(string text)
    {
        if (!text.StartsWith("```", StringComparison.Ordinal)) return text;
        var nl = text.IndexOf('\n');
        var last = text.LastIndexOf("```", StringComparison.Ordinal);
        return (nl >= 0 && last > nl) ? text[(nl + 1)..last].Trim() : text;
    }

    private static IReadOnlyList<RubricScoreDto> ParseScores(string json, IReadOnlyList<RubricCriteriaDto> criteria)
    {
        using var doc = JsonDocument.Parse(json);
        var map = criteria.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
        var result = new List<RubricScoreDto>(criteria.Count);

        if (doc.RootElement.TryGetProperty("scores", out var scores))
            foreach (var s in scores.EnumerateArray())
            {
                var name    = s.TryGetProperty("criteriaName", out var n) ? n.GetString() ?? "" : "";
                var score   = s.TryGetProperty("givenScore",   out var sv) && sv.TryGetDouble(out var d) ? d : 0;
                var comment = s.TryGetProperty("comment",      out var cm) ? cm.GetString() ?? "" : "";

                // Chỉ chấp nhận tiêu chí thuộc rubric — bỏ qua hallucinated criteria
                if (!map.TryGetValue(name, out var c))
                    continue;

                score = Math.Clamp(score, 0, c.MaxScore);
                result.Add(new RubricScoreDto(name, Math.Round(score, 2), c.MaxScore, comment));
            }

        // Fill missing criteria with 0
        var filled = result.Select(r => r.CriteriaName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var c in criteria.Where(c => !filled.Contains(c.Name)))
            result.Add(new RubricScoreDto(c.Name, 0, c.MaxScore, "AI did not evaluate this criterion."));

        return result;
    }

    private static IReadOnlyList<RubricCriteriaDto> ParseCriteria(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var result = new List<RubricCriteriaDto>();

        if (doc.RootElement.TryGetProperty("criteria", out var arr))
            foreach (var c in arr.EnumerateArray())
            {
                var name  = c.TryGetProperty("name",        out var n)  ? n.GetString()  ?? "" : "";
                var score = c.TryGetProperty("maxScore",    out var sv) && sv.TryGetDouble(out var d) ? d : 1;
                var desc  = c.TryGetProperty("description", out var de) ? de.GetString() ?? "" : "";
                if (!string.IsNullOrWhiteSpace(name))
                    result.Add(new RubricCriteriaDto(Guid.Empty, name, score, desc));
            }

        return result;
    }
}
