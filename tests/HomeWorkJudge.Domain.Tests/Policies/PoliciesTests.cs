using Domain.Entity;
using Domain.Exception;
using Domain.Policy;
using Domain.ValueObject;

namespace HomeWorkJudge.Domain.Tests.Policies;

public class PoliciesTests
{
    [Fact]
    public void AIResultValidationPolicy_WhenResultsValid_ShouldNotThrow()
    {
        var policy = new AIResultValidationPolicy();
        var criteria = new[]
        {
            new RubricCriteria(new RubricCriteriaId(Guid.NewGuid()), "Correctness", 10, "", 0),
            new RubricCriteria(new RubricCriteriaId(Guid.NewGuid()), "Style", 5, "", 1)
        };
        var results = new[]
        {
            new RubricResult("Correctness", 8, 10, "good"),
            new RubricResult("Style", 4, 5, "ok")
        };

        var ex = Record.Exception(() => policy.Validate(results, criteria));

        Assert.Null(ex);
    }

    [Fact]
    public void AIResultValidationPolicy_WhenMissingCriteria_ShouldThrowDomainException()
    {
        var policy = new AIResultValidationPolicy();
        var criteria = new[]
        {
            new RubricCriteria(new RubricCriteriaId(Guid.NewGuid()), "Correctness", 10, "", 0),
            new RubricCriteria(new RubricCriteriaId(Guid.NewGuid()), "Style", 5, "", 1)
        };
        var results = new[]
        {
            new RubricResult("Correctness", 8, 10, "good")
        };

        var ex = Assert.Throws<DomainException>(() => policy.Validate(results, criteria));

        Assert.Contains("AI thiếu kết quả cho tiêu chí: 'Style'", ex.Message);
    }

    [Fact]
    public void AIResultValidationPolicy_WhenResultHasUnknownCriteria_ShouldThrowDomainException()
    {
        var policy = new AIResultValidationPolicy();
        var criteria = new[]
        {
            new RubricCriteria(new RubricCriteriaId(Guid.NewGuid()), "Correctness", 10, "", 0)
        };
        var results = new[]
        {
            new RubricResult("Correctness", 8, 10, "good"),
            new RubricResult("Performance", 4, 5, "extra")
        };

        var ex = Assert.Throws<DomainException>(() => policy.Validate(results, criteria));

        Assert.Contains("AI trả về tiêu chí không tồn tại trong rubric: 'Performance'", ex.Message);
    }

    [Fact]
    public void AIResultValidationPolicy_WhenScoreOutOfRange_ShouldThrowDomainException()
    {
        var policy = new AIResultValidationPolicy();
        var criteria = new[]
        {
            new RubricCriteria(new RubricCriteriaId(Guid.NewGuid()), "Correctness", 10, "", 0)
        };
        var results = new[]
        {
            new RubricResult("Correctness", 11, 10, "too high")
        };

        var ex = Assert.Throws<DomainException>(() => policy.Validate(results, criteria));

        Assert.Contains("vượt phạm vi [0, 10]", ex.Message);
    }

    [Fact]
    public void GradingSessionReadyPolicy_WhenRubricHasNoCriteria_ShouldThrowDomainException()
    {
        var policy = new GradingSessionReadyPolicy();
        var rubric = new Rubric(new RubricId(Guid.NewGuid()), "R1");
        var submissions = new[] { CreateSubmission("SV001") };

        var ex = Assert.Throws<DomainException>(() => policy.Validate(submissions, rubric));

        Assert.Contains("Rubric chưa có tiêu chí nào", ex.Message);
    }

    [Fact]
    public void GradingSessionReadyPolicy_WhenNoPendingSubmissions_ShouldThrowDomainException()
    {
        var policy = new GradingSessionReadyPolicy();
        var rubric = new Rubric(new RubricId(Guid.NewGuid()), "R1");
        rubric.AddCriteria("Correctness", 10, "");

        var submission = CreateSubmission("SV001");
        submission.StartGrading();
        submission.MarkError("timeout");

        var ex = Assert.Throws<DomainException>(() => policy.Validate([submission], rubric));

        Assert.Contains("Không có bài nộp nào ở trạng thái Pending", ex.Message);
    }

    [Fact]
    public void GradingSessionReadyPolicy_WhenPendingSubmissionExists_ShouldNotThrow()
    {
        var policy = new GradingSessionReadyPolicy();
        var rubric = new Rubric(new RubricId(Guid.NewGuid()), "R1");
        rubric.AddCriteria("Correctness", 10, "");
        var submissions = new[] { CreateSubmission("SV001") };

        var ex = Record.Exception(() => policy.Validate(submissions, rubric));

        Assert.Null(ex);
    }

    [Fact]
    public void SubmissionImportConflictPolicy_Skip_ShouldExcludeExistingStudentIdentifiers()
    {
        var policy = new SubmissionImportConflictPolicy(ImportConflictResolution.Skip);

        var existing = new[] { CreateSubmission("SV001") };
        var incoming = new[]
        {
            CreateSubmission("sv001"),
            CreateSubmission("SV002")
        };

        var result = policy.Resolve(incoming, existing);

        var only = Assert.Single(result);
        Assert.Equal("SV002", only.StudentIdentifier);
    }

    [Fact]
    public void SubmissionImportConflictPolicy_Replace_ShouldReturnAllIncoming()
    {
        var policy = new SubmissionImportConflictPolicy(ImportConflictResolution.Replace);

        var existing = new[] { CreateSubmission("SV001") };
        var incoming = new[]
        {
            CreateSubmission("SV001"),
            CreateSubmission("SV002")
        };

        var result = policy.Resolve(incoming, existing);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void SubmissionImportConflictPolicy_WithUnknownResolution_ShouldThrowDomainException()
    {
        var policy = new SubmissionImportConflictPolicy((ImportConflictResolution)999);

        Assert.Throws<DomainException>(() => policy.Resolve([CreateSubmission("SV001")], []));
    }

    [Fact]
    public void PlagiarismCheckPolicy_WithInvalidThreshold_ShouldThrowDomainException()
    {
        Assert.Throws<DomainException>(() => new PlagiarismCheckPolicy(101));
    }

    [Fact]
    public void PlagiarismCheckPolicy_Apply_ShouldFilterAndFlagSubmissions()
    {
        var policy = new PlagiarismCheckPolicy(70);

        var subA = CreateSubmission("SV001");
        var subB = CreateSubmission("SV002");
        var subC = CreateSubmission("SV003");

        var similarities = new[]
        {
            new PlagiarismSimilarity(subA.Id, subB.Id, "SV001", "SV002", 82),
            new PlagiarismSimilarity(subA.Id, subC.Id, "SV001", "SV003", 60)
        };

        var suspected = policy.Apply(similarities, [subA, subB, subC]);

        Assert.Single(suspected);
        Assert.True(subA.IsPlagiarismSuspected);
        Assert.True(subB.IsPlagiarismSuspected);
        Assert.False(subC.IsPlagiarismSuspected);
        Assert.Equal(82, subA.MaxSimilarityPercentage);
        Assert.Equal(82, subB.MaxSimilarityPercentage);
    }

    [Fact]
    public void PlagiarismCheckPolicy_Apply_WhenSubmissionMissing_ShouldStillReturnSuspectedPairs()
    {
        var policy = new PlagiarismCheckPolicy(70);
        var existing = new[] { CreateSubmission("SV001") };
        var unknownId = new SubmissionId(Guid.NewGuid());

        var similarities = new[]
        {
            new PlagiarismSimilarity(existing[0].Id, unknownId, "SV001", "MISSING", 90)
        };

        var result = policy.Apply(similarities, existing);

        Assert.Single(result);
        Assert.True(existing[0].IsPlagiarismSuspected);
    }

    private static Submission CreateSubmission(string studentIdentifier)
        => new(
            new SubmissionId(Guid.NewGuid()),
            new GradingSessionId(Guid.NewGuid()),
            studentIdentifier,
            [new SourceFile("Program.cs", "Console.WriteLine(1);")]);
}
