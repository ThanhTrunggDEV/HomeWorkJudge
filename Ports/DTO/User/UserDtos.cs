using System;
using Ports.DTO.Common;

namespace Ports.DTO.User;

public sealed record RegisterUserRequestDto(
    string Email,
    string FullName,
    UserRoleDto Role,
    string Password);

public sealed record RegisterUserResponseDto(
    Guid UserId,
    string Email,
    UserRoleDto Role);

public sealed record AssignUserRoleRequestDto(
    Guid UserId,
    UserRoleDto Role);

public sealed record LoginRequestDto(string Email, string Password);

public sealed record LoginResponseDto(Guid UserId, string AccessToken, DateTime ExpiresAt);
