using System;

namespace SqliteDataAccess.PersistenceModel;

public sealed class GradingSessionRecord
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public Guid RubricId { get; set; }
    public DateTime CreatedAt { get; set; }
}
