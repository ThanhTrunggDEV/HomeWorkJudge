using System.Collections.Generic;
using System.Threading.Tasks;
using Domain.Entity;
using Domain.ValueObject;

namespace Domain.Ports;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(UserId id);
    Task AddAsync(User user);
    Task UpdateAsync(User user);
}

public interface IClassroomRepository
{
    Task<Classroom?> GetByIdAsync(ClassroomId id);
    Task AddAsync(Classroom classroom);
    Task UpdateAsync(Classroom classroom);
}

public interface IAssignmentRepository
{
    Task<Assignment?> GetByIdAsync(AssignmentId id);
    Task<Assignment?> GetByIdWithTestCasesAsync(AssignmentId id);
    Task AddAsync(Assignment assignment);
    Task UpdateAsync(Assignment assignment);
}

public interface ISubmissionRepository
{
    Task<Submission?> GetByIdAsync(SubmissionId id);
    Task<IReadOnlyList<Submission>> GetByAssignmentIdAsync(AssignmentId assignmentId);
    Task AddAsync(Submission submission);
    Task UpdateAsync(Submission submission);
}
