using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Application.DependencyInjection;
using Domain.Ports;
using HomeWorkJudge.UI.ViewModels;
using HomeWorkJudge.UI.Views;
using InfrastructureService.Configuration;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SqliteDataAccess;

namespace HomeWorkJudge.UI;

public partial class App : System.Windows.Application
{
    private const string SingleInstanceMutexName = "HomeWorkJudge.UI.SingleInstance";
    private static Mutex? _singleInstanceMutex;

    private IHost? _host;

    private static string ErrorLogPath
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HomeWorkJudge");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "startup-error.log");
        }
    }

    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Prevent multiple hidden instances that lock binaries and DB files.
        _singleInstanceMutex = new Mutex(initiallyOwned: true, name: SingleInstanceMutexName, createdNew: out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                "HomeWork Judge dang chay o tien trinh khac.",
                "HomeWork Judge",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown(0);
            return;
        }

        try
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

                    // Infrastructure
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

                    services.AddInfrastructureServices(configuration);
                    services.AddApplicationServices();

                    // ViewModels
                    services.AddSingleton<MainViewModel>();
                    services.AddTransient<RubricListViewModel>();
                    services.AddTransient<SessionListViewModel>();
                    services.AddTransient<SessionCreateViewModel>();
                    services.AddTransient<GradingDashboardViewModel>();
                    services.AddTransient<SubmissionReviewViewModel>();
                    services.AddTransient<SettingsViewModel>();

                    // Views
                    services.AddTransient<MainWindow>();
                })
                .Build();

            using var startupCts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await _host.StartAsync(startupCts.Token);

            var db = _host.Services.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync(startupCts.Token);

            // Schema migration: thêm cột mới nếu chưa tồn tại (tương thích với DB cũ)
            await ApplySchemaMigrationsAsync(db, startupCts.Token);

            // Restore saved theme preference
            var config = _host.Services.GetRequiredService<IConfiguration>();
            if (bool.TryParse(config["UI:DarkMode"], out var isDark))
            {
                var paletteHelper = new PaletteHelper();
                var theme = paletteHelper.GetTheme();
                theme.SetBaseTheme(isDark ? BaseTheme.Dark : BaseTheme.Light);
                paletteHelper.SetTheme(theme);
            }

            var mainWindow = _host.Services.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            mainWindow.Show();
            mainWindow.Activate();
        }
        catch (Exception ex)
        {
            AppendErrorLog("Startup", ex);
            MessageBox.Show(
                $"Loi khoi dong:\n\n{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}\n\nInner: {ex.InnerException}",
                "HomeWork Judge - Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        try
        {
            if (_host is not null)
            {
                using var stopCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await _host.StopAsync(stopCts.Token);
                _host.Dispose();
            }
        }
        catch
        {
            // Ignore shutdown errors to avoid hanging process termination.
        }
        finally
        {
            if (_singleInstanceMutex is not null)
            {
                try
                {
                    _singleInstanceMutex.ReleaseMutex();
                }
                catch
                {
                    // Ignore if ownership has already been released.
                }

                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
            }

            base.OnExit(e);
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppendErrorLog("DispatcherUnhandled", e.Exception);
        MessageBox.Show(
            $"Loi khong xu ly:\n\n{e.Exception.GetType().Name}: {e.Exception.Message}\n\n{e.Exception.StackTrace}",
            "HomeWork Judge - Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        AppendErrorLog("Unhandled", e.ExceptionObject as Exception);
        MessageBox.Show(
            $"Fatal:\n\n{e.ExceptionObject}",
            "HomeWork Judge - Fatal",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        AppendErrorLog("UnobservedTask", e.Exception);
        MessageBox.Show(
            $"Unobserved Task:\n\n{e.Exception}",
            "HomeWork Judge - Task Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.SetObserved();
    }

    /// <summary>
    /// Áp dụng các thay đổi schema thủ công cho DB đã tồn tại trước đây.
    /// An toàn khi chạy nhiều lần (idempotent) — bỏ qua lỗi khi cột đã tồn tại.
    /// </summary>
    private static async Task ApplySchemaMigrationsAsync(SqliteDataAccess.AppDbContext db, CancellationToken ct)
    {
        await db.ApplySchemaMigrationsAsync(ct);
    }

    private static void AppendErrorLog(string phase, Exception? ex)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("========================================");
            sb.AppendLine($"UTC: {DateTime.UtcNow:O}");
            sb.AppendLine($"Phase: {phase}");
            sb.AppendLine(ex?.ToString() ?? "<null exception>");
            File.AppendAllText(ErrorLogPath, sb.ToString());
        }
        catch
        {
            // Never throw from logger paths.
        }
    }
}
