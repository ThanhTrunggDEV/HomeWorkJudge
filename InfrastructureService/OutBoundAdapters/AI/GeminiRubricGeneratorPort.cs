using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using InfrastructureService.Common.Resilience;
using InfrastructureService.Configuration.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ports.DTO.Rubric;
using Ports.OutBoundPorts.AI;

namespace InfrastructureService.OutBoundAdapters.AI;

/// <summary>
/// Generates rubric criteria suggestions via Gemini.
/// Reuses HTTP/parsing logic from GeminiGradingPort.
/// </summary>
public sealed class GeminiRubricGeneratorPort : IAiRubricGeneratorPort
{
    private readonly GeminiGradingPort _gemini;
    private readonly IOptions<AiOptions> _options;

    public GeminiRubricGeneratorPort(GeminiGradingPort gemini, IOptions<AiOptions> options)
    {
        _gemini  = gemini;
        _options = options;
    }

    public Task<IReadOnlyList<RubricCriteriaDto>> GenerateAsync(
        string assignmentDescription,
        CancellationToken ct = default)
        => _gemini.GenerateRubricAsync(assignmentDescription, _options, ct);
}
