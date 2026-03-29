using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Domain.Entity;
using Domain.ValueObject;
using SqliteDataAccess.PersistenceModel;

namespace SqliteDataAccess.Repository;

internal static class SqliteEntityMapper
{
    private const string SubmissionTestCaseResultsFieldName = "_testCaseResults";
    private const string SubmissionRubricResultsFieldName = "_rubricResults";

    private static readonly PropertyInfo ClassroomJoinCodeProperty =
        GetRequiredProperty<Classroom>(nameof(Classroom.JoinCode));

    private static readonly PropertyInfo AssignmentAllowedLanguagesProperty =
        GetRequiredProperty<Assignment>(nameof(Assignment.AllowedLanguages));

    private static readonly PropertyInfo AssignmentTimeLimitMsProperty =
        GetRequiredProperty<Assignment>(nameof(Assignment.TimeLimitMs));

    private static readonly PropertyInfo AssignmentMemoryLimitKbProperty =
        GetRequiredProperty<Assignment>(nameof(Assignment.MemoryLimitKb));

    private static readonly PropertyInfo AssignmentMaxSubmissionsProperty =
        GetRequiredProperty<Assignment>(nameof(Assignment.MaxSubmissions));

    private static readonly PropertyInfo AssignmentPublishStatusProperty =
        GetRequiredProperty<Assignment>(nameof(Assignment.PublishStatus));

    private static readonly PropertyInfo SubmissionSubmitTimeProperty =
        GetRequiredProperty<Submission>(nameof(Submission.SubmitTime));

    private static readonly PropertyInfo SubmissionStatusProperty =
        GetRequiredProperty<Submission>(nameof(Submission.Status));

    private static readonly PropertyInfo SubmissionTotalScoreProperty =
        GetRequiredProperty<Submission>(nameof(Submission.TotalScore));

    private static readonly FieldInfo SubmissionTestCaseResultsField =
        GetRequiredField<Submission>(SubmissionTestCaseResultsFieldName);

    private static readonly FieldInfo SubmissionRubricResultsField =
        GetRequiredField<Submission>(SubmissionRubricResultsFieldName);

    public static UserRecord ToRecord(User user) => new()
    {
        Id = user.Id.Value,
        Email = user.Email,
        FullName = user.FullName,
        Role = (int)user.Role
    };

    public static User ToDomain(UserRecord record)
    {
        var user = new User(new UserId(record.Id), record.Email, record.FullName, (UserRole)record.Role);
        user.ClearDomainEvents();
        return user;
    }

    public static ClassroomRecord ToRecord(Classroom classroom) => new()
    {
        Id = classroom.Id.Value,
        JoinCode = classroom.JoinCode,
        Name = classroom.Name,
        TeacherId = classroom.TeacherId.Value
    };

    public static Classroom ToDomain(ClassroomRecord record, IReadOnlyList<Guid> studentIds)
    {
        var classroom = new Classroom(new ClassroomId(record.Id), record.Name, new UserId(record.TeacherId));
        SetPropertyValue(classroom, ClassroomJoinCodeProperty, record.JoinCode);

        foreach (var studentId in studentIds.Distinct())
        {
            classroom.AddStudent(new UserId(studentId));
        }

        classroom.ClearDomainEvents();
        return classroom;
    }

    public static AssignmentRecord ToRecord(Assignment assignment) => new()
    {
        Id = assignment.Id.Value,
        ClassroomId = assignment.ClassroomId.Value,
        Title = assignment.Title,
        Description = assignment.Description,
        AllowedLanguages = assignment.AllowedLanguages,
        DueDate = assignment.DueDate,
        PublishStatus = (int)assignment.PublishStatus,
        TimeLimitMs = assignment.TimeLimitMs,
        MemoryLimitKb = assignment.MemoryLimitKb,
        MaxSubmissions = assignment.MaxSubmissions,
        GradingType = (int)assignment.GradingType
    };

    public static Assignment ToDomain(
        AssignmentRecord record,
        IReadOnlyList<TestCaseRecord> testCaseRecords,
        RubricRecord? rubricRecord)
    {
        var assignment = new Assignment(
            new AssignmentId(record.Id),
            new ClassroomId(record.ClassroomId),
            record.Title,
            record.Description,
            record.DueDate,
            (GradingType)record.GradingType);

        SetPropertyValue(assignment, AssignmentAllowedLanguagesProperty, record.AllowedLanguages);
        SetPropertyValue(assignment, AssignmentTimeLimitMsProperty, record.TimeLimitMs);
        SetPropertyValue(assignment, AssignmentMemoryLimitKbProperty, record.MemoryLimitKb);
        SetPropertyValue(assignment, AssignmentMaxSubmissionsProperty, record.MaxSubmissions);

        if ((GradingType)record.GradingType == GradingType.TestCase)
        {
            foreach (var testCaseRecord in testCaseRecords)
            {
                assignment.AddTestCase(ToDomain(testCaseRecord));
            }
        }

        if ((GradingType)record.GradingType == GradingType.Rubric && rubricRecord is not null)
        {
            assignment.SetRubric(ToDomain(rubricRecord));
        }

        SetPropertyValue(assignment, AssignmentPublishStatusProperty, (PublishStatus)record.PublishStatus);
        assignment.ClearDomainEvents();
        return assignment;
    }

    public static TestCaseRecord ToRecord(TestCase testCase) => new()
    {
        Id = testCase.Id.Value,
        AssignmentId = testCase.AssignmentId.Value,
        InputData = testCase.InputData,
        ExpectedOutput = testCase.ExpectedOutput,
        IsHidden = testCase.IsHidden,
        ScoreWeight = testCase.ScoreWeight
    };

    public static TestCase ToDomain(TestCaseRecord record)
        => new(
            new TestCaseId(record.Id),
            new AssignmentId(record.AssignmentId),
            record.InputData,
            record.ExpectedOutput,
            record.IsHidden,
            record.ScoreWeight);

    public static RubricRecord ToRecord(Rubric rubric) => new()
    {
        Id = rubric.Id.Value,
        AssignmentId = rubric.AssignmentId.Value,
        CriteriaListJson = rubric.CriteriaListJson
    };

    public static Rubric ToDomain(RubricRecord record)
        => new(new RubricId(record.Id), new AssignmentId(record.AssignmentId), record.CriteriaListJson);

    public static SubmissionRecord ToRecord(Submission submission) => new()
    {
        Id = submission.Id.Value,
        AssignmentId = submission.AssignmentId.Value,
        StudentId = submission.StudentId.Value,
        SourceCode = submission.SourceCode,
        Language = submission.Language,
        SubmitTime = submission.SubmitTime,
        Status = (int)submission.Status,
        TotalScore = submission.TotalScore
    };

    public static Submission ToDomain(
        SubmissionRecord record,
        IReadOnlyList<SubmissionTestCaseResultRecord> testCaseResultRecords,
        IReadOnlyList<SubmissionRubricResultRecord> rubricResultRecords)
    {
        var submission = new Submission(
            new SubmissionId(record.Id),
            new AssignmentId(record.AssignmentId),
            new UserId(record.StudentId),
            record.SourceCode,
            record.Language);

        SetPropertyValue(submission, SubmissionSubmitTimeProperty, record.SubmitTime);
        SetPropertyValue(submission, SubmissionStatusProperty, (SubmissionStatus)record.Status);
        SetPropertyValue(submission, SubmissionTotalScoreProperty, record.TotalScore);

        var testCaseResults = testCaseResultRecords
            .OrderBy(x => x.SortOrder)
            .Select(x => new TestCaseResult(
                new TestCaseId(x.TestCaseId),
                x.ActualOutput,
                x.ExecutionTimeMs,
                x.MemoryUsedKb,
                (TestCaseStatus)x.Status))
            .ToList();

        var rubricResults = rubricResultRecords
            .OrderBy(x => x.SortOrder)
            .Select(x => new RubricResult(x.CriteriaName, x.GivenScore, x.CommentReason))
            .ToList();

        SetFieldValue(submission, SubmissionTestCaseResultsField, testCaseResults);
        SetFieldValue(submission, SubmissionRubricResultsField, rubricResults);

        submission.ClearDomainEvents();
        return submission;
    }

    public static SubmissionTestCaseResultRecord ToRecord(Guid submissionId, TestCaseResult result, int sortOrder) => new()
    {
        SubmissionId = submissionId,
        TestCaseId = result.TestCaseId.Value,
        ActualOutput = result.ActualOutput,
        ExecutionTimeMs = result.ExecutionTimeMs,
        MemoryUsedKb = result.MemoryUsedKb,
        Status = (int)result.Status,
        SortOrder = sortOrder
    };

    public static SubmissionRubricResultRecord ToRecord(Guid submissionId, RubricResult result, int sortOrder) => new()
    {
        SubmissionId = submissionId,
        CriteriaName = result.CriteriaName,
        GivenScore = result.GivenScore,
        CommentReason = result.CommentReason,
        SortOrder = sortOrder
    };

    private static PropertyInfo GetRequiredProperty<TTarget>(string propertyName)
    {
        var propertyInfo = typeof(TTarget).GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (propertyInfo is null)
        {
            throw new InvalidOperationException($"Property '{propertyName}' not found on {typeof(TTarget).Name}.");
        }

        return propertyInfo;
    }

    private static FieldInfo GetRequiredField<TTarget>(string fieldName)
    {
        var fieldInfo = typeof(TTarget).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);

        if (fieldInfo is null)
        {
            throw new InvalidOperationException($"Field '{fieldName}' not found on {typeof(TTarget).Name}.");
        }

        return fieldInfo;
    }

    private static void SetPropertyValue<TTarget, TValue>(TTarget target, PropertyInfo propertyInfo, TValue value)
    {
        propertyInfo.SetValue(target, value);
    }

    private static void SetFieldValue<TTarget>(TTarget target, FieldInfo fieldInfo, object value)
    {
        fieldInfo.SetValue(target, value);
    }
}
