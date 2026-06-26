using System.IO;
using System.Windows.Threading;
using DataManagementSystem.Config;
using DataManagementSystem.Data;
using DataManagementSystem.Licensing;
using DataManagementSystem.Services;
using DataManagementSystem.ViewModels;
using DataManagementSystem.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DataManagementSystem;

public partial class App : Application
{


    private IHost _host = null!;
    private System.Threading.Timer? _backupTimer;

    public static IServiceProvider Services { get; private set; } = null!;
    public static AppSettings Settings     { get; private set; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        RegisterGlobalExceptionHandlers();

        try
        {
            Settings = ConfigBootstrapper.Load(AppContext.BaseDirectory, GetDefaultConfig());

            if (!RunDatabaseSetup()) { Shutdown(1); return; }

            if (!RunLicenseCheckIfRequired()) { Shutdown(0); return; }

            BuildHost();
            Logger.CleanupOldLogs();

            Services.GetRequiredService<LoginWindow>().Show();
        }
        catch (Exception ex)
        {
            Logger.LogCrash("OnStartup failed", ex);
            MessageBox.Show(
                $"Application failed to start.\n\n{ex.Message}\n\nLogs: {Logger.LogDirectory_}",
                "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _backupTimer?.Dispose();
        BackupNow("OnExit");
        if (_host != null) _host.Dispose();
        base.OnExit(e);
    }

    private bool RunDatabaseSetup()
    {
        var setup = new DatabaseSetupService(Settings.Database.ConnectionString);
        if (setup.EnsureCreated(out var error)) return true;

        MessageBox.Show(
            $"Database setup failed:\n\n{error}\n\nLogs: {Logger.LogDirectory_}",
            "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
        return false;
    }

    private bool RunLicenseCheckIfRequired()
    {
        var mode = new ApplicationModeService();
        if (mode.IsDevelopment)
        {
            Logger.LogInfo("Development mode — license check bypassed.", "App");
            return true;
        }

        var licenseManager = new LicenseManager(Settings.ProjectProfile.ProjectCode);
        var result         = licenseManager.Validate();
        if (result.IsValid) return true;

        Logger.LogWarning($"License invalid [{result.Status}] — showing activation window.", "App");
        var win = new LicenseWindow(licenseManager, result);
        if (win.ShowDialog() == true) return true;

        Logger.LogInfo("User closed license window without activating.", "App");
        return false;
    }

    private void BuildHost()
    {
        var resolvedConnString = DatabaseSetupService.Resolve(Settings.Database.ConnectionString);
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(Settings);
                services.AddSingleton(Settings.Modbus);
                services.AddSingleton(Settings.Simulation);
                services.AddSingleton<SessionManager>();

                // Data
                services.AddSingleton(_ => new UserRepository(resolvedConnString));
                services.AddSingleton(_ => new DataCaptureRepository(resolvedConnString));

                // Services
                services.AddSingleton<IAuthService, AuthService>();
                services.AddSingleton<IDataCaptureService, DataCaptureService>();
                if (Settings.Simulation.Enabled)
                    services.AddSingleton<IModbusScanService, SimulatedModbusScanService>();
                else
                    services.AddSingleton<IModbusScanService, ModbusScanService>();

                // ViewModels
                services.AddTransient<LoginViewModel>();
                services.AddSingleton<DataCaptureViewModel>();
                services.AddSingleton<ViewDataViewModel>();
                services.AddTransient<MainViewModel>();

                // Views
                services.AddTransient<LoginWindow>();
                services.AddTransient<MainWindow>();
            })
            .Build();

        Services = _host.Services;

        StartBackupTimer();
    }

    private void StartBackupTimer()
    {
        int hours = Settings.Backup.IntervalHours;
        if (hours <= 0) return;
        _backupTimer = new System.Threading.Timer(_ => BackupNow("Periodic"), null,
            TimeSpan.FromHours(hours), TimeSpan.FromHours(hours));
    }

    private static void BackupNow(string reason)
    {
        try
        {
            var resolved = DatabaseSetupService.Resolve(Settings.Database.ConnectionString);
            var builder  = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(resolved);
            var dbPath   = builder.DataSource;
            if (!File.Exists(dbPath)) return;

            var backupDir = Path.Combine(ConfigBootstrapper.ResolveProgramDataDir(), "Backups");
            Directory.CreateDirectory(backupDir);
            var destPath = Path.Combine(backupDir, $"dms_backup_{reason}_{DateTime.Now:yyyyMMdd_HHmmss}.db");
            File.Copy(dbPath, destPath, overwrite: true);

            PruneBackups(backupDir, Settings.Backup.RetainCount);
            Logger.LogInfo($"Backup [{reason}]: {destPath}", "App");
        }
        catch (Exception ex) { Logger.LogError($"Backup [{reason}] failed", ex, "App"); }
    }

    private static void PruneBackups(string dir, int retain)
    {
        try
        {
            var files = new DirectoryInfo(dir).GetFiles("*.db")
                .OrderByDescending(f => f.CreationTimeUtc)
                .Skip(retain);
            foreach (var f in files) f.Delete();
        }
        catch { }
    }

    private void RegisterGlobalExceptionHandlers()
    {
        DispatcherUnhandledException          += OnDispatcherException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTask;
    }

    private void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Logger.LogCrash("DispatcherUnhandledException", e.Exception);
        BackupNow("Crash");
        MessageBox.Show($"An unexpected error occurred:\n\n{e.Exception.Message}\n\nLogs: {Logger.LogDirectory_}",
            "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnDomainException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        Logger.LogCrash($"AppDomain.UnhandledException (terminating={e.IsTerminating})", ex);
        BackupNow("Crash");
    }

    private void OnUnobservedTask(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        Logger.LogError("Unobserved Task exception", e.Exception, "App");
    }

    internal static AppSettings LoadConfig()
        => ConfigBootstrapper.Load(AppContext.BaseDirectory, GetDefaultConfig());

    private static string GetDefaultConfig() => """
        {
          "Simulation": { "Enabled": false, "IntervalSeconds": 3 },
          "Database": { "ConnectionString": "Data Source=%PROGRAMDATA%\\Braentech\\DMS\\dms.db" },
          "Backup":   { "IntervalHours": 4, "RetainCount": 7 },
          "Modbus": {
            "IpAddress": "192.168.1.1", "Port": 502,
            "SlaveAddress": 1, "TriggerRegister": 100,
            "SerialNumberRegister": 101, "SerialNumberRegisters": 8,
            "ModelNumberRegister": 109, "ModelNumberRegisters": 4,
            "PollIntervalMs": 500, "ConnectTimeoutMs": 3000
          },
          "ProjectProfile": { "ProjectCode": "DMS", "ProjectName": "Data Management System" }
        }
        """;
}
