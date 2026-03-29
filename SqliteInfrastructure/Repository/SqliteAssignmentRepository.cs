using System.Linq;
using System.Threading.Tasks;
using Domain.Entity;
using Domain.Ports;
using Domain.ValueObject;
using Microsoft.EntityFrameworkCore;

namespace SqliteDataAccess.Repository;

public sealed class SqliteAssignmentRepository : IAssignmentRepository
{
    private readonly AppDbContext _context;

    public SqliteAssignmentRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<Assignment?> GetByIdAsync(AssignmentId id) => GetByIdWithTestCasesAsync(id);

    public async Task<Assignment?> GetByIdWithTestCasesAsync(AssignmentId id)
    {
        var assignmentRecord = await _context.Assignments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id.Value);

        if (assignmentRecord is null)
        {
            return null;
        }

        var testCaseRecords = await _context.TestCases
            .AsNoTracking()
            .Where(x => x.AssignmentId == id.Value)
            .OrderBy(x => x.Id)
            .ToListAsync();

        var rubricRecord = await _context.Rubrics
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.AssignmentId == id.Value);

        return SqliteEntityMapper.ToDomain(assignmentRecord, testCaseRecords, rubricRecord);
    }

    public Task AddAsync(Assignment assignment)
    {
        _context.Assignments.Add(SqliteEntityMapper.ToRecord(assignment));

        if (assignment.TestCases.Count > 0)
        {
            var testCaseRecords = assignment.TestCases
                .Select(SqliteEntityMapper.ToRecord);

            _context.TestCases.AddRange(testCaseRecords);
        }

        if (assignment.Rubric is not null)
        {
            _context.Rubrics.Add(SqliteEntityMapper.ToRecord(assignment.Rubric));
        }

        return Task.CompletedTask;
    }

    public async Task UpdateAsync(Assignment assignment)
    {
        var assignmentRecord = SqliteEntityMapper.ToRecord(assignment);
        var exists = await _context.Assignments.AnyAsync(x => x.Id == assignmentRecord.Id);

        if (exists)
        {
            _context.Assignments.Update(assignmentRecord);
        }
        else
        {
            await _context.Assignments.AddAsync(assignmentRecord);
        }

        var existingTestCases = _context.TestCases.Where(x => x.AssignmentId == assignmentRecord.Id);
        _context.TestCases.RemoveRange(existingTestCases);

        if (assignment.TestCases.Count > 0)
        {
            var testCaseRecords = assignment.TestCases
                .Select(SqliteEntityMapper.ToRecord);

            _context.TestCases.AddRange(testCaseRecords);
        }

        var existingRubric = _context.Rubrics.Where(x => x.AssignmentId == assignmentRecord.Id);
        _context.Rubrics.RemoveRange(existingRubric);

        if (assignment.Rubric is not null)
        {
            _context.Rubrics.Add(SqliteEntityMapper.ToRecord(assignment.Rubric));
        }
    }
}
