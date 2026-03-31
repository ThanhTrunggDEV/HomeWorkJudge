using Domain.Entity;
using Domain.ValueObject;

namespace HomeWorkJudge.Domain.Tests.Entities;

public class GradingSessionTests
{
    [Fact]
    public void Constructor_ShouldTrimName()
    {
        var session = new GradingSession(
            new GradingSessionId(Guid.NewGuid()),
            "  Session A  ",
            new RubricId(Guid.NewGuid()));

        Assert.Equal("Session A", session.Name);
    }

    [Fact]
    public void Constructor_WithBlankName_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => new GradingSession(
            new GradingSessionId(Guid.NewGuid()),
            "   ",
            new RubricId(Guid.NewGuid())));
    }

    [Fact]
    public void Rename_ShouldTrimAndUpdateName()
    {
        var session = new GradingSession(
            new GradingSessionId(Guid.NewGuid()),
            "Session A",
            new RubricId(Guid.NewGuid()));

        session.Rename("  Session B  ");

        Assert.Equal("Session B", session.Name);
    }

    [Fact]
    public void Rename_WithBlankName_ShouldThrow()
    {
        var session = new GradingSession(
            new GradingSessionId(Guid.NewGuid()),
            "Session A",
            new RubricId(Guid.NewGuid()));

        Assert.Throws<ArgumentException>(() => session.Rename("\t\n"));
    }
}
