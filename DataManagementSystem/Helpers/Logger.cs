using System.IO;
using System.Reflection;

namespace DataManagementSystem.Helpers;

public static class Logger
{
    private static readonly string LogDirectory;
    private static readonly object LockObject = new();

    static Logger()
    {
        string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        LogDirectory = Path.Combine(programData, "Braentech", "Logs");
        try   { Directory.CreateDirectory(LogDirectory); }
        catch { LogDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs"); Directory.CreateDirectory(LogDirectory); }
    }

    public static string LogDirectory_ => LogDirectory;

    private static string GetLogFile()   => Path.Combine(LogDirectory, $"Error_{DateTime.Now:yyyy-MM-dd}.log");
    private static string GetCrashFile() => Path.Combine(LogDirectory, $"Crash_{DateTime.Now:yyyy-MM-dd}.log");

    public static void LogError(string message, Exception? ex = null, string? source = null)
    {
        try
        {
            lock (LockObject)
            {
                using var w = new StreamWriter(GetLogFile(), true);
                w.WriteLine("========================================");
                w.WriteLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                w.WriteLine($"Level: ERROR");
                if (source != null) w.WriteLine($"Source: {source}");
                w.WriteLine($"Message: {message}");
                if (ex != null)
                {
                    w.WriteLine($"Exception: {ex.GetType().FullName}: {ex.Message}");
                    w.WriteLine($"Stack Trace: {ex.StackTrace}");
                    if (ex.InnerException != null)
                        w.WriteLine($"Inner: {ex.InnerException.GetType().FullName}: {ex.InnerException.Message}");
                }
                w.WriteLine("========================================");
                w.WriteLine();
            }
        }
        catch { }
    }

    public static void LogWarning(string message, string? source = null)
    {
        try
        {
            lock (LockObject)
            {
                using var w = new StreamWriter(GetLogFile(), true);
                w.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [WARNING] {(source != null ? $"[{source}] " : "")}{message}");
            }
        }
        catch { }
    }

    public static void LogInfo(string message, string? source = null)
    {
        try
        {
            lock (LockObject)
            {
                using var w = new StreamWriter(GetLogFile(), true);
                w.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [INFO] {(source != null ? $"[{source}] " : "")}{message}");
            }
        }
        catch { }
    }

    public static void LogCrash(string context, Exception? ex)
    {
        try
        {
            lock (LockObject)
            {
                using var w = new StreamWriter(GetCrashFile(), append: true);
                w.WriteLine("╔══════════════════════════════════════════════════════╗");
                w.WriteLine($"║  CRASH REPORT — {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
                w.WriteLine("╚══════════════════════════════════════════════════════╝");
                w.WriteLine($"Context : {context}");
                w.WriteLine($"OS      : {Environment.OSVersion}");
                w.WriteLine($"App Ver : {Assembly.GetExecutingAssembly().GetName().Version}");
                w.WriteLine($"CLR     : {Environment.Version}");
                w.WriteLine($"User    : {Environment.UserName}");
                w.WriteLine();
                if (ex != null) WriteExceptionChain(w, ex);
                else w.WriteLine("(no exception object)");
                w.WriteLine();
            }
        }
        catch { }
    }

    private static void WriteExceptionChain(StreamWriter w, Exception ex, int depth = 0)
    {
        string indent = new(' ', depth * 2);
        w.WriteLine($"{indent}Type    : {ex.GetType().FullName}");
        w.WriteLine($"{indent}Message : {ex.Message}");
        w.WriteLine($"{indent}Stack   :");
        foreach (var line in (ex.StackTrace ?? "(none)").Split('\n'))
            w.WriteLine($"{indent}  {line.TrimEnd()}");
        if (ex.InnerException != null) { w.WriteLine($"{indent}--- Inner ---"); WriteExceptionChain(w, ex.InnerException, depth + 1); }
    }

    public static void CleanupOldLogs(int daysToKeep = 30)
    {
        try
        {
            var cutoff = DateTime.Now.AddDays(-daysToKeep);
            foreach (var file in new System.IO.DirectoryInfo(LogDirectory).GetFiles("*.log"))
                if (file.CreationTime < cutoff) file.Delete();
        }
        catch { }
    }
}
