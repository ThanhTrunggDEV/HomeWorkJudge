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
    public void StartGrading_FromBuildFailed_ShouldMoveToGrading_AndClearBuildLog()
    {
        var submission = CreateSubmission();
        submission.StartGrading();
        submission.MarkBuildFailed("error CS1002");

        submission.StartGrading();

        Assert.Equal(SubmissionStatus.Grading, submission.Status);
        Assert.Null(submission.BuildLog);
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
    public void MarkBuildFailed_FromGrading_ShouldSetBuildFailedStatusWithZeroScoreAndLog()
    {
        var submission = CreateSubmission();
        submission.StartGrading();

        submission.MarkBuildFailed("error CS0246: Missing type");

        Assert.Equal(SubmissionStatus.BuildFailed, submission.Status);
        Assert.Equal(0, submission.TotalScore);
        Assert.Equal("error CS0246: Missing type", submission.BuildLog);
    }

    [Fact]
    public void MarkBuildFailed_FromPending_ShouldThrowDomainException()
    {
        var submission = CreateSubmission();

        Assert.Throws<DomainException>(() => submission.MarkBuildFailed("compile error"));
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
        var submission = CreateSubmission();
        submission.StartGrading();
        submission.MarkBuildFailed("compile error");

        submission.ResetForRegrade();

        Assert.Equal(SubmissionStatus.Pending, submission.Status);
        Assert.Equal(0, submission.TotalScore);
        Assert.Empty(submission.RubricResults);
        Assert.Null(submission.ErrorMessage);
        Assert.Null(submission.BuildLog);
    }

    [Fact]
    public void FlagAsPlagiarism_WithInvalidPercent_ShouldThrow()
    {
        var submission = CreateSubmission();

        Assert.Throws<ArgumentOutOfRangeException>(() => submission.FlagAsPlagiarism(101));
    }

    // ── Constructor guards ────────────────────────────────────────────────────

    [Fact]
    public void Constructor_WithBlankIdentifier_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => new Submission(
            new SubmissionId(Guid.NewGuid()),
            new GradingSessionId(Guid.NewGuid()),
            "   ",
            [new SourceFile("main.cs", "code")]));
    }

    [Fact]
    public void Constructor_WithNoFiles_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => new Submission(
            new SubmissionId(Guid.NewGuid()),
            new GradingSessionId(Guid.NewGuid()),
            "SV001",
            []));
    }

    [Fact]
    public void Constructor_TrimsStudentIdentifier()
    {
        var s = new Submission(
            new SubmissionId(Guid.NewGuid()),
            new GradingSessionId(Guid.NewGuid()),
            "  SV999  ",
            [new SourceFile("main.cs", "code")]);

        Assert.Equal("SV999", s.StudentIdentifier);
    }

    // ── StartGrading edge cases ───────────────────────────────────────────────

    [Fact]
    public void StartGrading_FromError_ShouldMoveToGrading()
    {
        var s = CreateSubmission();
        s.StartGrading();
        s.MarkError("timeout");

        s.StartGrading();

        Assert.Equal(SubmissionStatus.Grading, s.Status);
        Assert.Null(s.ErrorMessage);
        Assert.Null(s.BuildLog);
    }

    // ── AttachAIResults guard ────────────────────────────────────────────────

    [Fact]
    public void AttachAIResults_WithEmptyList_ShouldThrow()
    {
        var s = CreateSubmission();
        s.StartGrading();

        Assert.Throws<ArgumentException>(() => s.AttachAIResults([]));
    }

    [Fact]
    public void AttachAIResults_FromWrongStatus_ShouldThrow()
    {
        var s = CreateSubmission(); // Pending

        Assert.Throws<DomainException>(() =>
            s.AttachAIResults([new RubricResult("C", 5, 10, "ok")]));
    }

    // ── Approve ──────────────────────────────────────────────────────────────

    [Fact]
    public void Approve_WhenAlreadyReviewed_ShouldBeIdempotent()
    {
        var s = CreateAiGradedSubmission();
        s.Approve();
        s.Approve(); // second call — should not throw

        Assert.Equal(SubmissionStatus.Reviewed, s.Status);
    }

    [Fact]
    public void Approve_FromPending_ShouldThrow()
    {
        var s = CreateSubmission();

        Assert.Throws<DomainException>(() => s.Approve());
    }

    // ── OverrideCriteriaScore ────────────────────────────────────────────────

    [Fact]
    public void OverrideCriteriaScore_WithUnknownCriteria_ShouldThrow()
    {
        var s = CreateAiGradedSubmission();

        Assert.Throws<DomainException>(() =>
            s.OverrideCriteriaScore("NonExistent", 5, "note"));
    }

    [Fact]
    public void OverrideCriteriaScore_WithNegativeScore_ShouldThrow()
    {
        var s = CreateAiGradedSubmission();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            s.OverrideCriteriaScore("Correctness", -1, "note"));
    }

    [Fact]
    public void OverrideCriteriaScore_WithScoreAboveMax_ShouldThrow()
    {
        var s = CreateAiGradedSubmission();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            s.OverrideCriteriaScore("Correctness", 999, "note"));
    }

    [Fact]
    public void OverrideCriteriaScore_FromWrongStatus_ShouldThrow()
    {
        var s = CreateSubmission(); // Pending

        Assert.Throws<DomainException>(() =>
            s.OverrideCriteriaScore("Correctness", 5, "note"));
    }

    // ── OverrideTotalScore ───────────────────────────────────────────────────

    [Fact]
    public void OverrideTotalScore_ShouldSetScoreAndMarkReviewed()
    {
        var s = CreateAiGradedSubmission();

        s.OverrideTotalScore(9.5);

        Assert.Equal(9.5, s.TotalScore);
        Assert.Equal(SubmissionStatus.Reviewed, s.Status);
    }

    [Fact]
    public void OverrideTotalScore_WithNegative_ShouldThrow()
    {
        var s = CreateAiGradedSubmission();

        Assert.Throws<ArgumentOutOfRangeException>(() => s.OverrideTotalScore(-1));
    }

    // ── AddTeacherNote ───────────────────────────────────────────────────────

    [Fact]
    public void AddTeacherNote_WithText_ShouldSetNote()
    {
        var s = CreateSubmission();

        s.AddTeacherNote("  Great work  ");

        Assert.Equal("Great work", s.TeacherNote);
    }

    [Fact]
    public void AddTeacherNote_WithBlank_ShouldSetNull()
    {
        var s = CreateSubmission();
        s.AddTeacherNote("initial");

        s.AddTeacherNote("   ");

        Assert.Null(s.TeacherNote);
    }

    // ── Plagiarism ───────────────────────────────────────────────────────────

    [Fact]
    public void FlagAsPlagiarism_ShouldSetFlagAndPercentage()
    {
        var s = CreateSubmission();

        s.FlagAsPlagiarism(75.5);

        Assert.True(s.IsPlagiarismSuspected);
        Assert.Equal(75.5, s.MaxSimilarityPercentage);
    }

    [Fact]
    public void ClearPlagiarismFlag_ShouldRemoveFlagAndPercentage()
    {
        var s = CreateSubmission();
        s.FlagAsPlagiarism(80);

        s.ClearPlagiarismFlag();

        Assert.False(s.IsPlagiarismSuspected);
        Assert.Null(s.MaxSimilarityPercentage);
    }

    // ── DomainEvents ─────────────────────────────────────────────────────────

    [Fact]
    public void ClearDomainEvents_ShouldEmptyEventList()
    {
        var s = CreateSubmission();
        Assert.NotEmpty(s.DomainEvents);

        s.ClearDomainEvents();

        Assert.Empty(s.DomainEvents);
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
