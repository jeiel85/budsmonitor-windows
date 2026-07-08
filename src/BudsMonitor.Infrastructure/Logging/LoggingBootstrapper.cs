using BudsMonitor.Infrastructure.Storage;
using Serilog;

namespace BudsMonitor.Infrastructure.Logging;

/// <summary>
/// Configures the global Serilog logger for file logging under
/// %LocalAppData%\BudsMonitor\logs. Files roll daily and at 10 MB, and are retained
/// for 14 days (see docs/09-storage-privacy log retention).
/// </summary>
public static class LoggingBootstrapper
{
    public static void Initialize(StoragePaths paths)
    {
        Directory.CreateDirectory(paths.LogsDirectory);
        var logFile = Path.Combine(paths.LogsDirectory, "app-.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.File(
                path: logFile,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                fileSizeLimitBytes: 10L * 1024 * 1024,
                rollOnFileSizeLimit: true,
                shared: true,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }

    public static void Shutdown() => Log.CloseAndFlush();
}
