using System;
using System.Threading.Tasks;
using Domain.Ports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ports.DTO.Common;
using Ports.DTO.User;
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
        await dbContext.Database.EnsureCreatedAsync();

        await EnsureSeedUsersAsync(serviceProvider);
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
