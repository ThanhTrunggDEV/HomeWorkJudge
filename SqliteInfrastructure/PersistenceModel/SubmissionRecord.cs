using System;
using System.Collections.Generic;

namespace SqliteDataAccess.PersistenceModel;

public sealed class SubmissionRecord
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public string StudentIdentifier { get; set; } = null!;

    /// <summary>JSON array of {FileName, Content} objects.</summary>
    public string SourceFilesJson { get; set; } = "[]";

    public DateTime ImportedAt { get; set; }

    /// <summary>Stored as string: Pending / Grading / AIGraded / Reviewed / Error.</summary>
    public string Status { get; set; } = "Pending";

    public double TotalScore { get; set; }
    public string? TeacherNote { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsPlagiarismSuspected { get; set; }
    public double? MaxSimilarityPercentage { get; set; }

    // Navigation
    public List<RubricResultRecord> RubricResults { get; set; } = [];
}

public sealed class RubricResultRecord
{
    public int Id { get; set; }          // auto-increment surrogate key
    public Guid SubmissionId { get; set; }
    public string CriteriaName { get; set; } = null!;
    public double GivenScore { get; set; }
    public double MaxScore { get; set; }
    public string Comment { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
