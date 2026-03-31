using System;
using Domain.ValueObject;

namespace Domain.Entity;

/// <summary>
/// Aggregate Root: Phiên chấm bài.
/// Một phiên = tên session + 1 Rubric (by reference) + nhiều Submission (by reference qua SessionId).
/// Không giữ List&lt;Submission&gt; trực tiếp để tránh aggregate quá nặng.
/// </summary>
public class GradingSession : EntityBase
{
    public GradingSessionId Id { get; private set; }
    public string Name { get; private set; }

    /// <summary>Reference tới Rubric dùng cho phiên chấm này.</summary>
    public RubricId RubricId { get; private set; }

    public DateTime CreatedAt { get; private set; }


    public GradingSession(GradingSessionId id, string name, RubricId rubricId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Tên phiên chấm không được rỗng.", nameof(name));

        Id = id;
        Name = name.Trim();
        RubricId = rubricId;
        CreatedAt = DateTime.UtcNow;
    }

    public void Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("Tên phiên chấm không được rỗng.", nameof(newName));
        Name = newName.Trim();
    }
}
