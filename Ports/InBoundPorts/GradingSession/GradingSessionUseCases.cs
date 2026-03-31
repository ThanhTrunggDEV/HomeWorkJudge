using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ports.DTO.GradingSession;
using Ports.DTO.Submission;

namespace Ports.InBoundPorts.GradingSession;

public interface IGradingSessionUseCase
{
    Task<CreateSessionResult> CreateAsync(CreateSessionCommand command, CancellationToken ct = default);
    Task<IReadOnlyList<GradingSessionSummaryDto>> GetAllAsync(CancellationToken ct = default);
    Task<SessionStatisticsDto> GetStatisticsAsync(Guid sessionId, CancellationToken ct = default);
    Task DeleteAsync(Guid sessionId, CancellationToken ct = default);
}
