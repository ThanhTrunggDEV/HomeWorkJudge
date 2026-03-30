using System;
using System.Collections.Generic;
using Ports.DTO.Common;
using Ports.DTO.Rubric;

namespace Ports.DTO.Assignment;

public sealed record CreateAssignmentRequestDto(
    Guid ClassroomId,
    Guid RequestedByUserId,
    string Title,
    string Description,
    IReadOnlyList<string> AllowedLanguages,
    DateTime DueDate,
    AssignmentGradingTypeDto GradingType,
    long TimeLimitMs,
    long MemoryLimitKb,
    int MaxSubmissions);

public sealed record CreateAssignmentResponseDto(Guid AssignmentId, AssignmentPublishStatusDto Status);

public sealed record UpdateAssignmentRequestDto(
    Guid AssignmentId,
    Guid RequestedByUserId,
    string Title,
    string Description,
    DateTime DueDate,
    IReadOnlyList<string> AllowedLanguages,
    long TimeLimitMs,
    long MemoryLimitKb,
    int MaxSubmissions);

public sealed record PublishAssignmentRequestDto(Guid AssignmentId, Guid RequestedByUserId);

public sealed record AssignmentTestCaseDto(
    Guid TestCaseId,
    string InputData,
    string ExpectedOutput,
    bool IsHidden,
    double ScoreWeight);

public sealed record AddAssignmentTestCaseRequestDto(
    Guid AssignmentId,
    string InputData,
    string ExpectedOutput,
    bool IsHidden,
    double ScoreWeight);

public sealed record UpdateAssignmentTestCaseRequestDto(
    Guid AssignmentId,
    Guid TestCaseId,
    string InputData,
    string ExpectedOutput,
    bool IsHidden,
    double ScoreWeight);

public sealed record DeleteAssignmentTestCaseRequestDto(Guid AssignmentId, Guid TestCaseId);

public sealed record CreateAssignmentRubricRequestDto(
    Guid AssignmentId,
    IReadOnlyList<RubricCriteriaDto> Criteria);

public sealed record UpdateAssignmentRubricRequestDto(
    Guid AssignmentId,
    IReadOnlyList<RubricCriteriaDto> Criteria);

public sealed record AssignmentListItemDto(
    Guid AssignmentId,
    Guid ClassroomId,
    string Title,
    DateTime DueDate,
    AssignmentPublishStatusDto PublishStatus,
    AssignmentGradingTypeDto GradingType);

public sealed record AssignmentDetailDto(
    Guid AssignmentId,
    Guid ClassroomId,
    string Title,
    string Description,
    IReadOnlyList<string> AllowedLanguages,
    DateTime DueDate,
    AssignmentPublishStatusDto PublishStatus,
    AssignmentGradingTypeDto GradingType,
    long TimeLimitMs,
    long MemoryLimitKb,
    int MaxSubmissions);
