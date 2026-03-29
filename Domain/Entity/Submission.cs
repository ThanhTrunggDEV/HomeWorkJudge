using System;
using System.Collections.Generic;
using System.Linq;
using Domain.ValueObject;
using Domain.Event;
using Domain.Exception;

namespace Domain.Entity;

public class Submission : EntityBase
{
    public SubmissionId Id { get; private set; }
    public AssignmentId AssignmentId { get; private set; }
    public UserId StudentId { get; private set; }
    public string SourceCode { get; private set; }
    public string Language { get; private set; }
    public DateTime SubmitTime { get; private set; }
    public SubmissionStatus Status { get; private set; }
    public double TotalScore { get; private set; }

    private readonly List<TestCaseResult> _testCaseResults = new();
    public IReadOnlyList<TestCaseResult> TestCaseResults => _testCaseResults.AsReadOnly();

    private readonly List<RubricResult> _rubricResults = new();
    public IReadOnlyList<RubricResult> RubricResults => _rubricResults.AsReadOnly();

    public Submission(SubmissionId id, AssignmentId assignmentId, UserId studentId, string sourceCode, string language)
    {
        if (string.IsNullOrWhiteSpace(sourceCode)) throw new ArgumentException("Source code cannot be empty.");
        if (string.IsNullOrWhiteSpace(language)) throw new ArgumentException("Language must be specified.");

        Id = id;
        AssignmentId = assignmentId;
        StudentId = studentId;
        SourceCode = sourceCode;
        Language = language;
        
        SubmitTime = DateTime.UtcNow;
        Status = SubmissionStatus.Pending;
        
        Raise(new SubmissionCreatedEvent(Id, AssignmentId, DateTimeOffset.UtcNow));
    }

    public void ChangeStatusToExecuting() 
    {
        if (Status != SubmissionStatus.Pending) throw new DomainException($"Cannot transition from {Status} to Executing.");
        Status = SubmissionStatus.Executing;
    }
        
    public void AttachTestCaseResults(IEnumerable<TestCaseResult> results)
    {
        if (Status != SubmissionStatus.Executing) throw new DomainException("Only executing submissions can attach results.");
        if (results == null || !results.Any()) throw new ArgumentException("Results cannot be empty.");
        
        _testCaseResults.AddRange(results);
        Status = SubmissionStatus.Done;
        TotalScore = CalculateScoreFromTestCases();
        Raise(new SubmissionGradingCompletedEvent(Id, TotalScore, DateTimeOffset.UtcNow));
    }

    public void AttachRubricResults(IEnumerable<RubricResult> results)
    {
        if (Status != SubmissionStatus.Executing) throw new DomainException("Only executing submissions can attach results.");
        if (results == null || !results.Any()) throw new ArgumentException("Results cannot be empty.");

        _rubricResults.AddRange(results);
        Status = SubmissionStatus.Done;
        TotalScore = _rubricResults.Sum(r => r.GivenScore);
        Raise(new SubmissionGradingCompletedEvent(Id, TotalScore, DateTimeOffset.UtcNow));
    }

    private double CalculateScoreFromTestCases()
    {
        if (_testCaseResults.Count == 0) return 0;
        double passed = _testCaseResults.Count(x => x.Status == TestCaseStatus.Passed);
        return (passed / _testCaseResults.Count) * 100.0;
    }
        
    public void ApplyLatePenalty(double penaltyPercent)
    {
        if (penaltyPercent < 0 || penaltyPercent > 100) throw new ArgumentException("Invalid penalty percentage.");
        TotalScore -= TotalScore * penaltyPercent / 100.0;
        if (TotalScore < 0) TotalScore = 0;
    }
}
