using System.IO;
using System.Windows;
using Application.DependencyInjection;
using InfrastructureService.Configuration;
using HomeWorkJudge.UI.ViewModels;
using HomeWorkJudge.UI.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Domain.Ports;
using SqliteDataAccess;

namespace HomeWorkJudge.UI;

public partial class App : System.Windows.Application
{
    private readonly IHost _host;

    public App()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((ctx, config) =>
            {
                config.SetBasePath(AppContext.BaseDirectory);
                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
            })
            .ConfigureServices((ctx, services) =>
            {
                var configuration = ctx.Configuration;

                // ── Infrastructure ──────────────────────────────────────
                // SQLite (Repositories + UoW)
                var dbPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "HomeWorkJudge", "homeworkjudge.db");
                Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

                services.AddDbContext<AppDbContext>(options =>
                    Microsoft.EntityFrameworkCore.SqliteDbContextOptionsBuilderExtensions
                        .UseSqlite(options, $"Data Source={dbPath}"));
                services.AddScoped<IUnitOfWork, SqliteUnitOfWork>();
                services.AddScoped<IRubricRepository, SqliteDataAccess.Repository.SqliteRubricRepository>();
                services.AddScoped<IGradingSessionRepository, SqliteDataAccess.Repository.SqliteGradingSessionRepository>();
                services.AddScoped<ISubmissionRepository, SqliteDataAccess.Repository.SqliteSubmissionRepository>();

                // InfrastructureService (AI, Plagiarism, FileExtractor, Report)
                services.AddInfrastructureServices(configuration);

                // Application (Use Case Handlers + EventDispatcher)
                services.AddApplicationServices();

                // ── ViewModels ──────────────────────────────────────────
                services.AddTransient<MainViewModel>();
                services.AddTransient<RubricListViewModel>();
                services.AddTransient<RubricEditorViewModel>();
                services.AddTransient<SessionListViewModel>();
                services.AddTransient<SessionCreateViewModel>();
                services.AddTransient<GradingDashboardViewModel>();
                services.AddTransient<SubmissionReviewViewModel>();
                services.AddTransient<SettingsViewModel>();

                // ── Views ───────────────────────────────────────────────
                services.AddTransient<MainWindow>();
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await _host.StartAsync();

        // Ensure DB is created
        using (var scope = _host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync();
        }

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync();
        _host.Dispose();
        base.OnExit(e);
    }
}
