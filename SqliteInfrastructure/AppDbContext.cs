using Microsoft.EntityFrameworkCore;
using SqliteDataAccess.PersistenceModel;

namespace SqliteDataAccess;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<UserRecord> Users => Set<UserRecord>();
    public DbSet<ClassroomRecord> Classrooms => Set<ClassroomRecord>();
    public DbSet<ClassroomStudentRecord> ClassroomStudents => Set<ClassroomStudentRecord>();
    public DbSet<AssignmentRecord> Assignments => Set<AssignmentRecord>();
    public DbSet<TestCaseRecord> TestCases => Set<TestCaseRecord>();
    public DbSet<RubricRecord> Rubrics => Set<RubricRecord>();
    public DbSet<SubmissionRecord> Submissions => Set<SubmissionRecord>();
    public DbSet<SubmissionTestCaseResultRecord> SubmissionTestCaseResults => Set<SubmissionTestCaseResultRecord>();
    public DbSet<SubmissionRubricResultRecord> SubmissionRubricResults => Set<SubmissionRubricResultRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserRecord>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Email).IsRequired();
            entity.Property(x => x.FullName).IsRequired();
            entity.HasIndex(x => x.Email).IsUnique();
        });

        modelBuilder.Entity<ClassroomRecord>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.JoinCode).IsRequired();
            entity.Property(x => x.Name).IsRequired();
            entity.HasIndex(x => x.JoinCode).IsUnique();

            entity.HasOne<UserRecord>()
                .WithMany()
                .HasForeignKey(x => x.TeacherId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ClassroomStudentRecord>(entity =>
        {
            entity.HasKey(x => new { x.ClassroomId, x.StudentId });

            entity.HasOne<ClassroomRecord>()
                .WithMany()
                .HasForeignKey(x => x.ClassroomId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<UserRecord>()
                .WithMany()
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AssignmentRecord>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).IsRequired();
            entity.Property(x => x.Description).IsRequired();
            entity.Property(x => x.AllowedLanguages).IsRequired();
            entity.HasIndex(x => x.ClassroomId);

            entity.HasOne<ClassroomRecord>()
                .WithMany()
                .HasForeignKey(x => x.ClassroomId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TestCaseRecord>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.InputData).IsRequired();
            entity.Property(x => x.ExpectedOutput).IsRequired();
            entity.HasIndex(x => x.AssignmentId);

            entity.HasOne<AssignmentRecord>()
                .WithMany()
                .HasForeignKey(x => x.AssignmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RubricRecord>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CriteriaListJson).IsRequired();
            entity.HasIndex(x => x.AssignmentId).IsUnique();

            entity.HasOne<AssignmentRecord>()
                .WithMany()
                .HasForeignKey(x => x.AssignmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SubmissionRecord>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SourceCode).IsRequired();
            entity.Property(x => x.Language).IsRequired();
            entity.HasIndex(x => x.AssignmentId);
            entity.HasIndex(x => x.StudentId);

            entity.HasOne<AssignmentRecord>()
                .WithMany()
                .HasForeignKey(x => x.AssignmentId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<UserRecord>()
                .WithMany()
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SubmissionTestCaseResultRecord>(entity =>
        {
            entity.HasKey(x => new { x.SubmissionId, x.SortOrder });
            entity.Property(x => x.ActualOutput).IsRequired();
            entity.HasIndex(x => x.SubmissionId);

            entity.HasOne<SubmissionRecord>()
                .WithMany()
                .HasForeignKey(x => x.SubmissionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<TestCaseRecord>()
                .WithMany()
                .HasForeignKey(x => x.TestCaseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SubmissionRubricResultRecord>(entity =>
        {
            entity.HasKey(x => new { x.SubmissionId, x.SortOrder });
            entity.Property(x => x.CriteriaName).IsRequired();
            entity.Property(x => x.CommentReason).IsRequired();
            entity.HasIndex(x => x.SubmissionId);

            entity.HasOne<SubmissionRecord>()
                .WithMany()
                .HasForeignKey(x => x.SubmissionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
