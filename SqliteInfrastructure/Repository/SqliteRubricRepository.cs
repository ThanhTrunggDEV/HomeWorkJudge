using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domain.Entity;
using Domain.Ports;
using Domain.ValueObject;
using Microsoft.EntityFrameworkCore;

namespace SqliteDataAccess.Repository;

public sealed class SqliteRubricRepository : IRubricRepository
{
    private readonly AppDbContext _db;

    public SqliteRubricRepository(AppDbContext db) => _db = db;

    public async Task<Rubric?> GetByIdAsync(RubricId id, CancellationToken ct = default)
    {
        var record = await _db.Rubrics
            .Include(r => r.Criteria)
            .FirstOrDefaultAsync(r => r.Id == id.Value, ct);

        return record is null ? null : EntityMapper.ToDomain(record);
    }

    public async Task<IReadOnlyList<Rubric>> GetAllAsync(CancellationToken ct = default)
    {
        var records = await _db.Rubrics
            .Include(r => r.Criteria)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        return records.Select(EntityMapper.ToDomain).ToList();
    }

    public async Task<IReadOnlyList<Rubric>> SearchByNameAsync(string keyword, CancellationToken ct = default)
    {
        var records = await _db.Rubrics
            .Include(r => r.Criteria)
            .Where(r => r.Name.Contains(keyword))
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        return records.Select(EntityMapper.ToDomain).ToList();
    }

    public async Task AddAsync(Rubric rubric, CancellationToken ct = default)
    {
        var record = EntityMapper.ToRecord(rubric);
        await _db.Rubrics.AddAsync(record, ct);
    }

    public async Task UpdateAsync(Rubric rubric, CancellationToken ct = default)
    {
        var existing = await _db.Rubrics
            .Include(r => r.Criteria)
            .FirstOrDefaultAsync(r => r.Id == rubric.Id.Value, ct);

        if (existing is null) return;

        var updated = EntityMapper.ToRecord(rubric);
        existing.Name = updated.Name;

        // Sync criteria: remove old, add new → đơn giản, criteria không nhiều
        _db.RubricCriteria.RemoveRange(existing.Criteria);
        existing.Criteria = updated.Criteria;
        foreach (var c in existing.Criteria)
            c.RubricId = existing.Id;
    }

    public async Task DeleteAsync(RubricId id, CancellationToken ct = default)
    {
        var record = await _db.Rubrics.FindAsync([id.Value], ct);
        if (record is not null)
            _db.Rubrics.Remove(record);
    }
}
