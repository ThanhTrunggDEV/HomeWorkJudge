using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Common;
using Domain.Ports;
using Domain.ValueObject;
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

    public async Task<IReadOnlyList<ScoreboardItemDto>> HandleAsync(Guid classroomId, CancellationToken cancellationToken = default)
    {
        var assignments = await _assignmentRepository.GetByClassroomIdAsync(new ClassroomId(classroomId));
        if (assignments.Count == 0)
        {
            return Array.Empty<ScoreboardItemDto>();
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
