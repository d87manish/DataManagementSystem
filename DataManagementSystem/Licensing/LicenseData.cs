namespace DataManagementSystem.Licensing;

public class LicenseData
{
    public string   Version     { get; set; } = "1";
    public string   ClientName  { get; set; } = string.Empty;
    public string   MachineId   { get; set; } = string.Empty;
    public string   ProjectCode { get; set; } = string.Empty;
    public DateTime IssuedOn    { get; set; }
    public DateTime ValidTo     { get; set; }
    public string[] Features    { get; set; } = [];
}

public enum LicenseStatus
{
    Valid,
    NotFound,
    Corrupt,
    InvalidSignature,
    WrongMachine,
    WrongProduct,
    Expired,
}

public record LicenseValidationResult(
    LicenseStatus Status,
    LicenseData?  License = null,
    string?       Message = null)
{
    public bool IsValid => Status == LicenseStatus.Valid;
}
