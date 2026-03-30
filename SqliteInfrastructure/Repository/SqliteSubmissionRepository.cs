using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Entity;
using Domain.Ports;
using Domain.ValueObject;
using Microsoft.EntityFrameworkCore;

namespace SqliteDataAccess.Repository;

public sealed class SqliteSubmissionRepository : ISubmissionRepository
{
    private readonly AppDbContext _context;

    public SqliteSubmissionRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Submission?> GetByIdAsync(SubmissionId id)
    {
        var submissionRecord = await _context.Submissions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id.Value);

        if (submissionRecord is null)
        {
            return null;
        }

        var testCaseResults = await _context.SubmissionTestCaseResults
            .AsNoTracking()
            .Where(x => x.SubmissionId == id.Value)
            .OrderBy(x => x.SortOrder)
            .ToListAsync();

        var rubricResults = await _context.SubmissionRubricResults
            .AsNoTracking()
            .Where(x => x.SubmissionId == id.Value)
            .OrderBy(x => x.SortOrder)
            .ToListAsync();

        return SqliteEntityMapper.ToDomain(submissionRecord, testCaseResults, rubricResults);
    }

    public async Task<IReadOnlyList<Submission>> GetByAssignmentIdAsync(AssignmentId assignmentId)
    {
        var submissionRecords = await _context.Submissions
            .AsNoTracking()
            .Where(x => x.AssignmentId == assignmentId.Value)
            .ToListAsync();

        if (submissionRecords.Count == 0)
        {
            return new List<Submission>();
        }

        var submissionIds = submissionRecords.Select(x => x.Id).ToList();

        var allTestCaseResults = await _context.SubmissionTestCaseResults
            .AsNoTracking()
            .Where(x => submissionIds.Contains(x.SubmissionId))
            .ToListAsync();

        var allRubricResults = await _context.SubmissionRubricResults
            .AsNoTracking()
            .Where(x => submissionIds.Contains(x.SubmissionId))
            .ToListAsync();

        var testCaseLookup = allTestCaseResults
            .GroupBy(x => x.SubmissionId)
            .ToDictionary(x => x.Key, x => (IReadOnlyList<PersistenceModel.SubmissionTestCaseResultRecord>)x.OrderBy(r => r.SortOrder).ToList());

        var rubricLookup = allRubricResults
            .GroupBy(x => x.SubmissionId)
            .ToDictionary(x => x.Key, x => (IReadOnlyList<PersistenceModel.SubmissionRubricResultRecord>)x.OrderBy(r => r.SortOrder).ToList());

        var submissions = submissionRecords
            .Select(record =>
            {
                var testCaseResults = testCaseLookup.TryGetValue(record.Id, out var t) ? t : new List<PersistenceModel.SubmissionTestCaseResultRecord>();
                var rubricResults = rubricLookup.TryGetValue(record.Id, out var r) ? r : new List<PersistenceModel.SubmissionRubricResultRecord>();
                return SqliteEntityMapper.ToDomain(record, testCaseResults, rubricResults);
            })
            .ToList();

        return submissions;
    }

    public async Task<IReadOnlyList<Submission>> GetByStudentIdAsync(UserId studentId)
    {
        var submissionRecords = await _context.Submissions
            .AsNoTracking()
            .Where(x => x.StudentId == studentId.Value)
            .OrderByDescending(x => x.SubmitTime)
            .ToListAsync();

        if (submissionRecords.Count == 0)
        {
            return new List<Submission>();
        }

        var submissionIds = submissionRecords.Select(x => x.Id).ToList();

        var allTestCaseResults = await _context.SubmissionTestCaseResults
            .AsNoTracking()
            .Where(x => submissionIds.Contains(x.SubmissionId))
            .ToListAsync();

        var allRubricResults = await _context.SubmissionRubricResults
            .AsNoTracking()
            .Where(x => submissionIds.Contains(x.SubmissionId))
            .ToListAsync();

        var testCaseLookup = allTestCaseResults
            .GroupBy(x => x.SubmissionId)
            .ToDictionary(x => x.Key, x => (IReadOnlyList<PersistenceModel.SubmissionTestCaseResultRecord>)x.OrderBy(r => r.SortOrder).ToList());

        var rubricLookup = allRubricResults
            .GroupBy(x => x.SubmissionId)
            .ToDictionary(x => x.Key, x => (IReadOnlyList<PersistenceModel.SubmissionRubricResultRecord>)x.OrderBy(r => r.SortOrder).ToList());

        var submissions = submissionRecords
            .Select(record =>
            {
                var testCaseResults = testCaseLookup.TryGetValue(record.Id, out var t) ? t : new List<PersistenceModel.SubmissionTestCaseResultRecord>();
                var rubricResults = rubricLookup.TryGetValue(record.Id, out var r) ? r : new List<PersistenceModel.SubmissionRubricResultRecord>();
                return SqliteEntityMapper.ToDomain(record, testCaseResults, rubricResults);
            })
            .ToList();

        return submissions;
    }

    public Task AddAsync(Submission submission)
    {
        var submissionId = submission.Id.Value;
        _context.Submissions.Add(SqliteEntityMapper.ToRecord(submission));

        if (submission.TestCaseResults.Count > 0)
        {
            var testCaseRecords = submission.TestCaseResults
                .Select((result, index) => SqliteEntityMapper.ToRecord(submissionId, result, index));

            _context.SubmissionTestCaseResults.AddRange(testCaseRecords);
        }

        if (submission.RubricResults.Count > 0)
        {
            var rubricRecords = submission.RubricResults
                .Select((result, index) => SqliteEntityMapper.ToRecord(submissionId, result, index));

            _context.SubmissionRubricResults.AddRange(rubricRecords);
        }

        return Task.CompletedTask;
    }

    public async Task UpdateAsync(Submission submission)
    {
        var submissionRecord = SqliteEntityMapper.ToRecord(submission);
        var exists = await _context.Submissions.AnyAsync(x => x.Id == submissionRecord.Id);

        if (exists)
        {
            _context.Submissions.Update(submissionRecord);
        }
        else
        {
            await _context.Submissions.AddAsync(submissionRecord);
        }

        var existingTestCaseResults = _context.SubmissionTestCaseResults
            .Where(x => x.SubmissionId == submissionRecord.Id);

        _context.SubmissionTestCaseResults.RemoveRange(existingTestCaseResults);

        if (submission.TestCaseResults.Count > 0)
        {
            var testCaseRecords = submission.TestCaseResults
                .Select((result, index) => SqliteEntityMapper.ToRecord(submissionRecord.Id, result, index));

            _context.SubmissionTestCaseResults.AddRange(testCaseRecords);
        }

        var existingRubricResults = _context.SubmissionRubricResults
            .Where(x => x.SubmissionId == submissionRecord.Id);

        _context.SubmissionRubricResults.RemoveRange(existingRubricResults);

        if (submission.RubricResults.Count > 0)
        {
            var rubricRecords = submission.RubricResults
                .Select((result, index) => SqliteEntityMapper.ToRecord(submissionRecord.Id, result, index));

            _context.SubmissionRubricResults.AddRange(rubricRecords);
        }
    }
}
