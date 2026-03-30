using System;
using System.Collections.Generic;
using Ports.DTO.Assignment;
using Ports.DTO.Common;

namespace Ports.DTO.Classroom;

public sealed record CreateClassroomRequestDto(string Name, Guid TeacherId);

public sealed record CreateClassroomResponseDto(Guid ClassroomId, string JoinCode);

public sealed record JoinClassroomRequestDto(string JoinCode, Guid StudentId);

public sealed record JoinClassroomResponseDto(Guid ClassroomId, Guid StudentId, string Status);

public sealed record ClassroomMemberDto(Guid UserId, string FullName, UserRoleDto Role);

public sealed record ClassroomOverviewDto(
	Guid ClassroomId,
	string Name,
	string JoinCode,
	Guid TeacherId,
	string TeacherName,
	int StudentCount,
	IReadOnlyList<ClassroomMemberDto> Members,
	IReadOnlyList<AssignmentListItemDto> Assignments);

public sealed record AuthorizedClassroomOverviewResponseDto(
	ResourceAccessDecisionDto AccessDecision,
	ClassroomOverviewDto? Classroom);
