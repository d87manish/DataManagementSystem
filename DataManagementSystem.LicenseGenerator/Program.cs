/**
 * DMS License Generator
 * =====================
 * Developer-only tool. Never ship this to clients.
 *
 * COMMANDS
 * ────────
 * --genkeys  [--out DIR]
 *     Generates RSA-2048 key pair.
 *     private.pem → keep secret, never ship
 *     public.pem  → copy into LicenseManager.cs PublicKeyPem
 *
 * --create
 *     --machine  XXXX-XXXX-XXXX-XXXX   (from client's LicenseWindow)
 *     --client   "Client Name"
 *     --project  DMS
 *     --days     365
 *     [--privkey path\to\private.pem]
 *     Prints the license string. Send this to the client.
 *
 * --info <license_string>
 *     Decodes and prints the license payload.
 *
 * TYPICAL WORKFLOW
 * ────────────────
 * 1. Run once: DMS.LicenseGenerator.exe --genkeys
 * 2. Copy public key PEM into DataManagementSystem\Licensing\LicenseManager.cs
 * 3. Rebuild and ship the app.
 * 4. Client runs app → sees Machine ID in LicenseWindow → sends to you.
 * 5. Run: DMS.LicenseGenerator.exe --create --machine XXXX-XXXX-XXXX-XXXX --client "ABC Ltd" --project DMS --days 365
 * 6. Send license string to client. They paste it in LicenseWindow → Activate.
 * 7. On each installer version, repeat step 5 with the same or new machine ID.
 */

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

if (args.Length == 0) { ShowHelp(); return; }

switch (args[0].ToLowerInvariant())
{
    case "--genkeys": GenKeys(args);         break;
    case "--create":  CreateLicense(args);   break;
    case "--info":    ShowInfo(args);        break;
    default:
        Console.WriteLine($"Unknown command: {args[0]}");
        ShowHelp();
        break;
}

// ── --genkeys ─────────────────────────────────────────────────────────────────

static void GenKeys(string[] args)
{
    var outDir   = ArgValue(args, "--out") ?? Directory.GetCurrentDirectory();
    Directory.CreateDirectory(outDir);

    var privPath = Path.Combine(outDir, "private.pem");
    var pubPath  = Path.Combine(outDir, "public.pem");

    using var rsa = RSA.Create(2048);
    File.WriteAllText(privPath, rsa.ExportRSAPrivateKeyPem(),        Encoding.ASCII);
    File.WriteAllText(pubPath,  rsa.ExportSubjectPublicKeyInfoPem(), Encoding.ASCII);

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("RSA-2048 key pair generated.");
    Console.ResetColor();
    Console.WriteLine($"  Private key : {privPath}  ← KEEP SECRET, DO NOT SHIP");
    Console.WriteLine($"  Public key  : {pubPath}");
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("── Copy the following into LicenseManager.cs PublicKeyPem ──────");
    Console.ResetColor();
    Console.WriteLine(File.ReadAllText(pubPath));
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("────────────────────────────────────────────────────────────────");
    Console.ResetColor();
}

// ── --create ──────────────────────────────────────────────────────────────────

static void CreateLicense(string[] args)
{
    var machineId   = ArgValue(args, "--machine")  ?? Abort("--machine is required.");
    var clientName  = ArgValue(args, "--client")   ?? Abort("--client is required.");
    var projectCode = ArgValue(args, "--project")  ?? Abort("--project is required.");
    var daysStr     = ArgValue(args, "--days")     ?? "365";
    var privKeyPath = ArgValue(args, "--privkey")  ?? "private.pem";

    if (!int.TryParse(daysStr, out int days) || days <= 0)
        Abort($"--days must be a positive integer (got '{daysStr}').");

    if (!File.Exists(privKeyPath))
        Abort($"Private key not found: {privKeyPath}\nRun --genkeys first.");

    var payload = new LicensePayload
    {
        ClientName  = clientName,
        MachineId   = machineId.ToUpperInvariant(),
        ProjectCode = projectCode.ToUpperInvariant(),
        IssuedOn    = DateTime.UtcNow.Date,
        ValidTo     = DateTime.UtcNow.Date.AddDays(days),
    };

    var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload,
        new JsonSerializerOptions { WriteIndented = false });

    using var rsa = RSA.Create();
    rsa.ImportFromPem(File.ReadAllText(privKeyPath).AsSpan());
    var signature     = rsa.SignData(payloadBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    var licenseString = $"V1.{ToBase64Url(payloadBytes)}.{ToBase64Url(signature)}";

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("License generated successfully.");
    Console.ResetColor();
    Console.WriteLine();
    Console.WriteLine($"  Client  : {clientName}");
    Console.WriteLine($"  Machine : {machineId.ToUpperInvariant()}");
    Console.WriteLine($"  Product : {projectCode.ToUpperInvariant()}");
    Console.WriteLine($"  Valid   : {payload.IssuedOn:dd MMM yyyy} → {payload.ValidTo:dd MMM yyyy} ({days} days)");
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("── License Key (send this to the client) ───────────────────────");
    Console.ResetColor();
    Console.WriteLine(licenseString);
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("────────────────────────────────────────────────────────────────");
    Console.ResetColor();
}

// ── --info ────────────────────────────────────────────────────────────────────

static void ShowInfo(string[] args)
{
    var licStr = args.Length > 1 ? args[1] : null;
    if (string.IsNullOrWhiteSpace(licStr)) Abort("Usage: --info <license_string>");

    var parts = licStr!.Trim().Split('.');
    if (parts.Length != 3 || parts[0] != "V1") { Console.WriteLine("Invalid license format."); return; }

    try
    {
        var data = JsonSerializer.Deserialize<LicensePayload>(FromBase64Url(parts[1]));
        if (data == null) { Console.WriteLine("Could not decode payload."); return; }
        Console.WriteLine($"  Client   : {data.ClientName}");
        Console.WriteLine($"  Machine  : {data.MachineId}");
        Console.WriteLine($"  Product  : {data.ProjectCode}");
        Console.WriteLine($"  Issued   : {data.IssuedOn:dd MMM yyyy}");
        Console.WriteLine($"  Valid to : {data.ValidTo:dd MMM yyyy}");
        Console.WriteLine($"  Expired  : {(DateTime.UtcNow.Date > data.ValidTo.Date ? "YES" : "no")}");
        Console.WriteLine("(Signature not verified — use the app to verify)");
    }
    catch (Exception ex) { Console.WriteLine($"Decode failed: {ex.Message}"); }
}

// ── Helpers ───────────────────────────────────────────────────────────────────

static void ShowHelp()
{
    Console.WriteLine("DMS License Generator");
    Console.WriteLine();
    Console.WriteLine("  --genkeys  [--out DIR]");
    Console.WriteLine("      Generate RSA-2048 key pair (run once, copy public key into app).");
    Console.WriteLine();
    Console.WriteLine("  --create --machine XXXX-XXXX-XXXX-XXXX --client \"Name\" --project DMS --days 365");
    Console.WriteLine("      Issue a license. Outputs the license key to send to the client.");
    Console.WriteLine();
    Console.WriteLine("  --info <license_string>");
    Console.WriteLine("      Decode and display license contents (no signature check).");
}

static string ToBase64Url(byte[] bytes)
    => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

static byte[] FromBase64Url(string s)
{
    s = s.Replace('-', '+').Replace('_', '/');
    switch (s.Length % 4) { case 2: s += "=="; break; case 3: s += "="; break; }
    return Convert.FromBase64String(s);
}

static string? ArgValue(string[] args, string flag)
{
    var i = Array.IndexOf(args, flag);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

static string Abort(string msg)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"Error: {msg}");
    Console.ResetColor();
    Environment.Exit(1);
    return "";
}

class LicensePayload
{
    public string   Version     { get; set; } = "1";
    public string   ClientName  { get; set; } = "";
    public string   MachineId   { get; set; } = "";
    public string   ProjectCode { get; set; } = "";
    public DateTime IssuedOn    { get; set; }
    public DateTime ValidTo     { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? Features   { get; set; }
}
