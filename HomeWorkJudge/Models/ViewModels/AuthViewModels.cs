using System;
using System.ComponentModel.DataAnnotations;
using Ports.DTO.Common;

namespace HomeWorkJudge.Models.ViewModels;

public sealed class LoginViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}

public sealed class RegisterViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(120)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 8)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}

public sealed class ProfileViewModel
{
    public Guid UserId { get; set; }

    public string Email { get; set; } = string.Empty;

    public UserRoleDto Role { get; set; }

    public DateTimeOffset? SessionExpiresAt { get; set; }
}
