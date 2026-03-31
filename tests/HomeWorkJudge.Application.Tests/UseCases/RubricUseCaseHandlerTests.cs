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
    public async Task CreateAsync_ShouldPersistRubricWithCriteria()
    {
        var addedRubric = default(Rubric);

        var rubricRepo = new Mock<IRubricRepository>();
        rubricRepo
            .Setup(r => r.AddAsync(It.IsAny<Rubric>(), It.IsAny<CancellationToken>()))
            .Callback<Rubric, CancellationToken>((rubric, _) => addedRubric = rubric)
            .Returns(Task.CompletedTask);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var sut = new RubricUseCaseHandler(rubricRepo.Object, uow.Object, new Mock<IAiRubricGeneratorPort>().Object);

        var result = await sut.CreateAsync(new CreateRubricCommand(
            "Rubric A",
            [new RubricCriteriaInputDto("Correctness", 10, "desc")]
        ));

        Assert.NotEqual(Guid.Empty, result.RubricId);
        Assert.NotNull(addedRubric);
        Assert.Equal("Rubric A", addedRubric!.Name);
        Assert.Single(addedRubric.Criteria);
        uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateByAiAsync_ShouldCreateRubricFromAiCriteria()
    {
        var rubricRepo = new Mock<IRubricRepository>();
        rubricRepo.Setup(r => r.AddAsync(It.IsAny<Rubric>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var aiGenerator = new Mock<IAiRubricGeneratorPort>();
        aiGenerator
            .Setup(a => a.GenerateAsync("oop assignment", It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new RubricCriteriaDto(Guid.NewGuid(), "Correctness", 10, "desc"),
                new RubricCriteriaDto(Guid.NewGuid(), "Style", 5, "desc")
            ]);

        var sut = new RubricUseCaseHandler(rubricRepo.Object, uow.Object, aiGenerator.Object);

        var result = await sut.GenerateByAiAsync(new GenerateRubricCommand("oop assignment", "AI Rubric"));

        Assert.NotEqual(Guid.Empty, result.RubricId);
        rubricRepo.Verify(r => r.AddAsync(
            It.Is<Rubric>(x => x.Name == "AI Rubric" && x.Criteria.Count == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CloneAsync_ShouldCloneAndPersist()
    {
        var sourceId = Guid.NewGuid();
        var source = new Rubric(new RubricId(sourceId), "Original");
        source.AddCriteria("C1", 10, "d1");

        var rubricRepo = new Mock<IRubricRepository>();
        rubricRepo
            .Setup(r => r.GetByIdAsync(It.Is<RubricId>(id => id.Value == sourceId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(source);

        var cloned = default(Rubric);
        rubricRepo
            .Setup(r => r.AddAsync(It.IsAny<Rubric>(), It.IsAny<CancellationToken>()))
            .Callback<Rubric, CancellationToken>((r, _) => cloned = r)
            .Returns(Task.CompletedTask);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var sut = new RubricUseCaseHandler(rubricRepo.Object, uow.Object, new Mock<IAiRubricGeneratorPort>().Object);

        var result = await sut.CloneAsync(new CloneRubricCommand(sourceId, "Clone A"));

        Assert.NotEqual(Guid.Empty, result.NewRubricId);
        Assert.NotNull(cloned);
        Assert.Equal("Clone A", cloned!.Name);
        Assert.Single(cloned.Criteria);
    }

    [Fact]
    public async Task GetAllAsync_WithoutSearch_ShouldUseGetAll()
    {
        var r1 = new Rubric(new RubricId(Guid.NewGuid()), "R1");
        r1.AddCriteria("C1", 10, "d");

        var rubricRepo = new Mock<IRubricRepository>();
        rubricRepo
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([r1]);

        var sut = new RubricUseCaseHandler(rubricRepo.Object, new Mock<IUnitOfWork>().Object, new Mock<IAiRubricGeneratorPort>().Object);

        var list = await sut.GetAllAsync(new GetAllRubricsQuery(null));

        var dto = Assert.Single(list);
        Assert.Equal("R1", dto.Name);
        Assert.Equal(10, dto.MaxTotalScore);
        Assert.Equal(1, dto.CriteriaCount);
        rubricRepo.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
        rubricRepo.Verify(r => r.SearchByNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAllAsync_WithSearch_ShouldUseSearchByName()
    {
        var r1 = new Rubric(new RubricId(Guid.NewGuid()), "Data Structures");

        var rubricRepo = new Mock<IRubricRepository>();
        rubricRepo
            .Setup(r => r.SearchByNameAsync("data", It.IsAny<CancellationToken>()))
            .ReturnsAsync([r1]);

        var sut = new RubricUseCaseHandler(rubricRepo.Object, new Mock<IUnitOfWork>().Object, new Mock<IAiRubricGeneratorPort>().Object);

        var list = await sut.GetAllAsync(new GetAllRubricsQuery("data"));

        Assert.Single(list);
        rubricRepo.Verify(r => r.SearchByNameAsync("data", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_WhenFound_ShouldMapDetail()
    {
        var rubricId = Guid.NewGuid();
        var rubric = new Rubric(new RubricId(rubricId), "R1");
        var c = rubric.AddCriteria("Correctness", 10, "desc");

        var rubricRepo = new Mock<IRubricRepository>();
        rubricRepo
            .Setup(r => r.GetByIdAsync(It.Is<RubricId>(id => id.Value == rubricId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(rubric);

        var sut = new RubricUseCaseHandler(rubricRepo.Object, new Mock<IUnitOfWork>().Object, new Mock<IAiRubricGeneratorPort>().Object);

        var detail = await sut.GetByIdAsync(rubricId);

        Assert.Equal("R1", detail.Name);
        var dto = Assert.Single(detail.Criteria);
        Assert.Equal(c.Id.Value, dto.Id);
        Assert.Equal("Correctness", dto.Name);
    }

    [Fact]
    public async Task DeleteAsync_ShouldDeleteAndSave()
    {
        var rubricId = Guid.NewGuid();

        var rubricRepo = new Mock<IRubricRepository>();
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var sut = new RubricUseCaseHandler(rubricRepo.Object, uow.Object, new Mock<IAiRubricGeneratorPort>().Object);

        await sut.DeleteAsync(rubricId);

        rubricRepo.Verify(r => r.DeleteAsync(It.Is<RubricId>(id => id.Value == rubricId), It.IsAny<CancellationToken>()), Times.Once);
        uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

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
