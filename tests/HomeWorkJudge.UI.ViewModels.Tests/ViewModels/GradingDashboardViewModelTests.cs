using System.Diagnostics;
using HomeWorkJudge.UI.ViewModels.Tests.Support;
using Moq;
using Ports.DTO.Grading;
using Ports.DTO.Submission;
using Ports.InBoundPorts.Grading;
using Ports.InBoundPorts.GradingSession;
using Ports.InBoundPorts.Report;

namespace HomeWorkJudge.UI.ViewModels.Tests.ViewModels;

public class GradingDashboardViewModelTests
{
    [Fact]
    public async Task RegradeOneCommand_WhenSubmissionProvided_ShouldCallUseCaseAndRefresh()
    {
        var sessionId = Guid.NewGuid();
        var submission = new SubmissionSummaryDto(
            Guid.NewGuid(),
            sessionId,
            "SV001",
            "AIGraded",
            7,
            false,
            null,
            DateTime.UtcNow);

        var gradingUseCase = new Mock<IGradingUseCase>();
        gradingUseCase
            .Setup(x => x.GetSubmissionsBySessionAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([submission]);

        var sessionUseCase = new Mock<IGradingSessionUseCase>();
        sessionUseCase
            .Setup(x => x.GetStatisticsAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SessionStatisticsDto(1, 0, 0, 1, 0, 0, 7, 7, 7));

        var reportUseCase = new Mock<IReportUseCase>();

        var sp = new TestServiceProvider();
        var mainVm = new HomeWorkJudge.UI.ViewModels.MainViewModel(sp);

        var vm = new HomeWorkJudge.UI.ViewModels.GradingDashboardViewModel(
            gradingUseCase.Object,
            sessionUseCase.Object,
            reportUseCase.Object,
            mainVm,
            sp);

        vm.Initialize(sessionId, "Session 1");
        await WaitForAsync(() => !vm.IsLoading);

        await vm.RegradeOneCommand.ExecuteAsync(submission);

        gradingUseCase.Verify(x => x.RegradeSubmissionAsync(
            It.Is<RegradeSubmissionCommand>(c => c.SubmissionId == submission.SubmissionId),
            It.IsAny<CancellationToken>()), Times.Once);

        gradingUseCase.Verify(x => x.GetSubmissionsBySessionAsync(sessionId, It.IsAny<CancellationToken>()), Times.AtLeast(2));
        Assert.Equal($"Đã chấm lại: {submission.StudentIdentifier}.", vm.StatusMessage);
    }

    [Fact]
    public async Task RegradeOneCommand_WhenSubmissionIsNull_ShouldNotCallUseCase()
    {
        var gradingUseCase = new Mock<IGradingUseCase>();
        var sessionUseCase = new Mock<IGradingSessionUseCase>();
        var reportUseCase = new Mock<IReportUseCase>();

        var sp = new TestServiceProvider();
        var mainVm = new HomeWorkJudge.UI.ViewModels.MainViewModel(sp);

        var vm = new HomeWorkJudge.UI.ViewModels.GradingDashboardViewModel(
            gradingUseCase.Object,
            sessionUseCase.Object,
            reportUseCase.Object,
            mainVm,
            sp);

        await vm.RegradeOneCommand.ExecuteAsync(null);

        gradingUseCase.Verify(x => x.RegradeSubmissionAsync(It.IsAny<RegradeSubmissionCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static async Task WaitForAsync(Func<bool> predicate, int timeoutMs = 1000)
    {
        var sw = Stopwatch.StartNew();
        while (!predicate())
        {
            if (sw.ElapsedMilliseconds > timeoutMs)
                throw new TimeoutException("Timeout waiting for async view-model state.");

            await Task.Delay(20);
        }
    }
}
