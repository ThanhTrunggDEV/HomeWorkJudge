using System.Diagnostics;
using System.Net.Http;
using HomeWorkJudge.UI.ViewModels.Tests.Support;
using Moq;
using Ports.DTO.Grading;
using Ports.DTO.Submission;
using Ports.InBoundPorts.Grading;
using Ports.InBoundPorts.GradingSession;
using Ports.InBoundPorts.Report;

namespace HomeWorkJudge.UI.ViewModels.Tests.ViewModels;

public class SubmissionReviewViewModelTests
{
    [Fact]
    public async Task Initialize_ShouldLoadDetailAndSelectFirstFile()
    {
        var submissionId = Guid.NewGuid();
        var gradingUseCase = CreateGradingUseCaseForSubmission(submissionId, "SV001", "AIGraded");

        var vm = CreateReviewVm(gradingUseCase.Object, out var mainVm, out var dashboardVm);

        vm.Initialize(submissionId, [submissionId], mainVm, dashboardVm);
        await WaitForAsync(() => !vm.IsLoading && vm.StudentIdentifier == "SV001");

        Assert.Equal("AIGraded", vm.Status);
        Assert.Equal("SV001", vm.StudentIdentifier);
        Assert.NotNull(vm.SelectedFile);
        Assert.Contains("Console.WriteLine", vm.CurrentFileContent);
        Assert.NotEmpty(vm.FileTree);
        Assert.True(vm.CanApprove);
    }

    [Fact]
    public async Task ApproveCommand_ShouldCallUseCaseAndReload()
    {
        var submissionId = Guid.NewGuid();
        var gradingUseCase = CreateGradingUseCaseForSubmission(submissionId, "SV001", "AIGraded");

        var vm = CreateReviewVm(gradingUseCase.Object, out var mainVm, out var dashboardVm);
        vm.Initialize(submissionId, [submissionId], mainVm, dashboardVm);
        await WaitForAsync(() => !vm.IsLoading);

        await vm.ApproveCommand.ExecuteAsync(null);

        gradingUseCase.Verify(x => x.ApproveAsync(
            It.Is<ApproveSubmissionCommand>(c => c.SubmissionId == submissionId),
            It.IsAny<CancellationToken>()), Times.Once);

        gradingUseCase.Verify(x => x.GetSubmissionDetailAsync(submissionId, It.IsAny<CancellationToken>()), Times.AtLeast(2));
    }

    [Fact]
    public async Task RegradeCommand_WhenHttpRequestFails_ShouldSetFriendlyMessage()
    {
        var submissionId = Guid.NewGuid();
        var gradingUseCase = CreateGradingUseCaseForSubmission(submissionId, "SV001", "AIGraded");
        gradingUseCase
            .Setup(x => x.RegradeSubmissionAsync(It.IsAny<RegradeSubmissionCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("network down"));

        var vm = CreateReviewVm(gradingUseCase.Object, out var mainVm, out var dashboardVm);
        vm.Initialize(submissionId, [submissionId], mainVm, dashboardVm);
        await WaitForAsync(() => !vm.IsLoading);

        await vm.RegradeCommand.ExecuteAsync(null);

        Assert.Equal("Không thể kết nối AI. Kiểm tra API key và kết nối mạng.", vm.ErrorMessage);
    }

    [Fact]
    public async Task SelectFileNodeCommand_WhenFileNode_ShouldUpdateSelectedFile()
    {
        var submissionId = Guid.NewGuid();
        var gradingUseCase = CreateGradingUseCaseForSubmission(submissionId, "SV001", "AIGraded");

        var vm = CreateReviewVm(gradingUseCase.Object, out var mainVm, out var dashboardVm);
        vm.Initialize(submissionId, [submissionId], mainVm, dashboardVm);
        await WaitForAsync(() => !vm.IsLoading && vm.FileTree.Count > 0);

        var rootFolder = vm.FileTree.First(x => x.IsFolder);
        var fileNode = rootFolder.Children.First(x => !x.IsFolder);

        vm.SelectFileNodeCommand.Execute(fileNode);

        Assert.Equal(fileNode.File, vm.SelectedFile);
        Assert.Equal(fileNode.File!.Content, vm.CurrentFileContent);
    }

    [Fact]
    public async Task GoNextAndGoPrev_ShouldNavigateBetweenSubmissionIds()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        var gradingUseCase = new Mock<IGradingUseCase>();
        gradingUseCase
            .Setup(x => x.GetSubmissionDetailAsync(id1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateDetail(id1, "SV001", "AIGraded"));
        gradingUseCase
            .Setup(x => x.GetSubmissionDetailAsync(id2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateDetail(id2, "SV002", "AIGraded"));

        var vm = CreateReviewVm(gradingUseCase.Object, out var mainVm, out var dashboardVm);
        vm.Initialize(id1, [id1, id2], mainVm, dashboardVm);
        await WaitForAsync(() => !vm.IsLoading && vm.StudentIdentifier == "SV001");

        await vm.GoNextCommand.ExecuteAsync(null);
        await WaitForAsync(() => !vm.IsLoading && vm.StudentIdentifier == "SV002");
        Assert.Equal("2 / 2", vm.NavigationInfo);

        await vm.GoPrevCommand.ExecuteAsync(null);
        await WaitForAsync(() => !vm.IsLoading && vm.StudentIdentifier == "SV001");
        Assert.Equal("1 / 2", vm.NavigationInfo);
    }

    private static Mock<IGradingUseCase> CreateGradingUseCaseForSubmission(Guid submissionId, string student, string status)
    {
        var mock = new Mock<IGradingUseCase>();
        mock
            .Setup(x => x.GetSubmissionDetailAsync(submissionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateDetail(submissionId, student, status));
        mock
            .Setup(x => x.ApproveAsync(It.IsAny<ApproveSubmissionCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mock
            .Setup(x => x.RegradeSubmissionAsync(It.IsAny<RegradeSubmissionCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return mock;
    }

    private static SubmissionDetailDto CreateDetail(Guid id, string student, string status)
        => new(
            SubmissionId: id,
            StudentIdentifier: student,
            SourceFiles:
            [
                new SourceFileDto("src/main.cs", "Console.WriteLine(\"Hello\");"),
                new SourceFileDto("README.md", "# title")
            ],
            Status: status,
            TotalScore: 7,
            RubricResults: [new RubricResultDto("Correctness", 7, 10, "ok")],
            IsPlagiarismSuspected: false,
            TeacherNote: null,
            ErrorMessage: null
        );

    private static HomeWorkJudge.UI.ViewModels.SubmissionReviewViewModel CreateReviewVm(
        IGradingUseCase gradingUseCase,
        out HomeWorkJudge.UI.ViewModels.MainViewModel mainVm,
        out HomeWorkJudge.UI.ViewModels.GradingDashboardViewModel dashboardVm)
    {
        var sp = new TestServiceProvider();
        mainVm = new HomeWorkJudge.UI.ViewModels.MainViewModel(sp);

        dashboardVm = new HomeWorkJudge.UI.ViewModels.GradingDashboardViewModel(
            gradingUseCase,
            new Mock<IGradingSessionUseCase>().Object,
            new Mock<IReportUseCase>().Object,
            mainVm,
            sp);

        return new HomeWorkJudge.UI.ViewModels.SubmissionReviewViewModel(gradingUseCase);
    }

    private static async Task WaitForAsync(Func<bool> predicate, int timeoutMs = 1400)
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
