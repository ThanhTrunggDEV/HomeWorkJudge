using System;

namespace SqliteDataAccess.PersistenceModel;

public sealed class AssignmentRecord
{
    public Guid Id { get; set; }
    public Guid ClassroomId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string AllowedLanguages { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public int PublishStatus { get; set; }
    public long TimeLimitMs { get; set; }
    public long MemoryLimitKb { get; set; }
    public int MaxSubmissions { get; set; }
    public int GradingType { get; set; }
}
