using Microsoft.EntityFrameworkCore;
using SqliteDataAccess.PersistenceModel;

namespace SqliteDataAccess;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<RubricRecord> Rubrics => Set<RubricRecord>();
    public DbSet<RubricCriteriaRecord> RubricCriteria => Set<RubricCriteriaRecord>();
    public DbSet<GradingSessionRecord> GradingSessions => Set<GradingSessionRecord>();
    public DbSet<SubmissionRecord> Submissions => Set<SubmissionRecord>();
    public DbSet<RubricResultRecord> RubricResults => Set<RubricResultRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ── Rubric ───────────────────────────────────────────────────────────
        modelBuilder.Entity<RubricRecord>(e =>
        {
            e.ToTable("Rubrics");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.CreatedAt).IsRequired();

            e.HasMany(x => x.Criteria)
             .WithOne()
             .HasForeignKey(x => x.RubricId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── RubricCriteria ────────────────────────────────────────────────────
        modelBuilder.Entity<RubricCriteriaRecord>(e =>
        {
            e.ToTable("RubricCriteria");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.Property(x => x.MaxScore).IsRequired();
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.SortOrder).IsRequired();
            e.HasIndex(x => x.RubricId);
        });

        // ── GradingSession ────────────────────────────────────────────────────
        modelBuilder.Entity<GradingSessionRecord>(e =>
        {
            e.ToTable("GradingSessions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(300);
            e.Property(x => x.RubricId).IsRequired();
            e.Property(x => x.CreatedAt).IsRequired();
            e.HasIndex(x => x.RubricId);

            // Cascade: xoá session → xoá tất cả submissions
            e.HasMany<SubmissionRecord>()
             .WithOne()
             .HasForeignKey(x => x.SessionId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ── Submission ────────────────────────────────────────────────────────
        modelBuilder.Entity<SubmissionRecord>(e =>
        {
            e.ToTable("Submissions");
            e.HasKey(x => x.Id);
            e.Property(x => x.StudentIdentifier).IsRequired().HasMaxLength(200);
            e.Property(x => x.SourceFilesJson).IsRequired().HasColumnType("TEXT");
            e.Property(x => x.Status).IsRequired().HasMaxLength(20);
            e.Property(x => x.TotalScore).IsRequired();
            e.Property(x => x.TeacherNote).HasMaxLength(2000);
            e.Property(x => x.ErrorMessage).HasMaxLength(1000);

            e.HasMany(x => x.RubricResults)
             .WithOne()
             .HasForeignKey(x => x.SubmissionId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => x.SessionId);
            e.HasIndex(x => new { x.SessionId, x.StudentIdentifier }).IsUnique();
            e.HasIndex(x => x.Status);
        });

        // ── RubricResult ──────────────────────────────────────────────────────
        modelBuilder.Entity<RubricResultRecord>(e =>
        {
            e.ToTable("RubricResults");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedOnAdd();
            e.Property(x => x.CriteriaName).IsRequired().HasMaxLength(200);
            e.Property(x => x.Comment).HasMaxLength(2000);
            e.HasIndex(x => x.SubmissionId);
        });
    }
}
