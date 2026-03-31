using System.Diagnostics;
using HomeWorkJudge.UI.ViewModels.Tests.Support;
using Moq;
using Ports.DTO.Rubric;
using Ports.InBoundPorts.Rubric;

namespace HomeWorkJudge.UI.ViewModels.Tests.ViewModels;

public class RubricEditorViewModelTests
{
    [Fact]
    public async Task SaveCommand_WhenNewRubric_ShouldCreateAndNavigateToRubrics()
    {
        var rubricUseCase = new Mock<IRubricUseCase>();
        rubricUseCase
            .Setup(x => x.CreateAsync(It.IsAny<CreateRubricCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreateRubricResult(Guid.NewGuid()));

        var sp = new TestServiceProvider();
        var mainVm = new HomeWorkJudge.UI.ViewModels.MainViewModel(sp);
        mainVm.NavigateTo("Sessions");

        var vm = new HomeWorkJudge.UI.ViewModels.RubricEditorViewModel(rubricUseCase.Object, mainVm, rubricId: null)
        {
            RubricName = "Rubric A"
        };
        vm.Criteria.Add(new RubricCriteriaDto(Guid.NewGuid(), "Correctness", 10, "desc"));

        await vm.SaveCommand.ExecuteAsync(null);

        rubricUseCase.Verify(x => x.CreateAsync(
            It.Is<CreateRubricCommand>(c => c.Name == "Rubric A" && c.Criteria.Count == 1),
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.False(vm.IsNewRubric);
        Assert.Equal("Rubrics", mainVm.CurrentPage);
    }

    [Fact]
    public async Task SaveCommand_WhenExistingRubric_ShouldUpdateNameAndNavigate()
    {
        var rubricId = Guid.NewGuid();

        var rubricUseCase = new Mock<IRubricUseCase>();
        rubricUseCase
            .Setup(x => x.GetByIdAsync(rubricId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RubricDetailDto(
                rubricId,
                "Old Name",
                DateTime.UtcNow,
                []));

        var sp = new TestServiceProvider();
        var mainVm = new HomeWorkJudge.UI.ViewModels.MainViewModel(sp);
        mainVm.NavigateTo("Sessions");

        var vm = new HomeWorkJudge.UI.ViewModels.RubricEditorViewModel(rubricUseCase.Object, mainVm, rubricId);
        await WaitForAsync(() => !vm.IsLoading);

        vm.RubricName = "New Name";
        await vm.SaveCommand.ExecuteAsync(null);

        rubricUseCase.Verify(x => x.UpdateNameAsync(
            It.Is<UpdateRubricNameCommand>(c => c.RubricId == rubricId && c.NewName == "New Name"),
            It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal("Rubrics", mainVm.CurrentPage);
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
