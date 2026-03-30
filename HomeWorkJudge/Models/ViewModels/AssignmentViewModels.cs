using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using HomeWorkJudge.Validation;
using Ports.DTO.Assignment;
using Ports.DTO.Common;

namespace HomeWorkJudge.Models.ViewModels;

public sealed class AssignmentListPageViewModel
{
    public Guid ClassroomId { get; set; }

    public int PageNumber { get; set; }

    public int PageSize { get; set; }

    public long TotalCount { get; set; }

    public IReadOnlyList<AssignmentListItemDto> Items { get; set; } = Array.Empty<AssignmentListItemDto>();
}

public sealed class AssignmentDetailPageViewModel
{
    public Ports.DTO.Assignment.AssignmentDetailDto? Assignment { get; set; }

    public AddAssignmentTestCaseViewModel AddTestCaseForm { get; set; } = new();

    public UpsertAssignmentRubricViewModel RubricForm { get; set; } = new();
}

public sealed class CreateAssignmentViewModel
{
    [NotEmptyGuid]
    public Guid ClassroomId { get; set; }

    [Required]
    [StringLength(180)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(4000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public string AllowedLanguagesCsv { get; set; } = "csharp";

    [Required]
    public DateTime DueDate { get; set; } = DateTime.UtcNow.AddDays(7);

    [Required]
    public AssignmentGradingTypeDto GradingType { get; set; } = AssignmentGradingTypeDto.TestCase;

    [Range(1, long.MaxValue)]
    public long TimeLimitMs { get; set; } = 1000;

    [Range(1, long.MaxValue)]
    public long MemoryLimitKb { get; set; } = 65536;

    [Range(1, int.MaxValue)]
    public int MaxSubmissions { get; set; } = 10;
}

public sealed class PublishAssignmentViewModel
{
    [NotEmptyGuid]
    public Guid AssignmentId { get; set; }

    [NotEmptyGuid]
    public Guid ClassroomId { get; set; }
}

public sealed class EditAssignmentViewModel
{
    [NotEmptyGuid]
    public Guid AssignmentId { get; set; }

    [NotEmptyGuid]
    public Guid ClassroomId { get; set; }

    [Required]
    [StringLength(180)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(4000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public string AllowedLanguagesCsv { get; set; } = "csharp";

    [Required]
    public DateTime DueDate { get; set; }

    [Range(1, long.MaxValue)]
    public long TimeLimitMs { get; set; }

    [Range(1, long.MaxValue)]
    public long MemoryLimitKb { get; set; }

    [Range(1, int.MaxValue)]
    public int MaxSubmissions { get; set; }
}

public sealed class AddAssignmentTestCaseViewModel
{
    [NotEmptyGuid]
    public Guid AssignmentId { get; set; }

    [Required]
    [StringLength(4000)]
    public string InputData { get; set; } = string.Empty;

    [Required]
    [StringLength(4000)]
    public string ExpectedOutput { get; set; } = string.Empty;

    public bool IsHidden { get; set; }

    [Range(0.0001, 1000)]
    public double ScoreWeight { get; set; } = 1;
}

public sealed class UpdateAssignmentTestCaseViewModel
{
    [NotEmptyGuid]
    public Guid AssignmentId { get; set; }

    [NotEmptyGuid]
    public Guid TestCaseId { get; set; }

    [Required]
    [StringLength(4000)]
    public string InputData { get; set; } = string.Empty;

    [Required]
    [StringLength(4000)]
    public string ExpectedOutput { get; set; } = string.Empty;

    public bool IsHidden { get; set; }

    [Range(0.0001, 1000)]
    public double ScoreWeight { get; set; } = 1;
}

public sealed class DeleteAssignmentTestCaseViewModel
{
    [NotEmptyGuid]
    public Guid AssignmentId { get; set; }

    [NotEmptyGuid]
    public Guid TestCaseId { get; set; }
}

public sealed class UpsertAssignmentRubricViewModel
{
    [NotEmptyGuid]
    public Guid AssignmentId { get; set; }

    [Required]
    [StringLength(16000)]
    public string CriteriaText { get; set; } = string.Empty;
}
