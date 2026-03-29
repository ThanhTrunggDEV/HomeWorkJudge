using System;
using Domain.ValueObject;

namespace Domain.Entity;

public class TestCase : EntityBase
{
    public TestCaseId Id { get; private set; }
    public AssignmentId AssignmentId { get; private set; }
    public string InputData { get; private set; }
    public string ExpectedOutput { get; private set; }
    public bool IsHidden { get; private set; }
    public double ScoreWeight { get; private set; }

    public TestCase(TestCaseId id, AssignmentId assignmentId, string inputData, string expectedOutput, bool isHidden, double scoreWeight)
    {
        if (scoreWeight < 0) throw new ArgumentException("Score weight cannot be negative.");
        
        Id = id;
        AssignmentId = assignmentId;
        InputData = inputData ?? string.Empty;
        ExpectedOutput = expectedOutput ?? string.Empty;
        IsHidden = isHidden;
        ScoreWeight = scoreWeight;
    }

    public void UpdateDetails(string inputData, string expectedOutput, bool isHidden, double scoreWeight)
    {
        if (scoreWeight < 0) throw new ArgumentException("Score weight cannot be negative.");
        InputData = inputData ?? string.Empty;
        ExpectedOutput = expectedOutput ?? string.Empty;
        IsHidden = isHidden;
        ScoreWeight = scoreWeight;
    }
}
