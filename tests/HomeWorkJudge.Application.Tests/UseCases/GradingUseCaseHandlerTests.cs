using Application.DomainEvents;
using Application.UseCases;
using Domain.Entity;
using Domain.Exception;
using Domain.Ports;
using Domain.ValueObject;
using Moq;
using Ports.DTO.Grading;
using Ports.DTO.Rubric;
using Ports.DTO.Submission;
using Ports.OutBoundPorts.AI;
using Ports.OutBoundPorts.Build;
using Ports.OutBoundPorts.Plagiarism;

namespace HomeWorkJudge.Application.Tests.UseCases;

public class GradingUseCaseHandlerTests
{
    [Fact]
    public async Task StartGradingAsync_WhenSessionNotFound_ShouldThrowDomainException()
    {
        var sessionId = Guid.NewGuid();

        var sessionRepo = new Mock<IGradingSessionRepository>();
        sessionRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<GradingSessionId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GradingSession?)null);

        var sut = new GradingUseCaseHandler(
            new Mock<ISubmissionRepository>().Object,
            sessionRepo.Object,
            new Mock<IRubricRepository>().Object,
            new Mock<IAiGradingPort>().Object,
            new Mock<ICSharpBuildPort>().Object,
            new Mock<IPlagiarismDetectionPort>().Object,
            new Mock<IUnitOfWork>().Object,
            new Mock<IDomainEventDispatcher>().Object);

        await Assert.ThrowsAsync<DomainException>(() => sut.StartGradingAsync(new StartGradingCommand(sessionId)));
    }

    [Fact]
    public async Task StartGradingAsync_WhenRubricMissing_ShouldThrowDomainException()
    {
        var sessionId = Guid.NewGuid();
        var rubricId = Guid.NewGuid();
        var session = new GradingSession(new GradingSessionId(sessionId), "S1", new RubricId(rubricId));

        var sessionRepo = new Mock<IGradingSessionRepository>();
        sessionRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<GradingSessionId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var rubricRepo = new Mock<IRubricRepository>();
        rubricRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<RubricId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Rubric?)null);

        var sut = new GradingUseCaseHandler(
            new Mock<ISubmissionRepository>().Object,
            sessionRepo.Object,
            rubricRepo.Object,
            new Mock<IAiGradingPort>().Object,
            new Mock<ICSharpBuildPort>().Object,
            new Mock<IPlagiarismDetectionPort>().Object,
            new Mock<IUnitOfWork>().Object,
            new Mock<IDomainEventDispatcher>().Object);

        await Assert.ThrowsAsync<DomainException>(() => sut.StartGradingAsync(new StartGradingCommand(sessionId)));
    }

    [Fact]
    public async Task StartGradingAsync_WhenPendingSubmissions_ShouldGradeAll()
    {
        var sessionId = Guid.NewGuid();
        var rubricId = Guid.NewGuid();

        var rubric = new Rubric(new RubricId(rubricId), "Lab Rubric");
        rubric.AddCriteria("Correctness", 10, "Main criteria");
        var session = new GradingSession(new GradingSessionId(sessionId), "Session 1", new RubricId(rubricId));

        var subA = new Submission(new SubmissionId(Guid.NewGuid()), new GradingSessionId(sessionId), "SV001", [new SourceFile("a.cs", "code")]);
        var subB = new Submission(new SubmissionId(Guid.NewGuid()), new GradingSessionId(sessionId), "SV002", [new SourceFile("b.cs", "code")]);

        var submissionRepo = new Mock<ISubmissionRepository>();
        submissionRepo
            .Setup(r => r.GetByStatusAsync(It.IsAny<GradingSessionId>(), SubmissionStatus.Pending, It.IsAny<CancellationToken>()))
            .ReturnsAsync([subA, subB]);

        var sessionRepo = new Mock<IGradingSessionRepository>();
        sessionRepo.Setup(r => r.GetByIdAsync(It.IsAny<GradingSessionId>(), It.IsAny<CancellationToken>())).ReturnsAsync(session);

        var rubricRepo = new Mock<IRubricRepository>();
        rubricRepo.Setup(r => r.GetByIdAsync(It.IsAny<RubricId>(), It.IsAny<CancellationToken>())).ReturnsAsync(rubric);

        var aiGrading = new Mock<IAiGradingPort>();
        aiGrading
            .Setup(a => a.GradeAsync(It.IsAny<IReadOnlyList<Ports.DTO.Submission.SourceFileDto>>(), It.IsAny<IReadOnlyList<RubricCriteriaDto>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new RubricScoreDto("Correctness", 6, 10, "ok")]);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var dispatcher = new Mock<IDomainEventDispatcher>();
        dispatcher.Setup(d => d.DispatchAsync(It.IsAny<IEnumerable<Domain.Event.IDomainEvent>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var buildPort = new Mock<ICSharpBuildPort>();
        buildPort
            .Setup(b => b.BuildAsync(It.IsAny<IReadOnlyList<SourceFileDto>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BuildResult(true, "Build succeeded."));

        var sut = new GradingUseCaseHandler(
            submissionRepo.Object,
            sessionRepo.Object,
            rubricRepo.Object,
            aiGrading.Object,
            buildPort.Object,
            new Mock<IPlagiarismDetectionPort>().Object,
            uow.Object,
            dispatcher.Object);

        var result = await sut.StartGradingAsync(new StartGradingCommand(sessionId));

        Assert.Equal(2, result.StartedCount);
        Assert.All([subA, subB], s => Assert.Equal(SubmissionStatus.AIGraded, s.Status));
        submissionRepo.Verify(r => r.UpdateAsync(It.IsAny<Submission>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task StartGradingAsync_WhenNoPendingSubmissions_ShouldReturnZero()
    {
        var sessionId = Guid.NewGuid();
        var rubricId = Guid.NewGuid();

        var session = new GradingSession(new GradingSessionId(sessionId), "Session 1", new RubricId(rubricId));
        var rubric = new Rubric(new RubricId(rubricId), "Lab Rubric");
        rubric.AddCriteria("Correctness", 10, "Main criteria");

        var submissionRepo = new Mock<ISubmissionRepository>();
        submissionRepo
            .Setup(r => r.GetByStatusAsync(
                It.Is<GradingSessionId>(id => id.Value == sessionId),
                SubmissionStatus.Pending,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Submission>());

        var sessionRepo = new Mock<IGradingSessionRepository>();
        sessionRepo
            .Setup(r => r.GetByIdAsync(It.Is<GradingSessionId>(id => id.Value == sessionId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var rubricRepo = new Mock<IRubricRepository>();
        rubricRepo
            .Setup(r => r.GetByIdAsync(It.Is<RubricId>(id => id.Value == rubricId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rubric);

        var aiGrading = new Mock<IAiGradingPort>();
        var plagiarismPort = new Mock<IPlagiarismDetectionPort>();
        var uow = new Mock<IUnitOfWork>();
        var dispatcher = new Mock<IDomainEventDispatcher>();

        var sut = new GradingUseCaseHandler(
            submissionRepo.Object,
            sessionRepo.Object,
            rubricRepo.Object,
            aiGrading.Object,
            new Mock<ICSharpBuildPort>().Object,
            plagiarismPort.Object,
            uow.Object,
            dispatcher.Object);

        var result = await sut.StartGradingAsync(new StartGradingCommand(sessionId));

        Assert.Equal(0, result.StartedCount);
        aiGrading.Verify(a => a.GradeAsync(
            It.IsAny<IReadOnlyList<Ports.DTO.Submission.SourceFileDto>>(),
            It.IsAny<IReadOnlyList<RubricCriteriaDto>>(),
            It.IsAny<CancellationToken>()), Times.Never);
        submissionRepo.Verify(r => r.UpdateAsync(It.IsAny<Submission>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StartGradingAsync_WhenBuildFails_ShouldMarkBuildFailed_AndSkipAiGrading()
    {
        var sessionId = Guid.NewGuid();
        var rubricId = Guid.NewGuid();

        var rubric = new Rubric(new RubricId(rubricId), "Lab Rubric");
        rubric.AddCriteria("Correctness", 10, "Main criteria");
        var session = new GradingSession(new GradingSessionId(sessionId), "Session 1", new RubricId(rubricId));

        var submission = new Submission(
            new SubmissionId(Guid.NewGuid()),
            new GradingSessionId(sessionId),
            "SV001",
            [new SourceFile("Program.cs", "invalid code")]);

        var submissionRepo = new Mock<ISubmissionRepository>();
        submissionRepo
            .Setup(r => r.GetByStatusAsync(It.IsAny<GradingSessionId>(), SubmissionStatus.Pending, It.IsAny<CancellationToken>()))
            .ReturnsAsync([submission]);

        var sessionRepo = new Mock<IGradingSessionRepository>();
        sessionRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<GradingSessionId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var rubricRepo = new Mock<IRubricRepository>();
        rubricRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<RubricId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rubric);

        var aiGrading = new Mock<IAiGradingPort>();

        var buildPort = new Mock<ICSharpBuildPort>();
        buildPort
            .Setup(b => b.BuildAsync(It.IsAny<IReadOnlyList<SourceFileDto>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BuildResult(false, "error CS1002: ; expected"));

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var dispatcher = new Mock<IDomainEventDispatcher>();
        dispatcher
            .Setup(d => d.DispatchAsync(It.IsAny<IEnumerable<Domain.Event.IDomainEvent>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new GradingUseCaseHandler(
            submissionRepo.Object,
            sessionRepo.Object,
            rubricRepo.Object,
            aiGrading.Object,
            buildPort.Object,
            new Mock<IPlagiarismDetectionPort>().Object,
            uow.Object,
            dispatcher.Object);

        var result = await sut.StartGradingAsync(new StartGradingCommand(sessionId));

        Assert.Equal(1, result.StartedCount);
        Assert.Equal(SubmissionStatus.BuildFailed, submission.Status);
        Assert.Equal("error CS1002: ; expected", submission.BuildLog);
        Assert.Equal(0, submission.TotalScore);
        aiGrading.Verify(a => a.GradeAsync(
            It.IsAny<IReadOnlyList<SourceFileDto>>(),
            It.IsAny<IReadOnlyList<RubricCriteriaDto>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegradeSubmissionAsync_WhenSubmissionExists_RegradesAndPersists()
    {
        var sessionId = Guid.NewGuid();
        var rubricId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();

        var rubric = new Rubric(new RubricId(rubricId), "Lab Rubric");
        rubric.AddCriteria("Correctness", 10, "Main criteria");

        var session = new GradingSession(new GradingSessionId(sessionId), "Session 1", new RubricId(rubricId));
        var submission = new Submission(
            new SubmissionId(submissionId),
            new GradingSessionId(sessionId),
            "SV001",
            [new SourceFile("main.cs", "Console.WriteLine(1);")]);

        var submissionRepo = new Mock<ISubmissionRepository>();
        submissionRepo
            .Setup(r => r.GetByIdAsync(It.Is<SubmissionId>(id => id.Value == submissionId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(submission);

        var sessionRepo = new Mock<IGradingSessionRepository>();
        sessionRepo
            .Setup(r => r.GetByIdAsync(It.Is<GradingSessionId>(id => id.Value == sessionId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var rubricRepo = new Mock<IRubricRepository>();
        rubricRepo
            .Setup(r => r.GetByIdAsync(It.Is<RubricId>(id => id.Value == rubricId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rubric);

        var aiGrading = new Mock<IAiGradingPort>();
        aiGrading
            .Setup(a => a.GradeAsync(It.IsAny<IReadOnlyList<Ports.DTO.Submission.SourceFileDto>>(), It.IsAny<IReadOnlyList<RubricCriteriaDto>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new RubricScoreDto("Correctness", 7, 10, "Good")]);

        var plagiarismPort = new Mock<IPlagiarismDetectionPort>();
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var dispatcher = new Mock<IDomainEventDispatcher>();
        dispatcher
            .Setup(d => d.DispatchAsync(It.IsAny<IEnumerable<Domain.Event.IDomainEvent>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var buildPort = new Mock<ICSharpBuildPort>();
        buildPort
            .Setup(b => b.BuildAsync(It.IsAny<IReadOnlyList<SourceFileDto>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BuildResult(true, "Build succeeded."));

        var sut = new GradingUseCaseHandler(
            submissionRepo.Object,
            sessionRepo.Object,
            rubricRepo.Object,
            aiGrading.Object,
            buildPort.Object,
            plagiarismPort.Object,
            uow.Object,
            dispatcher.Object);

        await sut.RegradeSubmissionAsync(new RegradeSubmissionCommand(submissionId));

        Assert.Equal(SubmissionStatus.AIGraded, submission.Status);
        Assert.Equal(7, submission.TotalScore);
        submissionRepo.Verify(r => r.UpdateAsync(It.Is<Submission>(s => s.Id.Value == submissionId), It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        dispatcher.Verify(d => d.DispatchAsync(It.IsAny<IEnumerable<Domain.Event.IDomainEvent>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegradeSubmissionAsync_WhenSubmissionNotFound_ThrowsDomainException()
    {
        var submissionId = Guid.NewGuid();

        var submissionRepo = new Mock<ISubmissionRepository>();
        submissionRepo
            .Setup(r => r.GetByIdAsync(It.Is<SubmissionId>(id => id.Value == submissionId), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Submission?)null);

        var sessionRepo = new Mock<IGradingSessionRepository>();
        var rubricRepo = new Mock<IRubricRepository>();
        var aiGrading = new Mock<IAiGradingPort>();
        var plagiarismPort = new Mock<IPlagiarismDetectionPort>();
        var uow = new Mock<IUnitOfWork>();
        var dispatcher = new Mock<IDomainEventDispatcher>();

        var sut = new GradingUseCaseHandler(
            submissionRepo.Object,
            sessionRepo.Object,
            rubricRepo.Object,
            aiGrading.Object,
            new Mock<ICSharpBuildPort>().Object,
            plagiarismPort.Object,
            uow.Object,
            dispatcher.Object);

        await Assert.ThrowsAsync<DomainException>(() => sut.RegradeSubmissionAsync(new RegradeSubmissionCommand(submissionId)));

        submissionRepo.Verify(r => r.UpdateAsync(It.IsAny<Submission>(), It.IsAny<CancellationToken>()), Times.Never);
        uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegradeSessionAsync_WhenHasSubmissions_ShouldRegradeAll()
    {
        var sessionId = Guid.NewGuid();
        var rubricId = Guid.NewGuid();

        var rubric = new Rubric(new RubricId(rubricId), "Lab Rubric");
        rubric.AddCriteria("Correctness", 10, "Main criteria");

        var session = new GradingSession(new GradingSessionId(sessionId), "Session 1", new RubricId(rubricId));
        var subA = new Submission(
            new SubmissionId(Guid.NewGuid()),
            new GradingSessionId(sessionId),
            "SV001",
            [new SourceFile("a.cs", "class A {}")]);
        var subB = new Submission(
            new SubmissionId(Guid.NewGuid()),
            new GradingSessionId(sessionId),
            "SV002",
            [new SourceFile("b.cs", "class B {}")]);

        // One submission starts from Error to ensure reset path is covered.
        subB.StartGrading();
        subB.MarkError("timeout");

        var submissionRepo = new Mock<ISubmissionRepository>();
        submissionRepo
            .Setup(r => r.GetBySessionIdAsync(It.Is<GradingSessionId>(id => id.Value == sessionId), It.IsAny<CancellationToken>()))
            .ReturnsAsync([subA, subB]);

        var sessionRepo = new Mock<IGradingSessionRepository>();
        sessionRepo
            .Setup(r => r.GetByIdAsync(It.Is<GradingSessionId>(id => id.Value == sessionId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var rubricRepo = new Mock<IRubricRepository>();
        rubricRepo
            .Setup(r => r.GetByIdAsync(It.Is<RubricId>(id => id.Value == rubricId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rubric);

        var aiGrading = new Mock<IAiGradingPort>();
        aiGrading
            .Setup(a => a.GradeAsync(It.IsAny<IReadOnlyList<Ports.DTO.Submission.SourceFileDto>>(), It.IsAny<IReadOnlyList<RubricCriteriaDto>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new RubricScoreDto("Correctness", 6, 10, "ok")]);

        var plagiarismPort = new Mock<IPlagiarismDetectionPort>();
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var dispatcher = new Mock<IDomainEventDispatcher>();
        dispatcher
            .Setup(d => d.DispatchAsync(It.IsAny<IEnumerable<Domain.Event.IDomainEvent>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var buildPort = new Mock<ICSharpBuildPort>();
        buildPort
            .Setup(b => b.BuildAsync(It.IsAny<IReadOnlyList<SourceFileDto>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BuildResult(true, "Build succeeded."));

        var sut = new GradingUseCaseHandler(
            submissionRepo.Object,
            sessionRepo.Object,
            rubricRepo.Object,
            aiGrading.Object,
            buildPort.Object,
            plagiarismPort.Object,
            uow.Object,
            dispatcher.Object);

        await sut.RegradeSessionAsync(new RegradeSessionCommand(sessionId));

        Assert.All([subA, subB], s => Assert.Equal(SubmissionStatus.AIGraded, s.Status));
        Assert.All([subA, subB], s => Assert.Equal(6, s.TotalScore));
        submissionRepo.Verify(r => r.UpdateAsync(It.IsAny<Submission>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        aiGrading.Verify(a => a.GradeAsync(
            It.IsAny<IReadOnlyList<Ports.DTO.Submission.SourceFileDto>>(),
            It.IsAny<IReadOnlyList<RubricCriteriaDto>>(),
            It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ApproveAsync_WhenSubmissionAiGraded_ShouldUpdateSaveAndDispatch()
    {
        var sessionId = Guid.NewGuid();
        var submission = new Submission(
            new SubmissionId(Guid.NewGuid()),
            new GradingSessionId(sessionId),
            "SV001",
            [new SourceFile("main.cs", "code")]);
        submission.StartGrading();
        submission.AttachAIResults([new RubricResult("Correctness", 7, 10, "ok")]);

        var submissionRepo = new Mock<ISubmissionRepository>();
        submissionRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<SubmissionId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(submission);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var dispatcher = new Mock<IDomainEventDispatcher>();
        dispatcher.Setup(d => d.DispatchAsync(It.IsAny<IEnumerable<Domain.Event.IDomainEvent>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var sut = new GradingUseCaseHandler(
            submissionRepo.Object,
            new Mock<IGradingSessionRepository>().Object,
            new Mock<IRubricRepository>().Object,
            new Mock<IAiGradingPort>().Object,
            new Mock<ICSharpBuildPort>().Object,
            new Mock<IPlagiarismDetectionPort>().Object,
            uow.Object,
            dispatcher.Object);

        await sut.ApproveAsync(new ApproveSubmissionCommand(submission.Id.Value));

        Assert.Equal(SubmissionStatus.Reviewed, submission.Status);
        Assert.Empty(submission.DomainEvents);
        submissionRepo.Verify(r => r.UpdateAsync(It.IsAny<Submission>(), It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        dispatcher.Verify(d => d.DispatchAsync(It.IsAny<IEnumerable<Domain.Event.IDomainEvent>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CheckPlagiarismAsync_WhenPairsAboveThreshold_ShouldFlagAndPersist()
    {
        var sessionId = Guid.NewGuid();
        var subA = new Submission(new SubmissionId(Guid.NewGuid()), new GradingSessionId(sessionId), "SV001", [new SourceFile("a.cs", "code")]);
        var subB = new Submission(new SubmissionId(Guid.NewGuid()), new GradingSessionId(sessionId), "SV002", [new SourceFile("b.cs", "code")]);

        var submissionRepo = new Mock<ISubmissionRepository>();
        submissionRepo
            .Setup(r => r.GetBySessionIdAsync(It.IsAny<GradingSessionId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([subA, subB]);
        submissionRepo
            .Setup(r => r.GetByIdAsync(It.Is<SubmissionId>(id => id.Value == subA.Id.Value), It.IsAny<CancellationToken>()))
            .ReturnsAsync(subA);
        submissionRepo
            .Setup(r => r.GetByIdAsync(It.Is<SubmissionId>(id => id.Value == subB.Id.Value), It.IsAny<CancellationToken>()))
            .ReturnsAsync(subB);

        var plagiarism = new Mock<IPlagiarismDetectionPort>();
        plagiarism
            .Setup(p => p.DetectAsync(It.IsAny<IReadOnlyList<SubmissionFilesDto>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new PlagiarismResultDto(subA.Id.Value, subB.Id.Value, subA.StudentIdentifier, subB.StudentIdentifier, 88)
            ]);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var sut = new GradingUseCaseHandler(
            submissionRepo.Object,
            new Mock<IGradingSessionRepository>().Object,
            new Mock<IRubricRepository>().Object,
            new Mock<IAiGradingPort>().Object,
            new Mock<ICSharpBuildPort>().Object,
            plagiarism.Object,
            uow.Object,
            new Mock<IDomainEventDispatcher>().Object);

        var result = await sut.CheckPlagiarismAsync(new CheckPlagiarismCommand(sessionId, 70));

        Assert.Single(result.SuspectedPairs);
        Assert.True(subA.IsPlagiarismSuspected);
        Assert.True(subB.IsPlagiarismSuspected);
        Assert.Equal(88, subA.MaxSimilarityPercentage);
        Assert.Equal(88, subB.MaxSimilarityPercentage);
        submissionRepo.Verify(r => r.UpdateAsync(It.IsAny<Submission>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetSubmissionDetailAsync_WhenBuildFailed_ShouldReturnBuildLog()
    {
        var submissionId = Guid.NewGuid();
        var submission = new Submission(
            new SubmissionId(submissionId),
            new GradingSessionId(Guid.NewGuid()),
            "SV001",
            [new SourceFile("Program.cs", "broken")]);
        submission.StartGrading();
        submission.MarkBuildFailed("error CS0246: The type or namespace name could not be found");

        var submissionRepo = new Mock<ISubmissionRepository>();
        submissionRepo
            .Setup(r => r.GetByIdAsync(It.Is<SubmissionId>(id => id.Value == submissionId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(submission);

        var sut = new GradingUseCaseHandler(
            submissionRepo.Object,
            new Mock<IGradingSessionRepository>().Object,
            new Mock<IRubricRepository>().Object,
            new Mock<IAiGradingPort>().Object,
            new Mock<ICSharpBuildPort>().Object,
            new Mock<IPlagiarismDetectionPort>().Object,
            new Mock<IUnitOfWork>().Object,
            new Mock<IDomainEventDispatcher>().Object);

        var detail = await sut.GetSubmissionDetailAsync(submissionId);

        Assert.Equal("BuildFailed", detail.Status);
        Assert.Equal("error CS0246: The type or namespace name could not be found", detail.BuildLog);
        Assert.Equal(0, detail.TotalScore);
    }

    [Fact]
    public async Task StartGradingAsync_WhenBuildFails_ShouldMarkBuildFailedAndNotCallAI()
    {
        var sessionId = Guid.NewGuid();
        var rubricId = Guid.NewGuid();

        var rubric = new Rubric(new RubricId(rubricId), "Lab Rubric");
        rubric.AddCriteria("Correctness", 10, "Main criteria");
        var session = new GradingSession(new GradingSessionId(sessionId), "Session 1", new RubricId(rubricId));

        var submission = new Submission(
            new SubmissionId(Guid.NewGuid()),
            new GradingSessionId(sessionId),
            "SV001",
            [new SourceFile("main.cs", "Console.WriteLine(1);")]);

        var submissionRepo = new Mock<ISubmissionRepository>();
        submissionRepo
            .Setup(r => r.GetByStatusAsync(It.IsAny<GradingSessionId>(), SubmissionStatus.Pending, It.IsAny<CancellationToken>()))
            .ReturnsAsync([submission]);

        var sessionRepo = new Mock<IGradingSessionRepository>();
        sessionRepo.Setup(r => r.GetByIdAsync(It.IsAny<GradingSessionId>(), It.IsAny<CancellationToken>())).ReturnsAsync(session);

        var rubricRepo = new Mock<IRubricRepository>();
        rubricRepo.Setup(r => r.GetByIdAsync(It.IsAny<RubricId>(), It.IsAny<CancellationToken>())).ReturnsAsync(rubric);

        var aiGrading = new Mock<IAiGradingPort>();
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var dispatcher = new Mock<IDomainEventDispatcher>();
        dispatcher.Setup(d => d.DispatchAsync(It.IsAny<IEnumerable<Domain.Event.IDomainEvent>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Build FAILS
        var buildPort = new Mock<ICSharpBuildPort>();
        buildPort
            .Setup(b => b.BuildAsync(It.IsAny<IReadOnlyList<SourceFileDto>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BuildResult(false, "error CS0246: Type not found"));

        var sut = new GradingUseCaseHandler(
            submissionRepo.Object, sessionRepo.Object, rubricRepo.Object,
            aiGrading.Object, buildPort.Object,
            new Mock<IPlagiarismDetectionPort>().Object, uow.Object, dispatcher.Object);

        await sut.StartGradingAsync(new StartGradingCommand(sessionId));

        // AI should NOT be called
        aiGrading.Verify(a => a.GradeAsync(
            It.IsAny<IReadOnlyList<SourceFileDto>>(),
            It.IsAny<IReadOnlyList<RubricCriteriaDto>>(),
            It.IsAny<CancellationToken>()), Times.Never);

        Assert.Equal(SubmissionStatus.BuildFailed, submission.Status);
        Assert.Equal(0, submission.TotalScore);
        Assert.Equal("error CS0246: Type not found", submission.BuildLog);
    }

    [Fact]
    public async Task RegradeSubmissionAsync_WhenBuildFails_ShouldMarkBuildFailedAndNotCallAI()
    {
        var sessionId = Guid.NewGuid();
        var rubricId = Guid.NewGuid();
        var submissionId = Guid.NewGuid();

        var rubric = new Rubric(new RubricId(rubricId), "Lab Rubric");
        rubric.AddCriteria("Correctness", 10, "Main criteria");
        var session = new GradingSession(new GradingSessionId(sessionId), "Session 1", new RubricId(rubricId));

        var submission = new Submission(
            new SubmissionId(submissionId),
            new GradingSessionId(sessionId),
            "SV001",
            [new SourceFile("main.cs", "broken code;")]);

        var submissionRepo = new Mock<ISubmissionRepository>();
        submissionRepo
            .Setup(r => r.GetByIdAsync(It.Is<SubmissionId>(id => id.Value == submissionId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(submission);

        var sessionRepo = new Mock<IGradingSessionRepository>();
        sessionRepo
            .Setup(r => r.GetByIdAsync(It.Is<GradingSessionId>(id => id.Value == sessionId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var rubricRepo = new Mock<IRubricRepository>();
        rubricRepo
            .Setup(r => r.GetByIdAsync(It.Is<RubricId>(id => id.Value == rubricId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rubric);

        var aiGrading = new Mock<IAiGradingPort>();
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var dispatcher = new Mock<IDomainEventDispatcher>();
        dispatcher.Setup(d => d.DispatchAsync(It.IsAny<IEnumerable<Domain.Event.IDomainEvent>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var buildPort = new Mock<ICSharpBuildPort>();
        buildPort
            .Setup(b => b.BuildAsync(It.IsAny<IReadOnlyList<SourceFileDto>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BuildResult(false, "error CS1002: ; expected"));

        var sut = new GradingUseCaseHandler(
            submissionRepo.Object, sessionRepo.Object, rubricRepo.Object,
            aiGrading.Object, buildPort.Object,
            new Mock<IPlagiarismDetectionPort>().Object, uow.Object, dispatcher.Object);

        await sut.RegradeSubmissionAsync(new RegradeSubmissionCommand(submissionId));

        aiGrading.Verify(a => a.GradeAsync(
            It.IsAny<IReadOnlyList<SourceFileDto>>(),
            It.IsAny<IReadOnlyList<RubricCriteriaDto>>(),
            It.IsAny<CancellationToken>()), Times.Never);

        Assert.Equal(SubmissionStatus.BuildFailed, submission.Status);
        Assert.Equal(0, submission.TotalScore);
    }
}
