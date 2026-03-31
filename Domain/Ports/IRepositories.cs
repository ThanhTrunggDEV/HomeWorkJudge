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

// ── Rubric AR ────────────────────────────────────────────────────────────────
public interface IRubricRepository
{
    Task<Rubric?> GetByIdAsync(RubricId id, CancellationToken ct = default);
    Task<IReadOnlyList<Rubric>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Rubric>> SearchByNameAsync(string keyword, CancellationToken ct = default);
    Task AddAsync(Rubric rubric, CancellationToken ct = default);
    Task UpdateAsync(Rubric rubric, CancellationToken ct = default);
    Task DeleteAsync(RubricId id, CancellationToken ct = default);
}

// ── GradingSession AR ────────────────────────────────────────────────────────
public interface IGradingSessionRepository
{
    Task<GradingSession?> GetByIdAsync(GradingSessionId id, CancellationToken ct = default);
    Task<IReadOnlyList<GradingSession>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(GradingSession session, CancellationToken ct = default);
    Task UpdateAsync(GradingSession session, CancellationToken ct = default);
    Task DeleteAsync(GradingSessionId id, CancellationToken ct = default);
}

// ── Submission AR ────────────────────────────────────────────────────────────
public interface ISubmissionRepository
{
    Task<Submission?> GetByIdAsync(SubmissionId id, CancellationToken ct = default);
    Task<IReadOnlyList<Submission>> GetBySessionIdAsync(GradingSessionId sessionId, CancellationToken ct = default);
    Task<IReadOnlyList<Submission>> GetByStatusAsync(GradingSessionId sessionId, SubmissionStatus status, CancellationToken ct = default);
    Task<int> CountBySessionIdAsync(GradingSessionId sessionId, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<Submission> submissions, CancellationToken ct = default);
    Task UpdateAsync(Submission submission, CancellationToken ct = default);
    Task UpdateRangeAsync(IEnumerable<Submission> submissions, CancellationToken ct = default);
}
