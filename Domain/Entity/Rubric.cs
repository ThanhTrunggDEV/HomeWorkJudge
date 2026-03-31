using System;
using System.Collections.Generic;
using System.Linq;
using Domain.ValueObject;

namespace Domain.Entity;

/// <summary>
/// Aggregate Root: Rubric chấm điểm.
/// Độc lập, tái sử dụng cho nhiều GradingSession.
/// </summary>
public class Rubric : EntityBase
{
    public RubricId Id { get; private set; }
    public string Name { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }

    private readonly List<RubricCriteria> _criteria = [];
    public IReadOnlyList<RubricCriteria> Criteria => _criteria.AsReadOnly();

    

    public Rubric(RubricId id, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tên rubric không được rỗng.", nameof(name));

        Id = id;
        Name = name.Trim();
        CreatedAt = DateTime.UtcNow;
    }

    // ── Criteria management ──────────────────────────────────────────────────

    public RubricCriteria AddCriteria(string name, double maxScore, string description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tên tiêu chí không được rỗng.", nameof(name));
        if (maxScore <= 0)
            throw new ArgumentException("Điểm tối đa phải lớn hơn 0.", nameof(maxScore));
        if (_criteria.Any(c => c.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Tiêu chí '{name}' đã tồn tại trong rubric.");

        var criteria = new RubricCriteria(
            Id: new RubricCriteriaId(Guid.NewGuid()),
            Name: name.Trim(),
            MaxScore: maxScore,
            Description: description ?? string.Empty,
            SortOrder: _criteria.Count
        );

        _criteria.Add(criteria);
        return criteria;
    }

    public void UpdateCriteria(RubricCriteriaId criteriaId, string name, double maxScore, string description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tên tiêu chí không được rỗng.", nameof(name));
        if (maxScore <= 0)
            throw new ArgumentException("Điểm tối đa phải lớn hơn 0.", nameof(maxScore));

        var index = FindCriteriaIndex(criteriaId);

        if (_criteria.Any(c => c.Id != criteriaId &&
                               c.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Tiêu chí '{name}' đã tồn tại trong rubric.");

        _criteria[index] = _criteria[index] with
        {
            Name = name.Trim(),
            MaxScore = maxScore,
            Description = description ?? string.Empty
        };
    }

    public void RemoveCriteria(RubricCriteriaId criteriaId)
    {
        var index = FindCriteriaIndex(criteriaId);
        _criteria.RemoveAt(index);
        ResetSortOrders();
    }

    public void ReorderCriteria(IReadOnlyList<RubricCriteriaId> orderedIds)
    {
        if (orderedIds.Count != _criteria.Count)
            throw new ArgumentException("Danh sách Id không khớp với số tiêu chí hiện có.");

        for (int i = 0; i < orderedIds.Count; i++)
        {
            var index = FindCriteriaIndex(orderedIds[i]);
            _criteria[index] = _criteria[index] with { SortOrder = i };
        }

        _criteria.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
    }

    // ── Computed ─────────────────────────────────────────────────────────────

    public double GetMaxTotalScore() => _criteria.Sum(c => c.MaxScore);

    // ── Clone ────────────────────────────────────────────────────────────────

    public Rubric Clone(string newName)
    {
        var clone = new Rubric(new RubricId(Guid.NewGuid()), newName);
        foreach (var c in _criteria.OrderBy(x => x.SortOrder))
            clone.AddCriteria(c.Name, c.MaxScore, c.Description);
        return clone;
    }

    // ── Rename ───────────────────────────────────────────────────────────────

    public void Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("Tên rubric không được rỗng.", nameof(newName));
        Name = newName.Trim();
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private int FindCriteriaIndex(RubricCriteriaId id)
    {
        var index = _criteria.FindIndex(c => c.Id == id);
        if (index < 0)
            throw new InvalidOperationException($"Không tìm thấy tiêu chí Id={id.Value}.");
        return index;
    }

    private void ResetSortOrders()
    {
        for (int i = 0; i < _criteria.Count; i++)
            _criteria[i] = _criteria[i] with { SortOrder = i };
    }
}
