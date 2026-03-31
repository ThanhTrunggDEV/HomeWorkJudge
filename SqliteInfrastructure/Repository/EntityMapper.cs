using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Domain.Entity;
using Domain.ValueObject;
using SqliteDataAccess.PersistenceModel;

namespace SqliteDataAccess.Repository;

/// <summary>
/// Chuyển đổi hai chiều giữa Domain entities và PersistenceModel records.
/// Dùng reflection để set private fields/properties — không ảnh hưởng Domain.
/// </summary>
internal static class EntityMapper
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    private static T CreateInstance<T>()
        => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));

    private static void SetProp<T>(T obj, string propName, object? value)
        => typeof(T)
            .GetProperty(propName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(obj, value);

    private static void SetField<T>(T obj, string fieldName, object? value)
    {
        Type? type = typeof(T);
        while (type is not null)
        {
            var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field is not null) { field.SetValue(obj, value); return; }
            type = type.BaseType;
        }
        throw new InvalidOperationException(
            $"Field '{fieldName}' not found on '{typeof(T).Name}' or its base types.");
    }

    // ── Rubric ────────────────────────────────────────────────────────────────

    public static RubricRecord ToRecord(Rubric rubric)
        => new()
        {
            Id = rubric.Id.Value,
            Name = rubric.Name,
            CreatedAt = rubric.CreatedAt,
            Criteria = rubric.Criteria.Select(ToRecord).ToList()
        };

    public static RubricCriteriaRecord ToRecord(RubricCriteria c)
        => new()
        {
            Id = c.Id.Value,
            Name = c.Name,
            MaxScore = c.MaxScore,
            Description = c.Description,
            SortOrder = c.SortOrder
        };

    public static Rubric ToDomain(RubricRecord r)
    {
        var rubric = CreateInstance<Rubric>();
        SetProp(rubric, "Id", new RubricId(r.Id));
        SetProp(rubric, "Name", r.Name);
        SetProp(rubric, "CreatedAt", r.CreatedAt);

        // Khởi tạo backing list (GetUninitializedObject không chạy constructor)
        var criteriaList = new System.Collections.Generic.List<RubricCriteria>();
        criteriaList.AddRange(r.Criteria.OrderBy(c => c.SortOrder).Select(ToDomain));
        SetField(rubric, "_criteria", criteriaList);

        // Khởi tạo event list từ EntityBase
        SetField(rubric, "_events", new System.Collections.Generic.List<Domain.Event.IDomainEvent>());

        return rubric;
    }

    public static RubricCriteria ToDomain(RubricCriteriaRecord c)
        => new(
            Id: new RubricCriteriaId(c.Id),
            Name: c.Name,
            MaxScore: c.MaxScore,
            Description: c.Description,
            SortOrder: c.SortOrder
        );

    // ── GradingSession ────────────────────────────────────────────────────────

    public static GradingSessionRecord ToRecord(GradingSession s)
        => new()
        {
            Id = s.Id.Value,
            Name = s.Name,
            RubricId = s.RubricId.Value,
            CreatedAt = s.CreatedAt
        };

    public static GradingSession ToDomain(GradingSessionRecord r)
    {
        var session = CreateInstance<GradingSession>();
        SetProp(session, "Id", new GradingSessionId(r.Id));
        SetProp(session, "Name", r.Name);
        SetProp(session, "RubricId", new RubricId(r.RubricId));
        SetProp(session, "CreatedAt", r.CreatedAt);
        SetField(session, "_events", new System.Collections.Generic.List<Domain.Event.IDomainEvent>());
        return session;
    }

    // ── Submission ────────────────────────────────────────────────────────────

    public static SubmissionRecord ToRecord(Submission s)
        => new()
        {
            Id = s.Id.Value,
            SessionId = s.SessionId.Value,
            StudentIdentifier = s.StudentIdentifier,
            SourceFilesJson = JsonSerializer.Serialize(s.SourceFiles, _json),
            ImportedAt = s.ImportedAt,
            Status = s.Status.ToString(),
            TotalScore = s.TotalScore,
            TeacherNote = s.TeacherNote,
            ErrorMessage = s.ErrorMessage,
            IsPlagiarismSuspected = s.IsPlagiarismSuspected,
            MaxSimilarityPercentage = s.MaxSimilarityPercentage,
            RubricResults = s.RubricResults.Select((r, i) => new RubricResultRecord
            {
                SubmissionId = s.Id.Value,
                CriteriaName = r.CriteriaName,
                GivenScore = r.GivenScore,
                MaxScore = r.MaxScore,
                Comment = r.Comment,
                SortOrder = i
            }).ToList()
        };

    public static Submission ToDomain(SubmissionRecord r)
    {
        var sourceFiles = JsonSerializer.Deserialize<List<SourceFile>>(r.SourceFilesJson, _json) ?? [];

        var rubricResults = r.RubricResults
            .OrderBy(x => x.SortOrder)
            .Select(x => new RubricResult(x.CriteriaName, x.GivenScore, x.MaxScore, x.Comment))
            .ToList();

        var s = CreateInstance<Submission>();
        SetProp(s, "Id", new SubmissionId(r.Id));
        SetProp(s, "SessionId", new GradingSessionId(r.SessionId));
        SetProp(s, "StudentIdentifier", r.StudentIdentifier);
        SetProp(s, "SourceFiles", sourceFiles);
        SetProp(s, "ImportedAt", r.ImportedAt);
        SetProp(s, "Status", Enum.Parse<SubmissionStatus>(r.Status));
        SetProp(s, "TotalScore", r.TotalScore);
        SetProp(s, "TeacherNote", r.TeacherNote);
        SetProp(s, "ErrorMessage", r.ErrorMessage);
        SetProp(s, "IsPlagiarismSuspected", r.IsPlagiarismSuspected);
        SetProp(s, "MaxSimilarityPercentage", r.MaxSimilarityPercentage);

        var results = new List<RubricResult>(rubricResults);
        SetField(s, "_rubricResults", results);
        SetField(s, "_events", new List<Domain.Event.IDomainEvent>());

        return s;
    }
}

// Alias để tránh import System.Runtime.CompilerServices
file static class RuntimeHelpers
{
    public static object GetUninitializedObject(Type type)
        => System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(type);
}
