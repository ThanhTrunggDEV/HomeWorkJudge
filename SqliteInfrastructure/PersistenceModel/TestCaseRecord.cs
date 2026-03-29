using System;

namespace SqliteDataAccess.PersistenceModel;

public sealed class TestCaseRecord
{
    public Guid Id { get; set; }
    public Guid AssignmentId { get; set; }
    public string InputData { get; set; } = string.Empty;
    public string ExpectedOutput { get; set; } = string.Empty;
    public bool IsHidden { get; set; }
    public double ScoreWeight { get; set; }
}
