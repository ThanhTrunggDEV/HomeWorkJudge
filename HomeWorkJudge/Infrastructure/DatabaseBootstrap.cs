using System;
using System.Data;
using System.Threading.Tasks;
using Domain.Ports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ports.DTO.Classroom;
using Ports.DTO.Common;
using Ports.DTO.User;
using Ports.InBoundPorts.Classroom;
using Ports.InBoundPorts.User;
using SqliteDataAccess;

namespace HomeWorkJudge.Infrastructure;

public static class DatabaseBootstrap
{
    public static async Task InitializeAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var serviceProvider = scope.ServiceProvider;

        var dbContext = serviceProvider.GetRequiredService<AppDbContext>();
        await BaselineLegacySchemaIfNeededAsync(dbContext);
        await dbContext.Database.MigrateAsync();

        await EnsureSeedUsersAsync(serviceProvider);
        await EnsureSeedClassroomAsync(serviceProvider, dbContext);
    }

    private static async Task BaselineLegacySchemaIfNeededAsync(AppDbContext dbContext)
    {
        var hasUsersTable = await TableExistsAsync(dbContext, "Users");
        if (!hasUsersTable)
        {
            return;
        }

        var hasHistoryTable = await TableExistsAsync(dbContext, "__EFMigrationsHistory");
        if (!hasHistoryTable)
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "CREATE TABLE IF NOT EXISTS __EFMigrationsHistory (MigrationId TEXT NOT NULL CONSTRAINT PK___EFMigrationsHistory PRIMARY KEY, ProductVersion TEXT NOT NULL);");
        }

        var latestMigration = dbContext.Database.GetMigrations().LastOrDefault();
        if (string.IsNullOrWhiteSpace(latestMigration))
        {
            return;
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            "INSERT OR IGNORE INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ({0}, {1});",
            latestMigration,
            "9.0.0");
    }

    private static async Task<bool> TableExistsAsync(AppDbContext dbContext, string tableName)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = $name";

            var parameter = command.CreateParameter();
            parameter.ParameterName = "$name";
            parameter.Value = tableName;
            command.Parameters.Add(parameter);

            var scalar = await command.ExecuteScalarAsync();
            return Convert.ToInt64(scalar) > 0;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task EnsureSeedUsersAsync(IServiceProvider serviceProvider)
    {
        var userRepository = serviceProvider.GetRequiredService<IUserRepository>();
        var registerUserUseCase = serviceProvider.GetRequiredService<IRegisterUserUseCase>();
        var assignRoleUseCase = serviceProvider.GetRequiredService<IAssignUserRoleUseCase>();

        await EnsureUserAsync(
            email: "admin@homeworkjudge.local",
            fullName: "System Admin",
            password: "Admin123!",
            role: UserRoleDto.Admin,
            userRepository,
            registerUserUseCase,
            assignRoleUseCase);

        await EnsureUserAsync(
            email: "teacher@homeworkjudge.local",
            fullName: "Default Teacher",
            password: "Teacher123!",
            role: UserRoleDto.Teacher,
            userRepository,
            registerUserUseCase,
            assignRoleUseCase);

        await EnsureUserAsync(
            email: "student@homeworkjudge.local",
            fullName: "Default Student",
            password: "Student123!",
            role: UserRoleDto.Student,
            userRepository,
            registerUserUseCase,
            assignRoleUseCase);
    }

    private static async Task EnsureSeedClassroomAsync(IServiceProvider serviceProvider, AppDbContext dbContext)
    {
        const string sampleClassroomName = "Sample Classroom";

        var userRepository = serviceProvider.GetRequiredService<IUserRepository>();
        var createClassroomUseCase = serviceProvider.GetRequiredService<ICreateClassroomUseCase>();
        var joinClassroomUseCase = serviceProvider.GetRequiredService<IJoinClassroomUseCase>();

        var teacher = await userRepository.GetByEmailAsync("teacher@homeworkjudge.local");
        var student = await userRepository.GetByEmailAsync("student@homeworkjudge.local");
        if (teacher is null || student is null)
        {
            return;
        }

        var existingClassroomRecord = await dbContext.Classrooms
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TeacherId == teacher.Id.Value && x.Name == sampleClassroomName);

        Guid classroomId;
        string joinCode;

        if (existingClassroomRecord is null)
        {
            var created = await createClassroomUseCase.HandleAsync(
                new CreateClassroomRequestDto(sampleClassroomName, teacher.Id.Value));

            classroomId = created.ClassroomId;
            joinCode = created.JoinCode;
        }
        else
        {
            classroomId = existingClassroomRecord.Id;
            joinCode = existingClassroomRecord.JoinCode;
        }

        dbContext.ChangeTracker.Clear();

        var alreadyJoined = await dbContext.ClassroomStudents
            .AsNoTracking()
            .AnyAsync(x => x.ClassroomId == classroomId && x.StudentId == student.Id.Value);

        if (!alreadyJoined)
        {
            await joinClassroomUseCase.HandleAsync(new JoinClassroomRequestDto(joinCode, student.Id.Value));
        }
    }

    private static async Task EnsureUserAsync(
        string email,
        string fullName,
        string password,
        UserRoleDto role,
        IUserRepository userRepository,
        IRegisterUserUseCase registerUserUseCase,
        IAssignUserRoleUseCase assignRoleUseCase)
    {
        var existing = await userRepository.GetByEmailAsync(email);
        if (existing is null)
        {
            await registerUserUseCase.HandleAsync(
                new RegisterUserRequestDto(email, fullName, role, password));
            return;
        }

        await assignRoleUseCase.HandleAsync(new AssignUserRoleRequestDto(existing.Id.Value, role));
    }
}
