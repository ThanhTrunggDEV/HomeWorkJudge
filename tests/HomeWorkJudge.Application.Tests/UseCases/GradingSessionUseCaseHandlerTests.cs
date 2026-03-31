using Application.UseCases;
using Domain.Entity;
using Domain.Exception;
using Domain.Ports;
using Domain.ValueObject;
using Moq;
using Ports.DTO.GradingSession;
using Ports.DTO.Submission;
using Ports.OutBoundPorts.Storage;

namespace HomeWorkJudge.Application.Tests.UseCases;

public class GradingSessionUseCaseHandlerTests
{
    [Fact]
    public async Task CreateAsync_WhenRubricNotFound_ShouldThrowDomainException()
    {
        var rubricId = Guid.NewGuid();

        var sessionRepo = new Mock<IGradingSessionRepository>();
        var rubricRepo = new Mock<IRubricRepository>();
        rubricRepo
            .Setup(r => r.GetByIdAsync(It.Is<RubricId>(id => id.Value == rubricId), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Rubric?)null);

        var submissionRepo = new Mock<ISubmissionRepository>();
        var extractor = new Mock<IFileExtractorPort>();
        var uow = new Mock<IUnitOfWork>();

        var sut = new GradingSessionUseCaseHandler(
            sessionRepo.Object,
            rubricRepo.Object,
            submissionRepo.Object,
            extractor.Object,
            uow.Object);

        var cmd = new CreateSessionCommand("Session 1", rubricId, [@"C:\tmp\s1.zip"]);

        await Assert.ThrowsAsync<DomainException>(() => sut.CreateAsync(cmd));

        sessionRepo.Verify(x => x.AddAsync(It.IsAny<GradingSession>(), It.IsAny<CancellationToken>()), Times.Never);
        submissionRepo.Verify(x => x.AddRangeAsync(It.IsAny<IEnumerable<Submission>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenSomeArchivesFail_ShouldReturnImportedAndSkipped()
    {
        var rubricId = Guid.NewGuid();
        var rubric = new Rubric(new RubricId(rubricId), "Rubric A");

        var sessionRepo = new Mock<IGradingSessionRepository>();
        var rubricRepo = new Mock<IRubricRepository>();
        rubricRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<RubricId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rubric);

        var submissionRepo = new Mock<ISubmissionRepository>();
        var extractor = new Mock<IFileExtractorPort>();
        extractor
            .Setup(x => x.ExtractAsync(@"C:\tmp\sv1.zip", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new SourceFileDto("main.cs", "Console.WriteLine(1);")]);
        extractor
            .Setup(x => x.ExtractAsync(@"C:\tmp\bad.zip", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidDataException("invalid archive"));

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var sut = new GradingSessionUseCaseHandler(
            sessionRepo.Object,
            rubricRepo.Object,
            submissionRepo.Object,
            extractor.Object,
            uow.Object);

        var cmd = new CreateSessionCommand("Session 1", rubricId, [@"C:\tmp\sv1.zip", @"C:\tmp\bad.zip"]);

        var result = await sut.CreateAsync(cmd);

        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(1, result.SkippedCount);
        sessionRepo.Verify(x => x.AddAsync(It.IsAny<GradingSession>(), It.IsAny<CancellationToken>()), Times.Once);
        submissionRepo.Verify(x => x.AddRangeAsync(
            It.Is<IEnumerable<Submission>>(items => items.Count() == 1 && items.First().StudentIdentifier == "sv1"),
            It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAllAsync_WhenRubricDeleted_ShouldUseDeletedLabelAndCountStatuses()
    {
        var sessionId = Guid.NewGuid();
        var rubricId = Guid.NewGuid();
        var session = new GradingSession(new GradingSessionId(sessionId), "Session A", new RubricId(rubricId));

        var reviewed = CreateReviewedSubmission(sessionId, "sv1", 8);
        var error = CreateErrorSubmission(sessionId, "sv2", "timeout");

        var sessionRepo = new Mock<IGradingSessionRepository>();
        sessionRepo
            .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([session]);

        var rubricRepo = new Mock<IRubricRepository>();
        rubricRepo
            .Setup(x => x.GetByIdAsync(It.IsAny<RubricId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Rubric?)null);

        var submissionRepo = new Mock<ISubmissionRepository>();
        submissionRepo
            .Setup(x => x.GetBySessionIdAsync(It.IsAny<GradingSessionId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([reviewed, error]);

        var extractor = new Mock<IFileExtractorPort>();
        var uow = new Mock<IUnitOfWork>();

        var sut = new GradingSessionUseCaseHandler(
            sessionRepo.Object,
            rubricRepo.Object,
            submissionRepo.Object,
            extractor.Object,
            uow.Object);

        var result = await sut.GetAllAsync();

        var dto = Assert.Single(result);
        Assert.Equal("(deleted)", dto.RubricName);
        Assert.Equal(2, dto.TotalSubmissions);
        Assert.Equal(1, dto.ReviewedCount);
        Assert.Equal(1, dto.ErrorCount);
    }

    [Fact]
    public async Task GetStatisticsAsync_ShouldComputeCountsAndScoreAggregates()
    {
        var sessionId = Guid.NewGuid();
        var pending = CreatePendingSubmission(sessionId, "sv1");
        var grading = CreateGradingSubmission(sessionId, "sv2");
        var aiGraded = CreateAiGradedSubmission(sessionId, "sv3", 8);
        var reviewed = CreateReviewedSubmission(sessionId, "sv4", 9);
        var error = CreateErrorSubmission(sessionId, "sv5", "oops");

        var submissionRepo = new Mock<ISubmissionRepository>();
        submissionRepo
            .Setup(x => x.GetBySessionIdAsync(It.IsAny<GradingSessionId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([pending, grading, aiGraded, reviewed, error]);

        var sut = new GradingSessionUseCaseHandler(
            new Mock<IGradingSessionRepository>().Object,
            new Mock<IRubricRepository>().Object,
            submissionRepo.Object,
            new Mock<IFileExtractorPort>().Object,
            new Mock<IUnitOfWork>().Object);

        var stats = await sut.GetStatisticsAsync(sessionId);

        Assert.Equal(5, stats.TotalCount);
        Assert.Equal(1, stats.PendingCount);
        Assert.Equal(1, stats.GradingCount);
        Assert.Equal(1, stats.AIGradedCount);
        Assert.Equal(1, stats.ReviewedCount);
        Assert.Equal(1, stats.ErrorCount);
        Assert.Equal(8.5, stats.AverageScore);
        Assert.Equal(8, stats.MinScore);
        Assert.Equal(9, stats.MaxScore);
    }

    [Fact]
    public async Task DeleteAsync_ShouldDeleteSessionAndSave()
    {
        var sessionId = Guid.NewGuid();

        var sessionRepo = new Mock<IGradingSessionRepository>();
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var sut = new GradingSessionUseCaseHandler(
            sessionRepo.Object,
            new Mock<IRubricRepository>().Object,
            new Mock<ISubmissionRepository>().Object,
            new Mock<IFileExtractorPort>().Object,
            uow.Object);

        await sut.DeleteAsync(sessionId);

        sessionRepo.Verify(x => x.DeleteAsync(It.Is<GradingSessionId>(id => id.Value == sessionId), It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Submission CreatePendingSubmission(Guid sessionId, string student)
        => new(new SubmissionId(Guid.NewGuid()), new GradingSessionId(sessionId), student, [new SourceFile("main.cs", "code")]);

    private static Submission CreateGradingSubmission(Guid sessionId, string student)
    {
        var s = CreatePendingSubmission(sessionId, student);
        s.StartGrading();
        return s;
    }

    private static Submission CreateAiGradedSubmission(Guid sessionId, string student, double score)
    {
        var s = CreatePendingSubmission(sessionId, student);
        s.StartGrading();
        s.AttachAIResults([new RubricResult("Correctness", score, 10, "ok")]);
        return s;
    }

    private static Submission CreateReviewedSubmission(Guid sessionId, string student, double score)
    {
        var s = CreateAiGradedSubmission(sessionId, student, score);
        s.Approve();
        return s;
    }

    private static Submission CreateErrorSubmission(Guid sessionId, string student, string message)
    {
        var s = CreatePendingSubmission(sessionId, student);
        s.StartGrading();
        s.MarkError(message);
        return s;
    }
}
