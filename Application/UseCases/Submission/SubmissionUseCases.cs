using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Common;
using Application.DomainEvents;
using Domain.Entity;
using Domain.Exception;
using Domain.Ports;
using Domain.ValueObject;
using Ports.DTO.Submission;
using Ports.InBoundPorts.Submission;

namespace Application.UseCases.Submission;

public sealed class SubmitCodeUseCase : ISubmitCodeUseCase
{
    private readonly IAssignmentRepository _assignmentRepository;
    private readonly IClassroomRepository _classroomRepository;
    private readonly IUserRepository _userRepository;
    private readonly ISubmissionRepository _submissionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly DomainEventDispatcher _domainEventDispatcher;

    public SubmitCodeUseCase(
        IAssignmentRepository assignmentRepository,
        IClassroomRepository classroomRepository,
        IUserRepository userRepository,
        ISubmissionRepository submissionRepository,
        IUnitOfWork unitOfWork,
        DomainEventDispatcher domainEventDispatcher)
    {
        _assignmentRepository = assignmentRepository;
        _classroomRepository = classroomRepository;
        _userRepository = userRepository;
        _submissionRepository = submissionRepository;
        _unitOfWork = unitOfWork;
        _domainEventDispatcher = domainEventDispatcher;
    }

    public async Task<SubmitCodeResponseDto> HandleAsync(SubmitCodeRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var assignment = await _assignmentRepository.GetByIdAsync(new AssignmentId(request.AssignmentId))
            ?? throw new DomainException("Assignment not found.");

        var student = await _userRepository.GetByIdAsync(new UserId(request.StudentId))
            ?? throw new DomainException("Student not found.");

        if (student.Role != UserRole.Student)
        {
            throw new DomainException("Only student can submit code.");
        }

        var classroom = await _classroomRepository.GetByIdAsync(assignment.ClassroomId)
            ?? throw new DomainException("Classroom not found.");

        if (!classroom.StudentIds.Contains(student.Id))
        {
            throw new DomainException("Student is not a member of this classroom.");
        }

        if (assignment.PublishStatus != PublishStatus.Published)
        {
            throw new DomainException("Assignment is not published.");
        }

        if (!IsLanguageAllowed(assignment.AllowedLanguages, request.Language))
        {
            throw new DomainException($"Language '{request.Language}' is not allowed for this assignment.");
        }

        var submission = new Domain.Entity.Submission(
            new SubmissionId(Guid.NewGuid()),
            assignment.Id,
            new UserId(request.StudentId),
            request.SourceCode,
            request.Language);

        var events = DomainEventUtilities.SnapshotEvents(submission);

        await _submissionRepository.AddAsync(submission);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _domainEventDispatcher.DispatchAsync(events, cancellationToken);
        DomainEventUtilities.ClearEvents(submission);

        return new SubmitCodeResponseDto(
            submission.Id.Value,
            EnumMapper.ToDto(submission.Status),
            submission.SubmitTime);
    }

    private static bool IsLanguageAllowed(string allowedLanguages, string requestedLanguage)
    {
        if (string.IsNullOrWhiteSpace(requestedLanguage))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(allowedLanguages) ||
            string.Equals(allowedLanguages, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return allowedLanguages
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(language => string.Equals(language, requestedLanguage, StringComparison.OrdinalIgnoreCase));
    }
}
