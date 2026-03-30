using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Common;
using Domain.Entity;
using Domain.Exception;
using Domain.Ports;
using Domain.ValueObject;
using Ports.DTO.Classroom;
using Ports.InBoundPorts.Classroom;

namespace Application.UseCases.Classroom;

public sealed class CreateClassroomUseCase : ICreateClassroomUseCase
{
    private readonly IClassroomRepository _classroomRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateClassroomUseCase(
        IClassroomRepository classroomRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork)
    {
        _classroomRepository = classroomRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<CreateClassroomResponseDto> HandleAsync(CreateClassroomRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var teacher = await _userRepository.GetByIdAsync(new UserId(request.TeacherId))
            ?? throw new DomainException("Teacher not found.");

        if (teacher.Role is not (UserRole.Teacher or UserRole.Admin))
        {
            throw new DomainException("Only Teacher or Admin can create classroom.");
        }

        var classroom = new Domain.Entity.Classroom(new ClassroomId(Guid.NewGuid()), request.Name, teacher.Id);

        await _classroomRepository.AddAsync(classroom);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateClassroomResponseDto(classroom.Id.Value, classroom.JoinCode);
    }
}

public sealed class JoinClassroomUseCase : IJoinClassroomUseCase
{
    private readonly IClassroomRepository _classroomRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public JoinClassroomUseCase(
        IClassroomRepository classroomRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork)
    {
        _classroomRepository = classroomRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<JoinClassroomResponseDto> HandleAsync(JoinClassroomRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var student = await _userRepository.GetByIdAsync(new UserId(request.StudentId))
            ?? throw new DomainException("Student not found.");

        if (student.Role != UserRole.Student)
        {
            throw new DomainException("Only student can join classroom.");
        }

        var classroom = await _classroomRepository.GetByJoinCodeAsync(request.JoinCode)
            ?? throw new DomainException("Classroom not found by join code.");

        classroom.AddStudent(student.Id);
        await _classroomRepository.UpdateAsync(classroom);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new JoinClassroomResponseDto(classroom.Id.Value, student.Id.Value, "Joined");
    }
}
