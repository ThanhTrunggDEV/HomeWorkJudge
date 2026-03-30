using System;
using System.Collections.Generic;
using Ports.DTO.Common;
using Ports.DTO.Report;
using Ports.DTO.Submission;

namespace HomeWorkJudge.Models.ViewModels;

public sealed class ScoreboardViewModel
{
    public Guid ClassroomId { get; set; }

    public Guid? AssignmentId { get; set; }

    public IReadOnlyList<ScoreboardItemDto> Items { get; set; } = Array.Empty<ScoreboardItemDto>();
}

public sealed class SubmissionHistoryViewModel
{
    public Guid StudentId { get; set; }

    public PagedResponseDto<SubmissionDetailDto>? Page { get; set; }
}
