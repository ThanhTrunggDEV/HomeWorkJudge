using System.Linq;
using System.Threading.Tasks;
using Domain.Entity;
using Domain.Ports;
using Domain.ValueObject;
using Microsoft.EntityFrameworkCore;
using SqliteDataAccess.PersistenceModel;

namespace SqliteDataAccess.Repository;

public sealed class SqliteClassroomRepository : IClassroomRepository
{
    private readonly AppDbContext _context;

    public SqliteClassroomRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Classroom?> GetByIdAsync(ClassroomId id)
    {
        var classroomRecord = await _context.Classrooms
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id.Value);

        if (classroomRecord is null)
        {
            return null;
        }

        var studentIds = await _context.ClassroomStudents
            .AsNoTracking()
            .Where(x => x.ClassroomId == id.Value)
            .Select(x => x.StudentId)
            .ToListAsync();

        return SqliteEntityMapper.ToDomain(classroomRecord, studentIds);
    }

    public async Task<Classroom?> GetByJoinCodeAsync(string joinCode)
    {
        if (string.IsNullOrWhiteSpace(joinCode))
        {
            return null;
        }

        var normalizedJoinCode = joinCode.Trim().ToUpperInvariant();
        var classroomRecord = await _context.Classrooms
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.JoinCode == normalizedJoinCode);

        if (classroomRecord is null)
        {
            return null;
        }

        var studentIds = await _context.ClassroomStudents
            .AsNoTracking()
            .Where(x => x.ClassroomId == classroomRecord.Id)
            .Select(x => x.StudentId)
            .ToListAsync();

        return SqliteEntityMapper.ToDomain(classroomRecord, studentIds);
    }

    public Task AddAsync(Classroom classroom)
    {
        var classroomRecord = SqliteEntityMapper.ToRecord(classroom);
        _context.Classrooms.Add(classroomRecord);

        var studentRecords = classroom.StudentIds
            .Select(studentId => new ClassroomStudentRecord
            {
                ClassroomId = classroom.Id.Value,
                StudentId = studentId.Value
            });

        _context.ClassroomStudents.AddRange(studentRecords);
        return Task.CompletedTask;
    }

    public async Task UpdateAsync(Classroom classroom)
    {
        var classroomRecord = SqliteEntityMapper.ToRecord(classroom);
        var exists = await _context.Classrooms.AnyAsync(x => x.Id == classroomRecord.Id);

        if (exists)
        {
            _context.Classrooms.Update(classroomRecord);
        }
        else
        {
            await _context.Classrooms.AddAsync(classroomRecord);
        }

        var existingStudents = _context.ClassroomStudents
            .Where(x => x.ClassroomId == classroomRecord.Id);

        _context.ClassroomStudents.RemoveRange(existingStudents);

        var studentRecords = classroom.StudentIds
            .Select(studentId => new ClassroomStudentRecord
            {
                ClassroomId = classroom.Id.Value,
                StudentId = studentId.Value
            });

        _context.ClassroomStudents.AddRange(studentRecords);
    }
}
