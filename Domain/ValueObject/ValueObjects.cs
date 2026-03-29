using System;

namespace Domain.ValueObject;

public readonly record struct UserId(Guid Value);
public readonly record struct ClassroomId(Guid Value);
public readonly record struct AssignmentId(Guid Value);
public readonly record struct TestCaseId(Guid Value);
public readonly record struct RubricId(Guid Value);
public readonly record struct SubmissionId(Guid Value);

public enum UserRole { Student, Teacher, Admin }
public enum PublishStatus { Draft, Published }
public enum GradingType { TestCase, Rubric }
public enum SubmissionStatus { Pending, Executing, Done }
public enum TestCaseStatus { Passed, Failed, TimeOut, RuntimeError }

public record TestCaseResult(TestCaseId TestCaseId, string ActualOutput, long ExecutionTimeMs, long MemoryUsedKb, TestCaseStatus Status);
public record RubricResult(string CriteriaName, double GivenScore, string CommentReason);
