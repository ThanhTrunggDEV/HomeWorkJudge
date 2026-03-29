using System;
using System.Collections.Generic;
using Domain.ValueObject;
using Domain.Event;
using Domain.Exception;

namespace Domain.Entity;

public class Assignment : EntityBase
{
    public AssignmentId Id { get; private set; }
    public ClassroomId ClassroomId { get; private set; }
    public string Title { get; private set; }
    public string Description { get; private set; }
    public string AllowedLanguages { get; private set; }
    public DateTime DueDate { get; private set; }
    public PublishStatus PublishStatus { get; private set; }
        
    public long TimeLimitMs { get; private set; }
    public long MemoryLimitKb { get; private set; }
    public int MaxSubmissions { get; private set; }
    public GradingType GradingType { get; private set; }

    private readonly List<TestCase> _testCases = new();
    public IReadOnlyList<TestCase> TestCases => _testCases.AsReadOnly();
        
    public Rubric? Rubric { get; private set; }

    public Assignment(AssignmentId id, ClassroomId classroomId, string title, string description, DateTime dueDate, GradingType gradingType)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title cannot be empty.");
        
        Id = id;
        ClassroomId = classroomId;
        Title = title;
        Description = description ?? string.Empty;
        DueDate = dueDate;
        GradingType = gradingType;
        
        PublishStatus = PublishStatus.Draft;
        AllowedLanguages = "ALL";
        MaxSubmissions = 100;
    }

    public void UpdateLimits(long timeLimitMs, long memoryLimitKb, int maxSubmissions)
    {
        if (timeLimitMs <= 0) throw new DomainException("Time limit must be positive.");
        if (memoryLimitKb <= 0) throw new DomainException("Memory limit must be positive.");
        if (maxSubmissions <= 0) throw new DomainException("Max submissions must be positive.");
        
        TimeLimitMs = timeLimitMs;
        MemoryLimitKb = memoryLimitKb;
        MaxSubmissions = maxSubmissions;
    }

    public void AddTestCase(TestCase testCase) 
    {
        if (GradingType != GradingType.TestCase) throw new DomainException("Cannot add test cases to a non-TestCase grading assignment.");
        if (testCase == null) throw new ArgumentNullException(nameof(testCase));
        _testCases.Add(testCase);
    }
    
    public void SetRubric(Rubric rubric) 
    {
        if (GradingType != GradingType.Rubric) throw new DomainException("Cannot set rubric for a non-Rubric grading assignment.");
        Rubric = rubric ?? throw new ArgumentNullException(nameof(rubric));
    }
        
    public void Publish()
    {
        if (PublishStatus == PublishStatus.Published) throw new DomainException("Assignment is already published.");
        if (GradingType == GradingType.TestCase && _testCases.Count == 0) throw new DomainException("Cannot publish a TestCase assignment without test cases.");
        if (GradingType == GradingType.Rubric && Rubric == null) throw new DomainException("Cannot publish a Rubric assignment without a rubric.");

        PublishStatus = PublishStatus.Published;
        Raise(new AssignmentPublishedEvent(Id, DateTime.UtcNow, DateTimeOffset.UtcNow));
    }

    public bool IsOverdue(DateTime currentTime) => currentTime > DueDate;
}
