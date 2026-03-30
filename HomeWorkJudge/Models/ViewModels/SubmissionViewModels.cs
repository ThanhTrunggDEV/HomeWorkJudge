using System;
using System.ComponentModel.DataAnnotations;
using HomeWorkJudge.Validation;
using Ports.DTO.Submission;

namespace HomeWorkJudge.Models.ViewModels;

public sealed class SubmitCodeViewModel
{
    [NotEmptyGuid]
    public Guid AssignmentId { get; set; }

    [Required]
    [StringLength(40)]
    public string Language { get; set; } = "csharp";

    [Required]
    [StringLength(100000)]
    public string SourceCode { get; set; } = string.Empty;
}

public sealed class SubmissionDetailPageViewModel
{
    public SubmissionDetailDto? Submission { get; set; }

    public Guid SubmissionId { get; set; }
}

public sealed class GradingPanelViewModel
{
    [NotEmptyGuid]
    public Guid SubmissionId { get; set; }

    [Range(0, 1000)]
    public double NewTotalScore { get; set; }

    public string OverrideReason { get; set; } = string.Empty;

    public bool IsApproved { get; set; } = true;

    [StringLength(2000)]
    public string TeacherComment { get; set; } = string.Empty;

    [StringLength(180)]
    public string CriteriaName { get; set; } = string.Empty;

    [Range(0, 1000)]
    public double NewCriteriaScore { get; set; }

    [StringLength(2000)]
    public string CriteriaOverrideReason { get; set; } = string.Empty;

    [StringLength(500)]
    public string StudentQuestion { get; set; } = string.Empty;
}
