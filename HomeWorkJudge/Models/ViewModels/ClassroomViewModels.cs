using System;
using System.ComponentModel.DataAnnotations;

namespace HomeWorkJudge.Models.ViewModels;

public sealed class ClassroomIndexViewModel
{
    public CreateClassroomViewModel CreateForm { get; set; } = new();

    public JoinClassroomViewModel JoinForm { get; set; } = new();

    public Guid? LastClassroomId { get; set; }

    public string? LastJoinCode { get; set; }
}

public sealed class CreateClassroomViewModel
{
    [Required]
    [StringLength(160)]
    public string Name { get; set; } = string.Empty;
}

public sealed class JoinClassroomViewModel
{
    [Required]
    [StringLength(16)]
    public string JoinCode { get; set; } = string.Empty;
}
