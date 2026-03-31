using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domain.Entity;
using Domain.Ports;
using Domain.ValueObject;
using Microsoft.EntityFrameworkCore;

namespace SqliteDataAccess.Repository;

public sealed class SqliteSubmissionRepository : ISubmissionRepository
{
    private readonly AppDbContext _db;

    public SqliteSubmissionRepository(AppDbContext db) => _db = db;

    public async Task<Submission?> GetByIdAsync(SubmissionId id, CancellationToken ct = default)
    {
        var record = await _db.Submissions
            .Include(s => s.RubricResults)
            .FirstOrDefaultAsync(s => s.Id == id.Value, ct);

        return record is null ? null : EntityMapper.ToDomain(record);
    }

    public async Task<IReadOnlyList<Submission>> GetBySessionIdAsync(
        GradingSessionId sessionId, CancellationToken ct = default)
    {
        var records = await _db.Submissions
            .Include(s => s.RubricResults)
            .Where(s => s.SessionId == sessionId.Value)
            .OrderBy(s => s.StudentIdentifier)
            .ToListAsync(ct);

        return records.Select(EntityMapper.ToDomain).ToList();
    }

    public async Task<IReadOnlyList<Submission>> GetByStatusAsync(
        GradingSessionId sessionId, SubmissionStatus status, CancellationToken ct = default)
    {
        var statusStr = status.ToString();
        var records = await _db.Submissions
            .Include(s => s.RubricResults)
            .Where(s => s.SessionId == sessionId.Value && s.Status == statusStr)
            .OrderBy(s => s.StudentIdentifier)
            .ToListAsync(ct);

        return records.Select(EntityMapper.ToDomain).ToList();
    }

    public Task<int> CountBySessionIdAsync(GradingSessionId sessionId, CancellationToken ct = default)
        => _db.Submissions.CountAsync(s => s.SessionId == sessionId.Value, ct);

    public async Task AddRangeAsync(IEnumerable<Submission> submissions, CancellationToken ct = default)
    {
        var records = submissions.Select(EntityMapper.ToRecord).ToList();
        await _db.Submissions.AddRangeAsync(records, ct);
    }

    public async Task UpdateAsync(Submission submission, CancellationToken ct = default)
    {
        var existing = await _db.Submissions
            .Include(s => s.RubricResults)
            .FirstOrDefaultAsync(s => s.Id == submission.Id.Value, ct);

        if (existing is null) return;

        var updated = EntityMapper.ToRecord(submission);
        existing.Status = updated.Status;
        existing.TotalScore = updated.TotalScore;
        existing.TeacherNote = updated.TeacherNote;
        existing.ErrorMessage = updated.ErrorMessage;
        existing.IsPlagiarismSuspected = updated.IsPlagiarismSuspected;
        existing.MaxSimilarityPercentage = updated.MaxSimilarityPercentage;

        // Sync RubricResults
        _db.RubricResults.RemoveRange(existing.RubricResults);
        foreach (var r in updated.RubricResults)
            r.SubmissionId = existing.Id;
        existing.RubricResults = updated.RubricResults;
    }

    public async Task UpdateRangeAsync(IEnumerable<Submission> submissions, CancellationToken ct = default)
    {
        foreach (var submission in submissions)
            await UpdateAsync(submission, ct);
    }
}
