using System;
using System.Collections.Generic;

namespace SqliteDataAccess.PersistenceModel;

public sealed class RubricRecord
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public DateTime CreatedAt { get; set; }

    // Navigation
    public List<RubricCriteriaRecord> Criteria { get; set; } = [];
}

public sealed class RubricCriteriaRecord
{
    public Guid Id { get; set; }
    public Guid RubricId { get; set; }
    public string Name { get; set; } = null!;
    public double MaxScore { get; set; }
    public string Description { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
