using System.IO;
using System.Text.Json;

namespace DataManagementSystem.Config;

public class ApplicationModeService
{
    public bool IsDevelopment { get; }

    public ApplicationModeService()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "application-mode.json");
            if (!File.Exists(path)) { IsDevelopment = false; return; }
            var doc  = JsonDocument.Parse(File.ReadAllText(path));
            IsDevelopment = doc.RootElement.TryGetProperty("Mode", out var m) && m.GetInt32() == 1;
        }
        catch { IsDevelopment = false; }
    }
}
