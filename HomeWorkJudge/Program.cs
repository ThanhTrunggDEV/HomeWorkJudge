using System;
using Application.DependencyInjection;
using Domain.Ports;
using HomeWorkJudge.Infrastructure;
using InfrastructureService.Configuration;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SqliteDataAccess;
using SqliteDataAccess.Repository;
using System.Threading.RateLimiting;

namespace HomeWorkJudge;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
            ?? "Data Source=homeworkjudge.db";

        builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));

        builder.Services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/Auth/Login";
                options.AccessDeniedPath = "/Home/StatusCode?code=403";
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.Cookie.SameSite = SameSiteMode.Lax;
            });

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("TeacherOrAdmin", policy =>
                policy.RequireRole("Teacher", "Admin"));
        });

        builder.Services.AddAntiforgery();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddControllersWithViews(options =>
        {
            options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
        });

        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 300,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));

            options.AddPolicy("auth", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
        });

        builder.Services.AddScoped<IUserRepository, SqliteUserRepository>();
        builder.Services.AddScoped<IClassroomRepository, SqliteClassroomRepository>();
        builder.Services.AddScoped<IAssignmentRepository, SqliteAssignmentRepository>();
        builder.Services.AddScoped<ISubmissionRepository, SqliteSubmissionRepository>();
        builder.Services.AddScoped<IUnitOfWork, SqliteUnitOfWork>();

        builder.Services.AddInfrastructureServiceFoundation(builder.Configuration, builder.Environment);
        builder.Services.AddApplicationUseCases();

        builder.Services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>("sqlite")
            .AddCheck("queue.provider", () =>
            {
                var provider = builder.Configuration["Infrastructure:Queue:Provider"];
                return string.IsNullOrWhiteSpace(provider)
                    ? HealthCheckResult.Unhealthy("Infrastructure:Queue:Provider is missing")
                    : HealthCheckResult.Healthy($"Queue provider: {provider}");
            })
            .AddCheck("ai.provider", () =>
            {
                var provider = builder.Configuration["Infrastructure:AI:Provider"];
                return string.IsNullOrWhiteSpace(provider)
                    ? HealthCheckResult.Unhealthy("Infrastructure:AI:Provider is missing")
                    : HealthCheckResult.Healthy($"AI provider: {provider}");
            });

        var app = builder.Build();

        var shouldBootstrapDatabase =
            builder.Configuration.GetValue<bool?>("Bootstrap:InitializeDatabase")
            ?? app.Environment.IsDevelopment();

        if (shouldBootstrapDatabase)
        {
            DatabaseBootstrap.InitializeAsync(app).GetAwaiter().GetResult();
        }

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Home/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseRouting();
        app.UseRateLimiter();

        app.Use(async (context, next) =>
        {
            context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
            context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
            context.Response.Headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
            context.Response.Headers.TryAdd("Content-Security-Policy",
                "default-src 'self'; " +
                "script-src 'self'; " +
                "style-src 'self' 'unsafe-inline'; " +
                "img-src 'self' data:; " +
                "font-src 'self'; " +
                "object-src 'none'; " +
                "base-uri 'self'; " +
                "frame-ancestors 'none'; " +
                "form-action 'self'");

            await next();
        });

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseStatusCodePagesWithReExecute("/Home/StatusCode", "?code={0}");

        app.MapHealthChecks("/health");

        app.MapStaticAssets();
        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}")
            .WithStaticAssets();

        app.Run();
    }
}
