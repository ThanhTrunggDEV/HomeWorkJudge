using Domain.Entity;
using Domain.Exception;
using Domain.ValueObject;

namespace HomeWorkJudge.Domain.Tests.Entities;

public class SubmissionTests
{
    [Fact]
    public void Constructor_ShouldStartPending_AndRaiseImportedEvent()
    {
        var submission = CreateSubmission();

        Assert.Equal(SubmissionStatus.Pending, submission.Status);
        Assert.Single(submission.DomainEvents);
    }

    [Fact]
    public void StartGrading_FromPending_ShouldMoveToGrading()
    {
        var submission = CreateSubmission();

        submission.StartGrading();

        Assert.Equal(SubmissionStatus.Grading, submission.Status);
        Assert.Null(submission.ErrorMessage);
    }

    [Fact]
    public void StartGrading_FromReviewed_ShouldThrowDomainException()
    {
        var submission = CreateAiGradedSubmission();
        submission.Approve();

        Assert.Throws<DomainException>(() => submission.StartGrading());
    }

    [Fact]
    public void AttachAIResults_FromGrading_ShouldSetScoreAndStatus()
    {
        var submission = CreateSubmission();
        submission.StartGrading();

        submission.AttachAIResults([
            new RubricResult("Correctness", 7, 10, "Good"),
            new RubricResult("Style", 2, 5, "Ok")
        ]);

        Assert.Equal(SubmissionStatus.AIGraded, submission.Status);
        Assert.Equal(9, submission.TotalScore);
        Assert.Equal(2, submission.RubricResults.Count);
    }

    [Fact]
    public void MarkError_FromGrading_ShouldMoveToError()
    {
        var submission = CreateSubmission();
        submission.StartGrading();

        submission.MarkError("timeout");

        Assert.Equal(SubmissionStatus.Error, submission.Status);
        Assert.Equal("timeout", submission.ErrorMessage);
    }

    [Fact]
    public void OverrideCriteriaScore_FromAiGraded_ShouldSetReviewedAndRecalculateTotal()
    {
        var submission = CreateAiGradedSubmission();

        submission.OverrideCriteriaScore("Correctness", 8, "Teacher adjusted");

        Assert.Equal(SubmissionStatus.Reviewed, submission.Status);
        Assert.Equal(8, submission.TotalScore);
        Assert.Contains(submission.RubricResults, r => r.CriteriaName == "Correctness" && r.GivenScore == 8);
    }

    [Fact]
    public void ResetForRegrade_ShouldClearResultsAndBackToPending()
    {
        var submission = CreateAiGradedSubmission();

        submission.ResetForRegrade();

        Assert.Equal(SubmissionStatus.Pending, submission.Status);
        Assert.Equal(0, submission.TotalScore);
        Assert.Empty(submission.RubricResults);
        Assert.Null(submission.ErrorMessage);
    }

    [Fact]
    public void FlagAsPlagiarism_WithInvalidPercent_ShouldThrow()
    {
        var submission = CreateSubmission();

        Assert.Throws<ArgumentOutOfRangeException>(() => submission.FlagAsPlagiarism(101));
    }

    private static Submission CreateSubmission()
        => new(
            new SubmissionId(Guid.NewGuid()),
            new GradingSessionId(Guid.NewGuid()),
            "SV001",
            [new SourceFile("main.cs", "Console.WriteLine(1);")]);

    private static Submission CreateAiGradedSubmission()
    {
        var submission = CreateSubmission();
        submission.StartGrading();
        submission.AttachAIResults([new RubricResult("Correctness", 7, 10, "Good")]);
        return submission;
    }
}
