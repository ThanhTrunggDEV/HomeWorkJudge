using System;
using System.Collections.Generic;
using Ports.DTO.AI;
using Ports.DTO.Common;
using Ports.DTO.Rubric;

namespace Ports.DTO.Submission;

public sealed record SubmitCodeRequestDto(
    Guid AssignmentId,
    Guid StudentId,
    string SourceCode,
    string Language);

public sealed record SubmitCodeResponseDto(
    Guid SubmissionId,
    SubmissionStatusDto Status,
    DateTime SubmittedAt);

public sealed record TestCaseExecutionResultDto(
    Guid TestCaseId,
    TestCaseExecutionStatusDto Status,
    string ActualOutput,
    long ExecutionTimeMs,
    long MemoryUsedKb);

public sealed record SubmissionDetailDto(
    Guid SubmissionId,
    Guid AssignmentId,
    Guid StudentId,
    SubmissionStatusDto Status,
    double TotalScore,
    DateTime SubmittedAt,
    IReadOnlyList<TestCaseExecutionResultDto> TestCaseResults,
    IReadOnlyList<RubricScoreDto> RubricResults,
    AiFeedbackDto? Feedback);

public sealed record CodeCompilationResultDto(
    bool Success,
    string CompilerOutput,
    string? ArtifactPath);

public sealed record CodeExecutionRequestDto(
    string ArtifactPath,
    string Input,
    long TimeLimitMs,
    long MemoryLimitKb);

public sealed record CodeExecutionResultDto(
    string ActualOutput,
    long ExecutionTimeMs,
    long MemoryUsedKb,
    bool TimedOut,
    bool RuntimeError,
    string? RuntimeMessage);

public sealed record TestCaseJudgeItemDto(
    Guid TestCaseId,
    string Input,
    string ExpectedOutput);

public sealed record TestCaseJudgeRequestDto(
    Guid SubmissionId,
    Guid AssignmentId,
    string Language,
    string SourceCode,
    IReadOnlyList<TestCaseJudgeItemDto> TestCases,
    long TimeLimitMs,
    long MemoryLimitKb);
