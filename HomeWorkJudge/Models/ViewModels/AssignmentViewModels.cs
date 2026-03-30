using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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

public sealed class CreateAssignmentViewModel
{
    [Required]
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
    [Required]
    public Guid AssignmentId { get; set; }

    [Required]
    public Guid ClassroomId { get; set; }
}
