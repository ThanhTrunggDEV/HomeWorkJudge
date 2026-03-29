using System;

namespace Ports.DTO.Classroom;

public sealed record CreateClassroomRequestDto(string Name, Guid TeacherId);

public sealed record CreateClassroomResponseDto(Guid ClassroomId, string JoinCode);

public sealed record JoinClassroomRequestDto(string JoinCode, Guid StudentId);

public sealed record JoinClassroomResponseDto(Guid ClassroomId, Guid StudentId, string Status);
