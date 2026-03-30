using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ports.DTO.Assignment;
using Ports.DTO.Common;

namespace Ports.InBoundPorts.Assignment;

public interface ICreateAssignmentUseCase
{
    Task<CreateAssignmentResponseDto> HandleAsync(
        CreateAssignmentRequestDto request,
        CancellationToken cancellationToken = default);
}

public interface IUpdateAssignmentUseCase
{
    Task HandleAsync(
        UpdateAssignmentRequestDto request,
        CancellationToken cancellationToken = default);
}

public interface IListAssignmentsUseCase
{
    Task<PagedResponseDto<AssignmentListItemDto>> HandleAsync(
        Guid classroomId,
    Guid requestedByUserId,
        PagedRequestDto request,
        CancellationToken cancellationToken = default);
}

public interface IPublishAssignmentUseCase
{
    Task HandleAsync(
        PublishAssignmentRequestDto request,
        CancellationToken cancellationToken = default);
}

public interface IAddAssignmentTestCaseUseCase
{
    Task<AssignmentTestCaseDto> HandleAsync(
        AddAssignmentTestCaseRequestDto request,
        CancellationToken cancellationToken = default);
}

public interface IUpdateAssignmentTestCaseUseCase
{
    Task<AssignmentTestCaseDto> HandleAsync(
        UpdateAssignmentTestCaseRequestDto request,
        CancellationToken cancellationToken = default);
}

public interface IDeleteAssignmentTestCaseUseCase
{
    Task HandleAsync(
        DeleteAssignmentTestCaseRequestDto request,
        CancellationToken cancellationToken = default);
}

public interface ICreateAssignmentRubricUseCase
{
    Task HandleAsync(
        CreateAssignmentRubricRequestDto request,
        CancellationToken cancellationToken = default);
}

public interface IUpdateAssignmentRubricUseCase
{
    Task HandleAsync(
        UpdateAssignmentRubricRequestDto request,
        CancellationToken cancellationToken = default);
}

public interface IRejudgeAssignmentUseCase
{
    Task HandleAsync(Guid assignmentId, CancellationToken cancellationToken = default);
}

public interface IGetAssignmentDetailUseCase
{
    Task<AssignmentDetailDto> HandleAsync(
        Guid assignmentId,
        Guid requestedByUserId,
        CancellationToken cancellationToken = default);
}
