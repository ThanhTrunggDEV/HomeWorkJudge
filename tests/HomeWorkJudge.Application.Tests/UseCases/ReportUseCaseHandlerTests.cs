using Application.UseCases;
using Domain.Entity;
using Domain.Exception;
using Domain.Ports;
using Domain.ValueObject;
using Moq;
using Ports.DTO.Report;
using Ports.DTO.Submission;
using Ports.OutBoundPorts.Report;

namespace HomeWorkJudge.Application.Tests.UseCases;

public class ReportUseCaseHandlerTests
{
    [Fact]
    public async Task ExportAsync_WhenSessionNotFound_ShouldThrowDomainException()
    {
        var sessionRepo = new Mock<IGradingSessionRepository>();
        sessionRepo
            .Setup(x => x.GetByIdAsync(It.IsAny<GradingSessionId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GradingSession?)null);

        var sut = new ReportUseCaseHandler(
            new Mock<ISubmissionRepository>().Object,
            sessionRepo.Object,
            new Mock<IReportExportPort>().Object);

        var cmd = new ExportScoreCommand(Guid.NewGuid(), ExportFormat.Csv, IncludeCriteriaDetail: false);

        await Assert.ThrowsAsync<DomainException>(() => sut.ExportAsync(cmd));
    }

    [Fact]
    public async Task ExportAsync_WhenSessionExists_ShouldMapSubmissionSummaryAndDelegateToExportPort()
    {
        var sessionId = Guid.NewGuid();
        var session = new GradingSession(new GradingSessionId(sessionId), "Session A", new RubricId(Guid.NewGuid()));

        var submission = new Submission(
            new SubmissionId(Guid.NewGuid()),
            new GradingSessionId(sessionId),
            "sv1",
            [new SourceFile("main.cs", "code")]);
        submission.StartGrading();
        submission.AttachAIResults([new RubricResult("Correctness", 8, 10, "good")]);

        var sessionRepo = new Mock<IGradingSessionRepository>();
        sessionRepo
            .Setup(x => x.GetByIdAsync(It.IsAny<GradingSessionId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var submissionRepo = new Mock<ISubmissionRepository>();
        submissionRepo
            .Setup(x => x.GetBySessionIdAsync(It.IsAny<GradingSessionId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([submission]);

        Guid capturedSessionId = Guid.Empty;
        IReadOnlyList<SubmissionSummaryDto> capturedSubmissions = [];
        bool capturedDetail = false;
        ExportFormat capturedFormat = ExportFormat.Csv;

        var exportPort = new Mock<IReportExportPort>();
        exportPort
            .Setup(x => x.ExportAsync(
                It.IsAny<Guid>(),
                It.IsAny<IReadOnlyList<SubmissionSummaryDto>>(),
                It.IsAny<bool>(),
                It.IsAny<ExportFormat>(),
                It.IsAny<CancellationToken>()))
            .Callback<Guid, IReadOnlyList<SubmissionSummaryDto>, bool, ExportFormat, CancellationToken>((sid, summaries, detail, format, _) =>
            {
                capturedSessionId = sid;
                capturedSubmissions = summaries;
                capturedDetail = detail;
                capturedFormat = format;
            })
            .ReturnsAsync(new ExportScoreResult([1, 2, 3], "score.csv", "text/csv"));

        var sut = new ReportUseCaseHandler(submissionRepo.Object, sessionRepo.Object, exportPort.Object);

        var result = await sut.ExportAsync(new ExportScoreCommand(sessionId, ExportFormat.Excel, IncludeCriteriaDetail: true));

        Assert.Equal("score.csv", result.FileName);
        Assert.Equal(sessionId, capturedSessionId);
        Assert.True(capturedDetail);
        Assert.Equal(ExportFormat.Excel, capturedFormat);

        var summary = Assert.Single(capturedSubmissions);
        Assert.Equal(submission.Id.Value, summary.SubmissionId);
        Assert.Equal("sv1", summary.StudentIdentifier);
        Assert.Equal("AIGraded", summary.Status);
        Assert.Equal(8, summary.TotalScore);
    }
}
