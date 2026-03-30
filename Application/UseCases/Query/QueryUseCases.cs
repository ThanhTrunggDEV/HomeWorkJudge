using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Common;
using Domain.Ports;
using Domain.ValueObject;
using Ports.DTO.Assignment;
using Ports.DTO.Classroom;
using Ports.DTO.Common;
using Ports.DTO.Report;
using Ports.DTO.Submission;
using Ports.InBoundPorts.Query;

namespace Application.UseCases.Query;

public sealed class GetSubmissionDetailUseCase : IGetSubmissionDetailUseCase
{
    private readonly ISubmissionRepository _submissionRepository;

    public GetSubmissionDetailUseCase(ISubmissionRepository submissionRepository)
    {
        _submissionRepository = submissionRepository;
    }

    public async Task<SubmissionDetailDto?> HandleAsync(Guid submissionId, CancellationToken cancellationToken = default)
    {
        var submission = await _submissionRepository.GetByIdAsync(new SubmissionId(submissionId));
        return submission is null ? null : QueryUseCaseMapper.ToSubmissionDetailDto(submission);
    }
}

public sealed class GetAuthorizedSubmissionDetailUseCase : IGetAuthorizedSubmissionDetailUseCase
{
    private readonly ISubmissionRepository _submissionRepository;
    private readonly IAssignmentRepository _assignmentRepository;
    private readonly IClassroomRepository _classroomRepository;

    public GetAuthorizedSubmissionDetailUseCase(
        ISubmissionRepository submissionRepository,
        IAssignmentRepository assignmentRepository,
        IClassroomRepository classroomRepository)
    {
        _submissionRepository = submissionRepository;
        _assignmentRepository = assignmentRepository;
        _classroomRepository = classroomRepository;
    }

    public async Task<AuthorizedSubmissionDetailResponseDto> HandleAsync(
        Guid submissionId,
        Guid actorUserId,
        UserRoleDto actorRole,
        CancellationToken cancellationToken = default)
    {
        var submission = await _submissionRepository.GetByIdAsync(new SubmissionId(submissionId));
        if (submission is null)
        {
            return new AuthorizedSubmissionDetailResponseDto(ResourceAccessDecisionDto.NotFound, null);
        }

        if (actorRole == UserRoleDto.Admin)
        {
            return new AuthorizedSubmissionDetailResponseDto(
                ResourceAccessDecisionDto.Allowed,
                QueryUseCaseMapper.ToSubmissionDetailDto(submission));
        }

        if (actorRole == UserRoleDto.Student)
        {
            if (submission.StudentId.Value != actorUserId)
            {
                return new AuthorizedSubmissionDetailResponseDto(ResourceAccessDecisionDto.Forbidden, null);
            }

            return new AuthorizedSubmissionDetailResponseDto(
                ResourceAccessDecisionDto.Allowed,
                QueryUseCaseMapper.ToSubmissionDetailDto(submission));
        }

        if (actorRole != UserRoleDto.Teacher)
        {
            return new AuthorizedSubmissionDetailResponseDto(ResourceAccessDecisionDto.Forbidden, null);
        }

        var assignment = await _assignmentRepository.GetByIdAsync(submission.AssignmentId);
        if (assignment is null)
        {
            return new AuthorizedSubmissionDetailResponseDto(ResourceAccessDecisionDto.NotFound, null);
        }

        var classroom = await _classroomRepository.GetByIdAsync(assignment.ClassroomId);
        if (classroom is null)
        {
            return new AuthorizedSubmissionDetailResponseDto(ResourceAccessDecisionDto.NotFound, null);
        }

        if (classroom.TeacherId.Value != actorUserId)
        {
            return new AuthorizedSubmissionDetailResponseDto(ResourceAccessDecisionDto.Forbidden, null);
        }

        return new AuthorizedSubmissionDetailResponseDto(
            ResourceAccessDecisionDto.Allowed,
            QueryUseCaseMapper.ToSubmissionDetailDto(submission));
    }
}

public sealed class GetScoreboardUseCase : IGetScoreboardUseCase
{
    private readonly IAssignmentRepository _assignmentRepository;
    private readonly ISubmissionRepository _submissionRepository;
    private readonly IUserRepository _userRepository;

    public GetScoreboardUseCase(
        IAssignmentRepository assignmentRepository,
        ISubmissionRepository submissionRepository,
        IUserRepository userRepository)
    {
        _assignmentRepository = assignmentRepository;
        _submissionRepository = submissionRepository;
        _userRepository = userRepository;
    }

    public async Task<IReadOnlyList<ScoreboardItemDto>> HandleAsync(
        Guid classroomId,
        Guid? assignmentId = null,
        CancellationToken cancellationToken = default)
    {
        var assignments = await _assignmentRepository.GetByClassroomIdAsync(new ClassroomId(classroomId));
        if (assignments.Count == 0)
        {
            return Array.Empty<ScoreboardItemDto>();
        }

        if (assignmentId.HasValue)
        {
            assignments = assignments
                .Where(x => x.Id.Value == assignmentId.Value)
                .ToList();

            if (assignments.Count == 0)
            {
                return Array.Empty<ScoreboardItemDto>();
            }
        }

        var allSubmissions = new List<Domain.Entity.Submission>();
        foreach (var assignment in assignments)
        {
            var submissions = await _submissionRepository.GetByAssignmentIdAsync(assignment.Id);
            allSubmissions.AddRange(submissions);
        }

        var grouped = allSubmissions
            .GroupBy(x => x.StudentId)
            .ToList();

        var items = new List<ScoreboardItemDto>(grouped.Count);
        foreach (var group in grouped)
        {
            var user = await _userRepository.GetByIdAsync(group.Key);
            var studentName = user?.FullName ?? group.Key.Value.ToString("N");

            items.Add(new ScoreboardItemDto(
                group.Key.Value,
                studentName,
                group.Average(x => x.TotalScore),
                group.Count()));
        }

        return items
            .OrderByDescending(x => x.AverageScore)
            .ThenBy(x => x.StudentName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

public sealed class GetSubmissionHistoryUseCase : IGetSubmissionHistoryUseCase
{
    private readonly ISubmissionRepository _submissionRepository;

    public GetSubmissionHistoryUseCase(ISubmissionRepository submissionRepository)
    {
        _submissionRepository = submissionRepository;
    }

    public async Task<PagedResponseDto<SubmissionDetailDto>> HandleAsync(
        Guid studentId,
        PagedRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var submissions = await _submissionRepository.GetByStudentIdAsync(new UserId(studentId));

        var pageNumber = request.PageNumber <= 0 ? 1 : request.PageNumber;
        var pageSize = request.PageSize <= 0 ? 20 : request.PageSize;

        var items = submissions
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(QueryUseCaseMapper.ToSubmissionDetailDto)
            .ToList();

        return new PagedResponseDto<SubmissionDetailDto>(items, pageNumber, pageSize, submissions.Count);
    }
}

public sealed class CheckClassroomAccessUseCase : ICheckClassroomAccessUseCase
{
    private readonly IClassroomRepository _classroomRepository;

    public CheckClassroomAccessUseCase(IClassroomRepository classroomRepository)
    {
        _classroomRepository = classroomRepository;
    }

    public async Task<ResourceAccessDecisionDto> HandleAsync(
        Guid classroomId,
        Guid actorUserId,
        UserRoleDto actorRole,
        CancellationToken cancellationToken = default)
    {
        var classroom = await _classroomRepository.GetByIdAsync(new ClassroomId(classroomId));
        if (classroom is null)
        {
            return ResourceAccessDecisionDto.NotFound;
        }

        if (actorRole == UserRoleDto.Admin)
        {
            return ResourceAccessDecisionDto.Allowed;
        }

        if (actorRole == UserRoleDto.Teacher && classroom.TeacherId.Value == actorUserId)
        {
            return ResourceAccessDecisionDto.Allowed;
        }

        return ResourceAccessDecisionDto.Forbidden;
    }
}

public sealed class GetAuthorizedClassroomOverviewUseCase : IGetAuthorizedClassroomOverviewUseCase
{
    private readonly IClassroomRepository _classroomRepository;
    private readonly IUserRepository _userRepository;
    private readonly IAssignmentRepository _assignmentRepository;

    public GetAuthorizedClassroomOverviewUseCase(
        IClassroomRepository classroomRepository,
        IUserRepository userRepository,
        IAssignmentRepository assignmentRepository)
    {
        _classroomRepository = classroomRepository;
        _userRepository = userRepository;
        _assignmentRepository = assignmentRepository;
    }

    public async Task<AuthorizedClassroomOverviewResponseDto> HandleAsync(
        Guid classroomId,
        Guid actorUserId,
        UserRoleDto actorRole,
        CancellationToken cancellationToken = default)
    {
        var classroom = await _classroomRepository.GetByIdAsync(new ClassroomId(classroomId));
        if (classroom is null)
        {
            return new AuthorizedClassroomOverviewResponseDto(ResourceAccessDecisionDto.NotFound, null);
        }

        var canAccess = actorRole == UserRoleDto.Admin
            || (actorRole == UserRoleDto.Teacher && classroom.TeacherId.Value == actorUserId)
            || (actorRole == UserRoleDto.Student && classroom.StudentIds.Any(x => x.Value == actorUserId));

        if (!canAccess)
        {
            return new AuthorizedClassroomOverviewResponseDto(ResourceAccessDecisionDto.Forbidden, null);
        }

        var members = new List<ClassroomMemberDto>();

        var teacher = await _userRepository.GetByIdAsync(classroom.TeacherId);
        var teacherName = teacher?.FullName ?? classroom.TeacherId.Value.ToString("N");
        members.Add(new ClassroomMemberDto(classroom.TeacherId.Value, teacherName, UserRoleDto.Teacher));

        foreach (var studentId in classroom.StudentIds)
        {
            var student = await _userRepository.GetByIdAsync(studentId);
            members.Add(new ClassroomMemberDto(
                studentId.Value,
                student?.FullName ?? studentId.Value.ToString("N"),
                UserRoleDto.Student));
        }

        var assignments = await _assignmentRepository.GetByClassroomIdAsync(classroom.Id);
        var assignmentItems = assignments
            .OrderByDescending(x => x.DueDate)
            .Select(assignment => new AssignmentListItemDto(
                assignment.Id.Value,
                assignment.ClassroomId.Value,
                assignment.Title,
                assignment.DueDate,
                EnumMapper.ToDto(assignment.PublishStatus),
                EnumMapper.ToDto(assignment.GradingType)))
            .ToList();

        var overview = new ClassroomOverviewDto(
            classroom.Id.Value,
            classroom.Name,
            classroom.JoinCode,
            classroom.TeacherId.Value,
            teacherName,
            classroom.StudentIds.Count,
            members,
            assignmentItems);

        return new AuthorizedClassroomOverviewResponseDto(ResourceAccessDecisionDto.Allowed, overview);
    }
}

internal static class QueryUseCaseMapper
{
    public static SubmissionDetailDto ToSubmissionDetailDto(Domain.Entity.Submission submission)
    {
        var testCaseResults = submission.TestCaseResults
            .Select(result => new TestCaseExecutionResultDto(
                result.TestCaseId.Value,
                EnumMapper.ToDto(result.Status),
                result.ActualOutput,
                result.ExecutionTimeMs,
                result.MemoryUsedKb))
            .ToList();

        var rubricResults = submission.RubricResults
            .Select(result => new Ports.DTO.Rubric.RubricScoreDto(result.CriteriaName, result.GivenScore, result.CommentReason))
            .ToList();

        return new SubmissionDetailDto(
            submission.Id.Value,
            submission.AssignmentId.Value,
            submission.StudentId.Value,
            EnumMapper.ToDto(submission.Status),
            submission.TotalScore,
            submission.SubmitTime,
            testCaseResults,
            rubricResults,
            null);
    }
}
