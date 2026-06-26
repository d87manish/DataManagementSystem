using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DataManagementSystem.Licensing;

/// <summary>
/// RSA-2048 offline license manager. Same architecture as WeightColorSystem.
///
/// SETUP: Run DMS.LicenseGenerator.exe --genkeys once, copy public key PEM below.
/// Issue licenses with: DMS.LicenseGenerator.exe --create --machine XXXX-XXXX-XXXX-XXXX --client "Name" --project DMS --days 365
/// </summary>
public class LicenseManager
{
    internal const string PublicKeyPem =
        """
        -----BEGIN PUBLIC KEY-----
        MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA+f1gQK24Xjz1M5bu/4Tl
        99+3emng1pPoy1Vrlo9YSvsLFnOGg7sYeKghxdKVg1W5ls8nmpa61B3GyfMuUl+l
        p3gULBGf4US7/u6P/EoUc48TLiHpIppQd9io43XYp+2JsZvWLqN1N+jEfo4TUVUY
        NVq5pNxaf5EUetHToNONXQNO1njq79H0n10kdaP2VgHNoVn6RLQ35OWbY1aidxe6
        TTXEjEBq6pT4iTgl6tMPtPr8cC+o0M+oEWJb4Hs+mr9kjfj4xw/A402zuxpPAaWe
        g1u6btzxiwJgE6MrM81BMPNZ4K0C+k9U/+K4Xpzgjj39aGrCIRxQG65SCuyfJeGT
        CQIDAQAB
        -----END PUBLIC KEY-----
        """;

    private static readonly string LicenseDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Braentech", "DMS");

    public static readonly string LicensePath = Path.Combine(LicenseDir, "license.lic");

    private readonly string _projectCode;

    public string       MachineId       { get; } = MachineIdProvider.GetMachineId();
    public LicenseData? CurrentLicense  { get; private set; }

    public LicenseManager(string projectCode) => _projectCode = projectCode;

    public LicenseValidationResult Validate()
    {
        if (!File.Exists(LicensePath))
            return Fail(LicenseStatus.NotFound, "No license file found.");

        string content;
        try   { content = File.ReadAllText(LicensePath).Trim(); }
        catch (Exception ex) { Logger.LogError("Cannot read license file", ex, "LicenseManager"); return Fail(LicenseStatus.Corrupt, "License file could not be read."); }

        return ValidateLicenseString(content);
    }

    public LicenseValidationResult ValidateLicenseString(string licenseString)
    {
        try
        {
            var parts = licenseString.Trim().Split('.');
            if (parts.Length != 3 || parts[0] != "V1")
                return Fail(LicenseStatus.Corrupt, "Invalid license format.");

            byte[] payload, signature;
            try { payload = FromBase64Url(parts[1]); signature = FromBase64Url(parts[2]); }
            catch { return Fail(LicenseStatus.Corrupt, "License string contains invalid base64."); }

            if (!VerifySignature(payload, signature))
                return Fail(LicenseStatus.InvalidSignature, "License signature is invalid.");

            LicenseData? data;
            try { data = JsonSerializer.Deserialize<LicenseData>(payload); }
            catch { data = null; }
            if (data == null) return Fail(LicenseStatus.Corrupt, "License payload could not be read.");

            if (data.MachineId != "*" && !data.MachineId.Equals(MachineId, StringComparison.OrdinalIgnoreCase))
                return Fail(LicenseStatus.WrongMachine,
                    $"This license is locked to machine {data.MachineId}.\nThis machine ID is: {MachineId}");

            if (!data.ProjectCode.Equals(_projectCode, StringComparison.OrdinalIgnoreCase))
                return Fail(LicenseStatus.WrongProduct,
                    $"This license is for product '{data.ProjectCode}', not '{_projectCode}'.");

            if (DateTime.UtcNow.Date > data.ValidTo.Date)
                return Fail(LicenseStatus.Expired,
                    $"License expired on {data.ValidTo:dd MMM yyyy}. Contact Braentech for renewal.");

            CurrentLicense = data;
            Logger.LogInfo($"License OK — Client: {data.ClientName} | Valid to: {data.ValidTo:dd MMM yyyy}", "LicenseManager");
            return new LicenseValidationResult(LicenseStatus.Valid, data);
        }
        catch (Exception ex)
        {
            Logger.LogError("License validation threw", ex, "LicenseManager");
            return Fail(LicenseStatus.Corrupt, "License validation failed unexpectedly.");
        }
    }

    public LicenseValidationResult Activate(string licenseString)
    {
        var result = ValidateLicenseString(licenseString);
        if (!result.IsValid) return result;

        try
        {
            Directory.CreateDirectory(LicenseDir);
            File.WriteAllText(LicensePath, licenseString.Trim(), Encoding.UTF8);
            Logger.LogInfo("License saved to disk.", "LicenseManager");
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to save license file", ex, "LicenseManager");
            return Fail(LicenseStatus.Corrupt, "Activation succeeded but license could not be saved. Check write permissions to " + LicenseDir);
        }

        return result;
    }

    private static bool VerifySignature(byte[] payload, byte[] signature)
    {
        if (PublicKeyPem.Contains("REPLACE_WITH_YOUR_PUBLIC_KEY"))
        {
            Logger.LogWarning("License public key not configured. Run DMS.LicenseGenerator.exe --genkeys.", "LicenseManager");
            return false;
        }
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(PublicKeyPem.AsSpan());
            return rsa.VerifyData(payload, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch (Exception ex) { Logger.LogError("RSA verify failed", ex, "LicenseManager"); return false; }
    }

    private static LicenseValidationResult Fail(LicenseStatus status, string message)
    {
        Logger.LogWarning($"License [{status}]: {message}", "LicenseManager");
        return new LicenseValidationResult(status, Message: message);
    }

    public static string ToBase64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] FromBase64Url(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4) { case 2: s += "=="; break; case 3: s += "="; break; }
        return Convert.FromBase64String(s);
    }
}
