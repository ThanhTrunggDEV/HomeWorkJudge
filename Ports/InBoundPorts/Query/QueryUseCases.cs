using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ports.DTO.Common;
using Ports.DTO.Report;
using Ports.DTO.Submission;

namespace Ports.InBoundPorts.Query;

public interface IGetSubmissionDetailUseCase
{
    Task<SubmissionDetailDto?> HandleAsync(
        Guid submissionId,
        CancellationToken cancellationToken = default);
}

public interface IGetScoreboardUseCase
{
    Task<IReadOnlyList<ScoreboardItemDto>> HandleAsync(
        Guid classroomId,
        CancellationToken cancellationToken = default);
}

public interface IGetSubmissionHistoryUseCase
{
    Task<PagedResponseDto<SubmissionDetailDto>> HandleAsync(
        Guid studentId,
        PagedRequestDto request,
        CancellationToken cancellationToken = default);
}
