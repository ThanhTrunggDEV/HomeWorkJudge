using Application.DomainEvents;
using Application.UseCases;
using Domain.Entity;
using Domain.Exception;
using Domain.Ports;
using Domain.ValueObject;
using Moq;
using Ports.DTO.Grading;
using Ports.DTO.Rubric;
using Ports.OutBoundPorts.AI;
using Ports.OutBoundPorts.Plagiarism;

namespace HomeWorkJudge.Application.Tests.UseCases;

public class GradingUseCaseHandlerTests
{
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

        var sut = new GradingUseCaseHandler(
            submissionRepo.Object,
            sessionRepo.Object,
            rubricRepo.Object,
            aiGrading.Object,
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

        var sut = new GradingUseCaseHandler(
            submissionRepo.Object,
            sessionRepo.Object,
            rubricRepo.Object,
            aiGrading.Object,
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
}
