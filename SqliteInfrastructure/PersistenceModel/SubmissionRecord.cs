using System;

namespace SqliteDataAccess.PersistenceModel;

public sealed class SubmissionRecord
{
    public Guid Id { get; set; }
    public Guid AssignmentId { get; set; }
    public Guid StudentId { get; set; }
    public string SourceCode { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public DateTime SubmitTime { get; set; }
    public int Status { get; set; }
    public double TotalScore { get; set; }
}
