using System;

namespace SqliteDataAccess.PersistenceModel;

public sealed class SubmissionRubricResultRecord
{
    public Guid SubmissionId { get; set; }
    public string CriteriaName { get; set; } = string.Empty;
    public double GivenScore { get; set; }
    public string CommentReason { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
