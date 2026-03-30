using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ports.DTO.Common;
using Ports.DTO.Classroom;
using Ports.DTO.Report;
using Ports.DTO.Submission;

namespace Ports.InBoundPorts.Query;

public interface IGetSubmissionDetailUseCase
{
    Task<SubmissionDetailDto?> HandleAsync(
        Guid submissionId,
        CancellationToken cancellationToken = default);
}

public interface IGetAuthorizedSubmissionDetailUseCase
{
    Task<AuthorizedSubmissionDetailResponseDto> HandleAsync(
        Guid submissionId,
        Guid actorUserId,
        UserRoleDto actorRole,
        CancellationToken cancellationToken = default);
}

public interface IGetScoreboardUseCase
{
    Task<IReadOnlyList<ScoreboardItemDto>> HandleAsync(
        Guid classroomId,
    Guid? assignmentId = null,
        CancellationToken cancellationToken = default);
}

public interface IGetSubmissionHistoryUseCase
{
    Task<PagedResponseDto<SubmissionDetailDto>> HandleAsync(
        Guid studentId,
        PagedRequestDto request,
        CancellationToken cancellationToken = default);
}

public interface ICheckClassroomAccessUseCase
{
    Task<ResourceAccessDecisionDto> HandleAsync(
        Guid classroomId,
        Guid actorUserId,
        UserRoleDto actorRole,
        CancellationToken cancellationToken = default);
}

public interface IGetAuthorizedClassroomOverviewUseCase
{
    Task<AuthorizedClassroomOverviewResponseDto> HandleAsync(
        Guid classroomId,
        Guid actorUserId,
        UserRoleDto actorRole,
        CancellationToken cancellationToken = default);
}
