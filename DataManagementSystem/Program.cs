using System.IO;
using DataManagementSystem.Config;
using DataManagementSystem.Licensing;

namespace DataManagementSystem;

// Entry point class. Kept separate from App so that WPF's build system
// does not also generate a Main() in App.g.cs, which would cause CS0017.
//
// CLI modes (called by the installer):
//   --activate <key>   Validate + write license.lic. Exit 0=OK, non-zero=fail.
//   --machine-id       Write machine fingerprint to %TEMP%\DMS_MachineId.txt.
internal static class Program
{
    [System.STAThread]
    public static void Main(string[] args)
    {
        if (args.Length >= 1)
        {
            switch (args[0].ToLowerInvariant())
            {
                case "--activate" when args.Length >= 2:
                    Environment.Exit(RunActivateCli(args[1]));
                    return;

                case "--machine-id":
                    RunMachineIdCli();
                    return;
            }
        }

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }

    private static int RunActivateCli(string licenseKey)
    {
        var resultFile = Path.Combine(Path.GetTempPath(), "DMS_Activate_Result.txt");
        try
        {
            var settings = App.LoadConfig();
            var manager  = new LicenseManager(settings.ProjectProfile.ProjectCode);
            var result   = manager.Activate(licenseKey.Trim());

            File.WriteAllText(resultFile, result.IsValid
                ? $"OK|{result.License?.ClientName}"
                : $"FAIL|{result.Message ?? "Activation failed."}");

            return result.IsValid ? 0 : 1;
        }
        catch (Exception ex)
        {
            try { File.WriteAllText(resultFile, $"FAIL|{ex.Message}"); } catch { }
            return 99;
        }
    }

    private static void RunMachineIdCli()
    {
        try
        {
            var id = MachineIdProvider.GetMachineId();
            File.WriteAllText(
                Path.Combine(Path.GetTempPath(), "DMS_MachineId.txt"), id);
        }
        catch { }
    }
}
