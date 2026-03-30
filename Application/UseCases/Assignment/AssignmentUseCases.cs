using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Application.Common;
using Application.DomainEvents;
using Domain.Entity;
using Domain.Exception;
using Domain.Ports;
using Domain.ValueObject;
using Ports.DTO.Assignment;
using Ports.DTO.Common;
using Ports.DTO.Rubric;
using Ports.InBoundPorts.Assignment;
using Ports.OutBoundPorts.Queue;

namespace Application.UseCases.Assignment;

public sealed class CreateAssignmentUseCase : ICreateAssignmentUseCase
{
    private readonly IAssignmentRepository _assignmentRepository;
    private readonly IClassroomRepository _classroomRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateAssignmentUseCase(
        IAssignmentRepository assignmentRepository,
        IClassroomRepository classroomRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork)
    {
        _assignmentRepository = assignmentRepository;
        _classroomRepository = classroomRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<CreateAssignmentResponseDto> HandleAsync(CreateAssignmentRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var classroom = await _classroomRepository.GetByIdAsync(new ClassroomId(request.ClassroomId))
            ?? throw new DomainException("Classroom not found.");

        var requester = await _userRepository.GetByIdAsync(new UserId(request.RequestedByUserId))
            ?? throw new DomainException("Requester not found.");

        AssignmentAuthorization.EnsureCanManageClassroom(requester, classroom);

        var assignment = new Domain.Entity.Assignment(
            new AssignmentId(Guid.NewGuid()),
            classroom.Id,
            request.Title,
            request.Description,
            request.DueDate,
            EnumMapper.ToDomain(request.GradingType));

        assignment.UpdateOverview(request.Title, request.Description, request.DueDate, AssignmentUseCaseHelpers.ToAllowedLanguages(request.AllowedLanguages));
        assignment.UpdateLimits(request.TimeLimitMs, request.MemoryLimitKb, request.MaxSubmissions);

        await _assignmentRepository.AddAsync(assignment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateAssignmentResponseDto(assignment.Id.Value, EnumMapper.ToDto(assignment.PublishStatus));
    }
}

public sealed class UpdateAssignmentUseCase : IUpdateAssignmentUseCase
{
    private readonly IAssignmentRepository _assignmentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateAssignmentUseCase(IAssignmentRepository assignmentRepository, IUnitOfWork unitOfWork)
    {
        _assignmentRepository = assignmentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task HandleAsync(UpdateAssignmentRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var assignment = await _assignmentRepository.GetByIdWithTestCasesAsync(new AssignmentId(request.AssignmentId))
            ?? throw new DomainException("Assignment not found.");

        assignment.UpdateOverview(request.Title, request.Description, request.DueDate, AssignmentUseCaseHelpers.ToAllowedLanguages(request.AllowedLanguages));
        assignment.UpdateLimits(request.TimeLimitMs, request.MemoryLimitKb, request.MaxSubmissions);

        await _assignmentRepository.UpdateAsync(assignment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class ListAssignmentsUseCase : IListAssignmentsUseCase
{
    private readonly IAssignmentRepository _assignmentRepository;
    private readonly IClassroomRepository _classroomRepository;
    private readonly IUserRepository _userRepository;

    public ListAssignmentsUseCase(
        IAssignmentRepository assignmentRepository,
        IClassroomRepository classroomRepository,
        IUserRepository userRepository)
    {
        _assignmentRepository = assignmentRepository;
        _classroomRepository = classroomRepository;
        _userRepository = userRepository;
    }

    public async Task<PagedResponseDto<AssignmentListItemDto>> HandleAsync(
        Guid classroomId,
        Guid requestedByUserId,
        PagedRequestDto request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var classroom = await _classroomRepository.GetByIdAsync(new ClassroomId(classroomId))
            ?? throw new DomainException("Classroom not found.");

        var requester = await _userRepository.GetByIdAsync(new UserId(requestedByUserId))
            ?? throw new DomainException("Requester not found.");

        AssignmentAuthorization.EnsureCanViewClassroom(requester, classroom);

        var assignments = await _assignmentRepository.GetByClassroomIdAsync(classroom.Id);

        var pageNumber = request.PageNumber <= 0 ? 1 : request.PageNumber;
        var pageSize = request.PageSize <= 0 ? 20 : request.PageSize;

        var items = assignments
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(assignment => new AssignmentListItemDto(
                assignment.Id.Value,
                assignment.ClassroomId.Value,
                assignment.Title,
                assignment.DueDate,
                EnumMapper.ToDto(assignment.PublishStatus),
                EnumMapper.ToDto(assignment.GradingType)))
            .ToList();

        return new PagedResponseDto<AssignmentListItemDto>(items, pageNumber, pageSize, assignments.Count);
    }
}

public sealed class PublishAssignmentUseCase : IPublishAssignmentUseCase
{
    private readonly IAssignmentRepository _assignmentRepository;
    private readonly IClassroomRepository _classroomRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly DomainEventDispatcher _domainEventDispatcher;

    public PublishAssignmentUseCase(
        IAssignmentRepository assignmentRepository,
        IClassroomRepository classroomRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        DomainEventDispatcher domainEventDispatcher)
    {
        _assignmentRepository = assignmentRepository;
        _classroomRepository = classroomRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _domainEventDispatcher = domainEventDispatcher;
    }

    public async Task HandleAsync(PublishAssignmentRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var assignment = await _assignmentRepository.GetByIdWithTestCasesAsync(new AssignmentId(request.AssignmentId))
            ?? throw new DomainException("Assignment not found.");

        var classroom = await _classroomRepository.GetByIdAsync(assignment.ClassroomId)
            ?? throw new DomainException("Classroom not found.");

        var requester = await _userRepository.GetByIdAsync(new UserId(request.RequestedByUserId))
            ?? throw new DomainException("Requester not found.");

        AssignmentAuthorization.EnsureCanManageClassroom(requester, classroom);

        assignment.Publish();
        var events = DomainEventUtilities.SnapshotEvents(assignment);

        await _assignmentRepository.UpdateAsync(assignment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _domainEventDispatcher.DispatchAsync(events, cancellationToken);
        DomainEventUtilities.ClearEvents(assignment);
    }
}

public sealed class AddAssignmentTestCaseUseCase : IAddAssignmentTestCaseUseCase
{
    private readonly IAssignmentRepository _assignmentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AddAssignmentTestCaseUseCase(IAssignmentRepository assignmentRepository, IUnitOfWork unitOfWork)
    {
        _assignmentRepository = assignmentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<AssignmentTestCaseDto> HandleAsync(AddAssignmentTestCaseRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var assignment = await _assignmentRepository.GetByIdWithTestCasesAsync(new AssignmentId(request.AssignmentId))
            ?? throw new DomainException("Assignment not found.");

        var testCase = new TestCase(
            new TestCaseId(Guid.NewGuid()),
            assignment.Id,
            request.InputData,
            request.ExpectedOutput,
            request.IsHidden,
            request.ScoreWeight);

        assignment.AddTestCase(testCase);

        await _assignmentRepository.UpdateAsync(assignment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new AssignmentTestCaseDto(
            testCase.Id.Value,
            testCase.InputData,
            testCase.ExpectedOutput,
            testCase.IsHidden,
            testCase.ScoreWeight);
    }
}

public sealed class UpdateAssignmentTestCaseUseCase : IUpdateAssignmentTestCaseUseCase
{
    private readonly IAssignmentRepository _assignmentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateAssignmentTestCaseUseCase(IAssignmentRepository assignmentRepository, IUnitOfWork unitOfWork)
    {
        _assignmentRepository = assignmentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<AssignmentTestCaseDto> HandleAsync(UpdateAssignmentTestCaseRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var assignment = await _assignmentRepository.GetByIdWithTestCasesAsync(new AssignmentId(request.AssignmentId))
            ?? throw new DomainException("Assignment not found.");

        var testCaseId = new TestCaseId(request.TestCaseId);
        assignment.UpdateTestCase(testCaseId, request.InputData, request.ExpectedOutput, request.IsHidden, request.ScoreWeight);

        var updated = assignment.TestCases.First(x => x.Id == testCaseId);

        await _assignmentRepository.UpdateAsync(assignment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new AssignmentTestCaseDto(
            updated.Id.Value,
            updated.InputData,
            updated.ExpectedOutput,
            updated.IsHidden,
            updated.ScoreWeight);
    }
}

public sealed class DeleteAssignmentTestCaseUseCase : IDeleteAssignmentTestCaseUseCase
{
    private readonly IAssignmentRepository _assignmentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteAssignmentTestCaseUseCase(IAssignmentRepository assignmentRepository, IUnitOfWork unitOfWork)
    {
        _assignmentRepository = assignmentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task HandleAsync(DeleteAssignmentTestCaseRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var assignment = await _assignmentRepository.GetByIdWithTestCasesAsync(new AssignmentId(request.AssignmentId))
            ?? throw new DomainException("Assignment not found.");

        assignment.RemoveTestCase(new TestCaseId(request.TestCaseId));

        await _assignmentRepository.UpdateAsync(assignment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class CreateAssignmentRubricUseCase : ICreateAssignmentRubricUseCase
{
    private readonly IAssignmentRepository _assignmentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateAssignmentRubricUseCase(IAssignmentRepository assignmentRepository, IUnitOfWork unitOfWork)
    {
        _assignmentRepository = assignmentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task HandleAsync(CreateAssignmentRubricRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var assignment = await _assignmentRepository.GetByIdWithTestCasesAsync(new AssignmentId(request.AssignmentId))
            ?? throw new DomainException("Assignment not found.");

        var rubric = new Rubric(new RubricId(Guid.NewGuid()), assignment.Id, AssignmentUseCaseHelpers.SerializeCriteria(request.Criteria));
        assignment.SetRubric(rubric);

        await _assignmentRepository.UpdateAsync(assignment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class UpdateAssignmentRubricUseCase : IUpdateAssignmentRubricUseCase
{
    private readonly IAssignmentRepository _assignmentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateAssignmentRubricUseCase(IAssignmentRepository assignmentRepository, IUnitOfWork unitOfWork)
    {
        _assignmentRepository = assignmentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task HandleAsync(UpdateAssignmentRubricRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var assignment = await _assignmentRepository.GetByIdWithTestCasesAsync(new AssignmentId(request.AssignmentId))
            ?? throw new DomainException("Assignment not found.");

        var rubric = assignment.Rubric ?? new Rubric(new RubricId(Guid.NewGuid()), assignment.Id, "[]");
        rubric.UpdateCriteria(AssignmentUseCaseHelpers.SerializeCriteria(request.Criteria));
        assignment.SetRubric(rubric);

        await _assignmentRepository.UpdateAsync(assignment);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class RejudgeAssignmentUseCase : IRejudgeAssignmentUseCase
{
    private readonly IAssignmentRepository _assignmentRepository;
    private readonly ISubmissionRepository _submissionRepository;
    private readonly IBackgroundJobQueuePort _backgroundJobQueuePort;

    public RejudgeAssignmentUseCase(
        IAssignmentRepository assignmentRepository,
        ISubmissionRepository submissionRepository,
        IBackgroundJobQueuePort backgroundJobQueuePort)
    {
        _assignmentRepository = assignmentRepository;
        _submissionRepository = submissionRepository;
        _backgroundJobQueuePort = backgroundJobQueuePort;
    }

    public async Task HandleAsync(Guid assignmentId, CancellationToken cancellationToken = default)
    {
        var assignment = await _assignmentRepository.GetByIdAsync(new AssignmentId(assignmentId))
            ?? throw new DomainException("Assignment not found.");

        var submissions = await _submissionRepository.GetByAssignmentIdAsync(assignment.Id);

        foreach (var submission in submissions)
        {
            var payload = JsonSerializer.Serialize(new
            {
                SubmissionId = submission.Id.Value,
                AssignmentId = assignment.Id.Value
            });

            var envelope = new JobEnvelopeDto(
                JobName: "submission.grade",
                Payload: payload,
                CorrelationId: Guid.NewGuid().ToString("N"),
                CreatedAt: DateTimeOffset.UtcNow,
                RetryCount: 0);

            await _backgroundJobQueuePort.EnqueueAsync(envelope, cancellationToken);
        }
    }
}

internal static class AssignmentUseCaseHelpers
{
    public static string ToAllowedLanguages(IReadOnlyList<string> allowedLanguages)
    {
        if (allowedLanguages is null || allowedLanguages.Count == 0)
        {
            return "ALL";
        }

        var normalized = allowedLanguages
            .Where(language => !string.IsNullOrWhiteSpace(language))
            .Select(language => language.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return normalized.Length == 0
            ? "ALL"
            : string.Join(",", normalized);
    }

    public static string SerializeCriteria(IReadOnlyList<RubricCriteriaDto> criteria)
        => JsonSerializer.Serialize(criteria ?? Array.Empty<RubricCriteriaDto>());
}

internal static class AssignmentAuthorization
{
    public static void EnsureCanViewClassroom(Domain.Entity.User requester, Domain.Entity.Classroom classroom)
    {
        if (requester.Role == UserRole.Admin)
        {
            return;
        }

        if (requester.Role == UserRole.Teacher && classroom.TeacherId == requester.Id)
        {
            return;
        }

        if (requester.Role == UserRole.Student && classroom.StudentIds.Contains(requester.Id))
        {
            return;
        }

        throw new DomainException("You do not have permission to view assignments in this classroom.");
    }

    public static void EnsureCanManageClassroom(Domain.Entity.User requester, Domain.Entity.Classroom classroom)
    {
        if (requester.Role == UserRole.Admin)
        {
            return;
        }

        if (requester.Role == UserRole.Teacher && classroom.TeacherId == requester.Id)
        {
            return;
        }

        throw new DomainException("You do not have permission to manage assignments in this classroom.");
    }
}
