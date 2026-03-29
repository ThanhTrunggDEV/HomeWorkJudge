using System;

namespace SqliteDataAccess.PersistenceModel;

public sealed class SubmissionTestCaseResultRecord
{
    public Guid SubmissionId { get; set; }
    public Guid TestCaseId { get; set; }
    public string ActualOutput { get; set; } = string.Empty;
    public long ExecutionTimeMs { get; set; }
    public long MemoryUsedKb { get; set; }
    public int Status { get; set; }
    public int SortOrder { get; set; }
}
