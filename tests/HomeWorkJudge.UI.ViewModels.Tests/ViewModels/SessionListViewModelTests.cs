using System.Diagnostics;
using HomeWorkJudge.UI.ViewModels.Tests.Support;
using Moq;
using Ports.DTO.GradingSession;
using Ports.DTO.Submission;
using Ports.InBoundPorts.Grading;
using Ports.InBoundPorts.GradingSession;
using Ports.InBoundPorts.Report;
using Ports.InBoundPorts.Rubric;

namespace HomeWorkJudge.UI.ViewModels.Tests.ViewModels;

public class SessionListViewModelTests
{
    [Fact]
    public async Task Constructor_ShouldLoadSessions()
    {
        var session = new GradingSessionSummaryDto(
            Guid.NewGuid(), "Session A", Guid.NewGuid(), "Rubric A", 2, 1, 0, DateTime.UtcNow);

        var sessionUseCase = new Mock<IGradingSessionUseCase>();
        sessionUseCase
            .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([session]);

        var sp = new TestServiceProvider();
        var mainVm = new HomeWorkJudge.UI.ViewModels.MainViewModel(sp);
        var vm = new HomeWorkJudge.UI.ViewModels.SessionListViewModel(sessionUseCase.Object, mainVm, sp);

        await WaitForAsync(() => !vm.IsLoading && vm.Sessions.Count == 1);

        var loaded = Assert.Single(vm.Sessions);
        Assert.Equal("Session A", loaded.Name);
    }

    [Fact]
    public async Task DeleteCommand_ShouldCallUseCaseAndRemoveSession()
    {
        var session = new GradingSessionSummaryDto(
            Guid.NewGuid(), "Session B", Guid.NewGuid(), "Rubric A", 2, 1, 0, DateTime.UtcNow);

        var sessionUseCase = new Mock<IGradingSessionUseCase>();
        sessionUseCase
            .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([session]);

        var sp = new TestServiceProvider();
        var mainVm = new HomeWorkJudge.UI.ViewModels.MainViewModel(sp);
        var vm = new HomeWorkJudge.UI.ViewModels.SessionListViewModel(sessionUseCase.Object, mainVm, sp);

        await WaitForAsync(() => !vm.IsLoading && vm.Sessions.Count == 1);
        await vm.DeleteCommand.ExecuteAsync(session);

        sessionUseCase.Verify(x => x.DeleteAsync(session.SessionId, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Empty(vm.Sessions);
    }

    [Fact]
    public async Task OpenSessionCommand_ShouldInitializeDashboardAndNavigate()
    {
        var session = new GradingSessionSummaryDto(
            Guid.NewGuid(), "Session C", Guid.NewGuid(), "Rubric A", 0, 0, 0, DateTime.UtcNow);

        var listUseCase = new Mock<IGradingSessionUseCase>();
        listUseCase
            .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([session]);

        var dashboardSessionUseCase = new Mock<IGradingSessionUseCase>();
        dashboardSessionUseCase
            .Setup(x => x.GetStatisticsAsync(session.SessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SessionStatisticsDto(0, 0, 0, 0, 0, 0, null, null, null));

        var gradingUseCase = new Mock<IGradingUseCase>();
        gradingUseCase
            .Setup(x => x.GetSubmissionsBySessionAsync(session.SessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<SubmissionSummaryDto>());

        var reportUseCase = new Mock<IReportUseCase>();

        var sp = new TestServiceProvider();
        var mainVm = new HomeWorkJudge.UI.ViewModels.MainViewModel(sp);

        var dashboardVm = new HomeWorkJudge.UI.ViewModels.GradingDashboardViewModel(
            gradingUseCase.Object,
            dashboardSessionUseCase.Object,
            reportUseCase.Object,
            mainVm,
            sp);

        sp.Register(dashboardVm);

        var vm = new HomeWorkJudge.UI.ViewModels.SessionListViewModel(listUseCase.Object, mainVm, sp);
        await WaitForAsync(() => !vm.IsLoading && vm.Sessions.Count == 1);

        vm.OpenSessionCommand.Execute(session);

        await WaitForAsync(() => !dashboardVm.IsLoading);
        Assert.Same(dashboardVm, mainVm.CurrentView);
        Assert.Equal("Session C", dashboardVm.SessionName);
    }

    [Fact]
    public async Task CreateNewCommand_WhenCreateViewRegistered_ShouldNavigate()
    {
        var listUseCase = new Mock<IGradingSessionUseCase>();
        listUseCase
            .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<GradingSessionSummaryDto>());

        var createUseCase = new Mock<IGradingSessionUseCase>();
        var rubricUseCase = new Mock<IRubricUseCase>();
        rubricUseCase
            .Setup(x => x.GetAllAsync(It.IsAny<Ports.DTO.Rubric.GetAllRubricsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Ports.DTO.Rubric.RubricSummaryDto>());

        var sp = new TestServiceProvider();
        var mainVm = new HomeWorkJudge.UI.ViewModels.MainViewModel(sp);

        var createVm = new HomeWorkJudge.UI.ViewModels.SessionCreateViewModel(
            createUseCase.Object,
            rubricUseCase.Object,
            mainVm,
            sp);
        sp.Register(createVm);

        var vm = new HomeWorkJudge.UI.ViewModels.SessionListViewModel(listUseCase.Object, mainVm, sp);
        await WaitForAsync(() => !vm.IsLoading);

        vm.CreateNewCommand.Execute(null);

        Assert.Same(createVm, mainVm.CurrentView);
    }

    private static async Task WaitForAsync(Func<bool> predicate, int timeoutMs = 1200)
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
