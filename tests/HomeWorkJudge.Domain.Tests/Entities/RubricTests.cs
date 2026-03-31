using Domain.Entity;
using Domain.ValueObject;

namespace HomeWorkJudge.Domain.Tests.Entities;

public class RubricTests
{
    [Fact]
    public void Constructor_ShouldTrimName()
    {
        var rubric = new Rubric(new RubricId(Guid.NewGuid()), "  OOP Rubric  ");

        Assert.Equal("OOP Rubric", rubric.Name);
    }

    [Fact]
    public void Rename_WithBlankName_ShouldThrow()
    {
        var rubric = new Rubric(new RubricId(Guid.NewGuid()), "Valid");

        Assert.Throws<ArgumentException>(() => rubric.Rename("   "));
    }

    [Fact]
    public void AddCriteria_WithDuplicateNameIgnoreCase_ShouldThrow()
    {
        var rubric = new Rubric(new RubricId(Guid.NewGuid()), "Lab");
        rubric.AddCriteria("Correctness", 10, "desc");

        Assert.Throws<InvalidOperationException>(() => rubric.AddCriteria("correctness", 8, "desc2"));
    }

    [Fact]
    public void UpdateCriteria_ToDuplicateName_ShouldThrow()
    {
        var rubric = new Rubric(new RubricId(Guid.NewGuid()), "Lab");
        var c1 = rubric.AddCriteria("Correctness", 10, "desc");
        var c2 = rubric.AddCriteria("Style", 5, "desc");

        Assert.Throws<InvalidOperationException>(
            () => rubric.UpdateCriteria(c2.Id, c1.Name, 5, "new"));
    }

    [Fact]
    public void RemoveCriteria_ShouldResetSortOrder()
    {
        var rubric = new Rubric(new RubricId(Guid.NewGuid()), "Lab");
        var c1 = rubric.AddCriteria("C1", 4, "d1");
        rubric.AddCriteria("C2", 3, "d2");

        rubric.RemoveCriteria(c1.Id);

        var remaining = Assert.Single(rubric.Criteria);
        Assert.Equal(0, remaining.SortOrder);
    }

    [Fact]
    public void ReorderCriteria_WithMismatchedCount_ShouldThrow()
    {
        var rubric = new Rubric(new RubricId(Guid.NewGuid()), "Lab");
        var c1 = rubric.AddCriteria("C1", 4, "d1");
        rubric.AddCriteria("C2", 3, "d2");

        Assert.Throws<ArgumentException>(() => rubric.ReorderCriteria([c1.Id]));
    }

    [Fact]
    public void ReorderCriteria_WithValidOrder_ShouldApplyOrder()
    {
        var rubric = new Rubric(new RubricId(Guid.NewGuid()), "Lab");
        var c1 = rubric.AddCriteria("C1", 4, "d1");
        var c2 = rubric.AddCriteria("C2", 3, "d2");
        var c3 = rubric.AddCriteria("C3", 2, "d3");

        rubric.ReorderCriteria([c3.Id, c1.Id, c2.Id]);

        Assert.Equal("C3", rubric.Criteria[0].Name);
        Assert.Equal("C1", rubric.Criteria[1].Name);
        Assert.Equal("C2", rubric.Criteria[2].Name);
        Assert.Equal([0, 1, 2], rubric.Criteria.Select(c => c.SortOrder));
    }
}
