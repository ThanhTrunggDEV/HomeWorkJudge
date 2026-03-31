using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domain.Entity;
using Domain.Ports;
using Domain.ValueObject;
using Microsoft.EntityFrameworkCore;

namespace SqliteDataAccess.Repository;

public sealed class SqliteGradingSessionRepository : IGradingSessionRepository
{
    private readonly AppDbContext _db;

    public SqliteGradingSessionRepository(AppDbContext db) => _db = db;

    public async Task<GradingSession?> GetByIdAsync(GradingSessionId id, CancellationToken ct = default)
    {
        var record = await _db.GradingSessions.FirstOrDefaultAsync(s => s.Id == id.Value, ct);
        return record is null ? null : EntityMapper.ToDomain(record);
    }

    public async Task<IReadOnlyList<GradingSession>> GetAllAsync(CancellationToken ct = default)
    {
        var records = await _db.GradingSessions
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);

        return records.Select(EntityMapper.ToDomain).ToList();
    }

    public async Task AddAsync(GradingSession session, CancellationToken ct = default)
    {
        var record = EntityMapper.ToRecord(session);
        await _db.GradingSessions.AddAsync(record, ct);
    }

    public async Task UpdateAsync(GradingSession session, CancellationToken ct = default)
    {
        var existing = await _db.GradingSessions.FindAsync([session.Id.Value], ct);
        if (existing is null) return;

        existing.Name = session.Name;
        existing.RubricId = session.RubricId.Value;
    }

    public async Task DeleteAsync(GradingSessionId id, CancellationToken ct = default)
    {
        var record = await _db.GradingSessions.FindAsync([id.Value], ct);
        if (record is not null)
            _db.GradingSessions.Remove(record);
    }
}
