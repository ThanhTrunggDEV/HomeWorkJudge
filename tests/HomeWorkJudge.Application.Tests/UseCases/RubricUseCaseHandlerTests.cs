using Application.UseCases;
using Domain.Entity;
using Domain.Exception;
using Domain.Ports;
using Domain.ValueObject;
using Moq;
using Ports.DTO.Rubric;
using Ports.OutBoundPorts.AI;

namespace HomeWorkJudge.Application.Tests.UseCases;

public class RubricUseCaseHandlerTests
{
    [Fact]
    public async Task UpdateNameAsync_WhenRubricExists_RenamesAndPersists()
    {
        var rubricId = Guid.NewGuid();
        var rubric = new Rubric(new RubricId(rubricId), "Old Name");

        var rubricRepo = new Mock<IRubricRepository>();
        rubricRepo
            .Setup(r => r.GetByIdAsync(It.Is<RubricId>(id => id.Value == rubricId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rubric);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var aiGenerator = new Mock<IAiRubricGeneratorPort>();

        var sut = new RubricUseCaseHandler(rubricRepo.Object, uow.Object, aiGenerator.Object);

        await sut.UpdateNameAsync(new UpdateRubricNameCommand(rubricId, "  New Name  "));

        Assert.Equal("New Name", rubric.Name);
        rubricRepo.Verify(r => r.UpdateAsync(It.Is<Rubric>(x => x.Id.Value == rubricId && x.Name == "New Name"), It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateNameAsync_WhenRubricNotFound_ThrowsDomainException()
    {
        var rubricId = Guid.NewGuid();

        var rubricRepo = new Mock<IRubricRepository>();
        rubricRepo
            .Setup(r => r.GetByIdAsync(It.Is<RubricId>(id => id.Value == rubricId), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Rubric?)null);

        var uow = new Mock<IUnitOfWork>();
        var aiGenerator = new Mock<IAiRubricGeneratorPort>();

        var sut = new RubricUseCaseHandler(rubricRepo.Object, uow.Object, aiGenerator.Object);

        await Assert.ThrowsAsync<DomainException>(
            () => sut.UpdateNameAsync(new UpdateRubricNameCommand(rubricId, "Any Name")));

        rubricRepo.Verify(r => r.UpdateAsync(It.IsAny<Rubric>(), It.IsAny<CancellationToken>()), Times.Never);
        uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
