using System;

namespace SqliteDataAccess.PersistenceModel;

public sealed class RubricRecord
{
    public Guid Id { get; set; }
    public Guid AssignmentId { get; set; }
    public string CriteriaListJson { get; set; } = string.Empty;
}
