using System.Globalization;

namespace DataManagementSystem.Models;

public class DataCapture
{
    private static readonly TimeZoneInfo Ist =
        TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");

    public int    Id           { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public string CaptureDate  { get; set; } = string.Empty;  // ddMMyyyy
    public string ModelNumber  { get; set; } = string.Empty;
    public bool   IsActive     { get; set; } = true;
    public string CreatedOn    { get; set; } = string.Empty;   // ISO 8601 UTC stored in DB
    public int    CreatedBy    { get; set; }

    public string CreatedByName { get; set; } = string.Empty;  // joined for display

    public string CaptureDateDisplay =>
        string.IsNullOrEmpty(CaptureDate) || CaptureDate.Length != 8
            ? CaptureDate
            : $"{CaptureDate[..2]}/{CaptureDate[2..4]}/{CaptureDate[4..]}";

    // UTC → IST for all UI display and CSV export
    public string CreatedOnIst
    {
        get
        {
            if (string.IsNullOrEmpty(CreatedOn)) return string.Empty;
            if (!DateTime.TryParse(CreatedOn, null, DateTimeStyles.RoundtripKind, out var utc))
                return CreatedOn;
            var ist = TimeZoneInfo.ConvertTimeFromUtc(utc.ToUniversalTime(), Ist);
            return ist.ToString("dd/MM/yyyy HH:mm:ss");
        }
    }
}
