using System.Globalization;
using System.IO;
using System.Text;

namespace DataManagementSystem.Helpers;

public static class CsvExporter
{
    public static string Build(IEnumerable<DataCapture> records)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Id,SerialNumber,CaptureDate,ModelNumber,CreatedOn,CreatedBy");

        foreach (var r in records)
        {
            sb.AppendLine(string.Join(",",
                r.Id.ToString(CultureInfo.InvariantCulture),
                Escape(r.SerialNumber),
                Escape(r.CaptureDateDisplay),
                Escape(r.ModelNumber),
                Escape(r.CreatedOnIst),
                Escape(r.CreatedByName)));
        }

        return sb.ToString();
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    public static void WriteToFile(string path, IEnumerable<DataCapture> records)
        => File.WriteAllText(path, Build(records), Encoding.UTF8);
}
