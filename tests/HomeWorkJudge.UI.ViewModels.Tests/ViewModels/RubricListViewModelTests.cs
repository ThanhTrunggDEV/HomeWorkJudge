using System.Diagnostics;
using Domain.Exception;
using HomeWorkJudge.UI.ViewModels.Tests.Support;
using Moq;
using Ports.DTO.Rubric;
using Ports.InBoundPorts.Rubric;

namespace HomeWorkJudge.UI.ViewModels.Tests.ViewModels;

public class RubricListViewModelTests
{
    [Fact]
    public async Task Constructor_ShouldLoadRubrics()
    {
        var rubricUseCase = new Mock<IRubricUseCase>();
        rubricUseCase
            .Setup(x => x.GetAllAsync(It.IsAny<GetAllRubricsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new RubricSummaryDto(Guid.NewGuid(), "R1", 10, 1, DateTime.UtcNow),
                new RubricSummaryDto(Guid.NewGuid(), "R2", 20, 2, DateTime.UtcNow)
            ]);

        var sp = new TestServiceProvider();
        var mainVm = new HomeWorkJudge.UI.ViewModels.MainViewModel(sp);

        var vm = new HomeWorkJudge.UI.ViewModels.RubricListViewModel(rubricUseCase.Object, mainVm);
        await WaitForAsync(() => !vm.IsLoading && vm.Rubrics.Count == 2);

        Assert.Equal(2, vm.Rubrics.Count);
    }

    [Fact]
    public async Task DeleteCommand_WhenDomainException_ShouldSetErrorMessage()
    {
        var rubric = new RubricSummaryDto(Guid.NewGuid(), "R1", 10, 1, DateTime.UtcNow);

        var rubricUseCase = new Mock<IRubricUseCase>();
        rubricUseCase
            .Setup(x => x.GetAllAsync(It.IsAny<GetAllRubricsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([rubric]);
        rubricUseCase
            .Setup(x => x.DeleteAsync(rubric.Id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DomainException("cannot delete"));

        var sp = new TestServiceProvider();
        var mainVm = new HomeWorkJudge.UI.ViewModels.MainViewModel(sp);
        var vm = new HomeWorkJudge.UI.ViewModels.RubricListViewModel(rubricUseCase.Object, mainVm);

        await WaitForAsync(() => !vm.IsLoading && vm.Rubrics.Count == 1);
        await vm.DeleteCommand.ExecuteAsync(rubric);

        Assert.Equal("cannot delete", vm.ErrorMessage);
        Assert.Single(vm.Rubrics);
    }

    [Fact]
    public async Task GenerateByAiCommand_ShouldGenerateAndResetInputs()
    {
        var rubricUseCase = new Mock<IRubricUseCase>();
        rubricUseCase
            .Setup(x => x.GetAllAsync(It.IsAny<GetAllRubricsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RubricSummaryDto>());
        rubricUseCase
            .Setup(x => x.GenerateByAiAsync(It.IsAny<GenerateRubricCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GenerateRubricResult(Guid.NewGuid()));

        var sp = new TestServiceProvider();
        var mainVm = new HomeWorkJudge.UI.ViewModels.MainViewModel(sp);
        var vm = new HomeWorkJudge.UI.ViewModels.RubricListViewModel(rubricUseCase.Object, mainVm)
        {
            AiDescription = "Build a rubric for OOP assignment",
            AiRubricName = "",
            ShowAiPanel = true
        };

        await WaitForAsync(() => !vm.IsLoading);
        await vm.GenerateByAiCommand.ExecuteAsync(null);

        rubricUseCase.Verify(x => x.GenerateByAiAsync(
            It.Is<GenerateRubricCommand>(c => c.RubricName == "AI Generated Rubric"),
            It.IsAny<CancellationToken>()), Times.Once);

        Assert.Equal(string.Empty, vm.AiDescription);
        Assert.Equal(string.Empty, vm.AiRubricName);
        Assert.False(vm.ShowAiPanel);
    }

    [Fact]
    public async Task CreateNewCommand_ShouldNavigateToEditorViewModel()
    {
        var rubricUseCase = new Mock<IRubricUseCase>();
        rubricUseCase
            .Setup(x => x.GetAllAsync(It.IsAny<GetAllRubricsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RubricSummaryDto>());

        var sp = new TestServiceProvider();
        var mainVm = new HomeWorkJudge.UI.ViewModels.MainViewModel(sp);
        var vm = new HomeWorkJudge.UI.ViewModels.RubricListViewModel(rubricUseCase.Object, mainVm);

        await WaitForAsync(() => !vm.IsLoading);
        vm.CreateNewCommand.Execute(null);

        Assert.IsType<HomeWorkJudge.UI.ViewModels.RubricEditorViewModel>(mainVm.CurrentView);
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
