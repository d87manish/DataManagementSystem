using System.IO;
using System.Text.Json;

namespace DataManagementSystem.Config;

public static class ConfigBootstrapper
{
    private static readonly string ProgramDataDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Braentech", "DMS");

    public static string ResolveProgramDataDir() => ProgramDataDir;

    public static AppSettings Load(string appBaseDir, string defaultJson)
    {
        Directory.CreateDirectory(ProgramDataDir);
        var programDataPath = Path.Combine(ProgramDataDir, "appsettings.json");
        var baseAppPath     = Path.Combine(appBaseDir, "appsettings.json");

        if (File.Exists(baseAppPath))
        {
            bool shouldCopy = !File.Exists(programDataPath)
                || File.GetLastWriteTimeUtc(baseAppPath) > File.GetLastWriteTimeUtc(programDataPath);

            if (shouldCopy)
            {
                File.Copy(baseAppPath, programDataPath, overwrite: true);
                Logger.LogInfo($"Seeded/updated appsettings.json to {programDataPath}", "ConfigBootstrapper");
            }
        }

        var path = File.Exists(programDataPath) ? programDataPath : baseAppPath;

        string json;
        try   { json = File.Exists(path) ? File.ReadAllText(path) : defaultJson; }
        catch { json = defaultJson; }

        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new AppSettings();

            // Ensure defaults for any missing sections
            var defaults = JsonSerializer.Deserialize<AppSettings>(defaultJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

            if (string.IsNullOrWhiteSpace(settings.Database.ConnectionString))
                settings.Database.ConnectionString = defaults.Database.ConnectionString;

            Logger.LogInfo($"Config loaded from {path}", "ConfigBootstrapper");
            return settings;
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to parse appsettings.json — using defaults", ex, "ConfigBootstrapper");
            return JsonSerializer.Deserialize<AppSettings>(defaultJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        }
    }
}
