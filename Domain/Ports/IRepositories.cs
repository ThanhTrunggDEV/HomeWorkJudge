using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Domain.Entity;
using Domain.ValueObject;

namespace Domain.Ports;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IUserRepository
{
    Task<User?> GetByIdAsync(UserId id);
    Task<User?> GetByEmailAsync(string email);
    Task AddAsync(User user);
    Task UpdateAsync(User user);
}

public interface IClassroomRepository
{
    Task<Classroom?> GetByIdAsync(ClassroomId id);
    Task<Classroom?> GetByJoinCodeAsync(string joinCode);
    Task AddAsync(Classroom classroom);
    Task UpdateAsync(Classroom classroom);
}

public interface IAssignmentRepository
{
    Task<Assignment?> GetByIdAsync(AssignmentId id);
    Task<Assignment?> GetByIdWithTestCasesAsync(AssignmentId id);
    Task<IReadOnlyList<Assignment>> GetByClassroomIdAsync(ClassroomId classroomId);
    Task AddAsync(Assignment assignment);
    Task UpdateAsync(Assignment assignment);
}

public interface ISubmissionRepository
{
    Task<Submission?> GetByIdAsync(SubmissionId id);
    Task<IReadOnlyList<Submission>> GetByAssignmentIdAsync(AssignmentId assignmentId);
    Task<IReadOnlyList<Submission>> GetByStudentIdAsync(UserId studentId);
    Task AddAsync(Submission submission);
    Task UpdateAsync(Submission submission);
}
