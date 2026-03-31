using Domain.Entity;
using Domain.ValueObject;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SqliteDataAccess;
using SqliteDataAccess.Repository;

namespace HomeWorkJudge.SqliteInfrastructure.Tests.Repositories;

public class SqliteRepositoryIntegrationTests
{
    [Fact]
    public async Task RubricRepository_AddAndGetById_ShouldRoundTrip()
    {
        await using var testDb = CreateDbContext();
        var db = testDb.Context;
        var rubricRepo = new SqliteRubricRepository(db);
        var uow = new SqliteUnitOfWork(db);

        var rubricId = Guid.NewGuid();
        var rubric = new Rubric(new RubricId(rubricId), "Lab 1");
        rubric.AddCriteria("Correctness", 10, "Main logic");
        rubric.AddCriteria("Style", 5, "Naming and formatting");

        await rubricRepo.AddAsync(rubric);
        await uow.SaveChangesAsync();

        var reloaded = await rubricRepo.GetByIdAsync(new RubricId(rubricId));

        Assert.NotNull(reloaded);
        Assert.Equal("Lab 1", reloaded!.Name);
        Assert.Equal(2, reloaded.Criteria.Count);
        Assert.Equal(new[] { "Correctness", "Style" }, reloaded.Criteria.Select(c => c.Name));
    }

    [Fact]
    public async Task SubmissionRepository_Update_ShouldPersistStatusAndResults()
    {
        await using var testDb = CreateDbContext();
        var db = testDb.Context;
        var sessionRepo = new SqliteGradingSessionRepository(db);
        var submissionRepo = new SqliteSubmissionRepository(db);
        var uow = new SqliteUnitOfWork(db);

        var sessionId = Guid.NewGuid();
        var session = new GradingSession(
            new GradingSessionId(sessionId),
            "Session A",
            new RubricId(Guid.NewGuid()));

        await sessionRepo.AddAsync(session);
        await uow.SaveChangesAsync();

        var submissionId = Guid.NewGuid();
        var submission = new Submission(
            new SubmissionId(submissionId),
            new GradingSessionId(sessionId),
            "SV001",
            [new SourceFile("main.cs", "Console.WriteLine(1);")]);

        await submissionRepo.AddRangeAsync([submission]);
        await uow.SaveChangesAsync();

        submission.StartGrading();
        submission.AttachAIResults([
            new RubricResult("Correctness", 8, 10, "Good"),
            new RubricResult("Style", 4, 5, "Clear")
        ]);
        submission.AddTeacherNote("Looks good");
        submission.FlagAsPlagiarism(82);

        await submissionRepo.UpdateAsync(submission);
        await uow.SaveChangesAsync();

        var reloaded = await submissionRepo.GetByIdAsync(new SubmissionId(submissionId));

        Assert.NotNull(reloaded);
        Assert.Equal(SubmissionStatus.AIGraded, reloaded!.Status);
        Assert.Equal(12, reloaded.TotalScore);
        Assert.Equal("Looks good", reloaded.TeacherNote);
        Assert.True(reloaded.IsPlagiarismSuspected);
        Assert.Equal(82, reloaded.MaxSimilarityPercentage);
        Assert.Equal(2, reloaded.RubricResults.Count);

        var aiGraded = await submissionRepo.GetByStatusAsync(new GradingSessionId(sessionId), SubmissionStatus.AIGraded);
        Assert.Single(aiGraded);
    }

    [Fact]
    public async Task SubmissionRepository_UpdateBuildFailed_ShouldPersistBuildLog()
    {
        await using var testDb = CreateDbContext();
        var db = testDb.Context;
        var sessionRepo = new SqliteGradingSessionRepository(db);
        var submissionRepo = new SqliteSubmissionRepository(db);
        var uow = new SqliteUnitOfWork(db);

        var sessionId = Guid.NewGuid();
        var session = new GradingSession(
            new GradingSessionId(sessionId),
            "Session Build",
            new RubricId(Guid.NewGuid()));

        await sessionRepo.AddAsync(session);
        await uow.SaveChangesAsync();

        var submissionId = Guid.NewGuid();
        var submission = new Submission(
            new SubmissionId(submissionId),
            new GradingSessionId(sessionId),
            "SV-BUILD",
            [new SourceFile("Program.cs", "broken")]);

        await submissionRepo.AddRangeAsync([submission]);
        await uow.SaveChangesAsync();

        submission.StartGrading();
        submission.MarkBuildFailed("error CS0246");
        Assert.Equal("error CS0246", submission.BuildLog);

        await submissionRepo.UpdateAsync(submission);
        await uow.SaveChangesAsync();

        db.ChangeTracker.Clear();

        var reloaded = await submissionRepo.GetByIdAsync(new SubmissionId(submissionId));

        Assert.NotNull(reloaded);
        Assert.Equal(SubmissionStatus.BuildFailed, reloaded!.Status);
        Assert.Equal("error CS0246", reloaded.BuildLog);
        Assert.Equal(0, reloaded.TotalScore);
    }

    [Fact]
    public async Task AppDbContext_ApplySchemaMigrationsAsync_ShouldBeIdempotent()
    {
        await using var testDb = CreateDbContext();
        var db = testDb.Context;

        await db.ApplySchemaMigrationsAsync();
        await db.ApplySchemaMigrationsAsync();

        await using var conn = new SqliteConnection(db.Database.GetConnectionString());
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA table_info(Submissions);";

        await using var reader = await cmd.ExecuteReaderAsync();
        var hasBuildLog = false;
        while (await reader.ReadAsync())
        {
            var columnName = reader.GetString(1);
            if (string.Equals(columnName, "BuildLog", StringComparison.OrdinalIgnoreCase))
            {
                hasBuildLog = true;
                break;
            }
        }

        Assert.True(hasBuildLog);
    }

    [Fact]
    public async Task SessionDelete_ShouldCascadeSubmissionDelete()
    {
        await using var testDb = CreateDbContext();
        var db = testDb.Context;
        var sessionRepo = new SqliteGradingSessionRepository(db);
        var submissionRepo = new SqliteSubmissionRepository(db);
        var uow = new SqliteUnitOfWork(db);

        var sessionId = Guid.NewGuid();
        var session = new GradingSession(
            new GradingSessionId(sessionId),
            "Session Cascade",
            new RubricId(Guid.NewGuid()));

        await sessionRepo.AddAsync(session);
        await uow.SaveChangesAsync();

        var submission = new Submission(
            new SubmissionId(Guid.NewGuid()),
            new GradingSessionId(sessionId),
            "SV002",
            [new SourceFile("main.cs", "class Program {}")]);

        await submissionRepo.AddRangeAsync([submission]);
        await uow.SaveChangesAsync();

        var beforeDelete = await submissionRepo.CountBySessionIdAsync(new GradingSessionId(sessionId));
        Assert.Equal(1, beforeDelete);

        await sessionRepo.DeleteAsync(new GradingSessionId(sessionId));
        await uow.SaveChangesAsync();

        var afterDelete = await submissionRepo.CountBySessionIdAsync(new GradingSessionId(sessionId));
        Assert.Equal(0, afterDelete);
    }

    private static TestDbContextHandle CreateDbContext()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"hwj-test-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return new TestDbContextHandle(db, dbPath);
    }

    private sealed class TestDbContextHandle : IAsyncDisposable
    {
        public AppDbContext Context { get; }
        private readonly string _dbPath;

        public TestDbContextHandle(AppDbContext context, string dbPath)
        {
            Context = context;
            _dbPath = dbPath;
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();

            // Best-effort cleanup: SQLite may briefly keep file handles open.
            try
            {
                if (File.Exists(_dbPath))
                {
                    File.Delete(_dbPath);
                }
            }
            catch (IOException)
            {
                // Ignore intermittent file-lock cleanup failures in test teardown.
            }
        }
    }
}
