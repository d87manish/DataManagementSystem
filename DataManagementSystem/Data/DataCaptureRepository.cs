using Microsoft.Data.Sqlite;

namespace DataManagementSystem.Data;

public class DataCaptureRepository
{
    private static readonly TimeZoneInfo Ist =
        TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");

    private readonly string _connectionString;

    public DataCaptureRepository(string connectionString) => _connectionString = connectionString;

    /// <summary>
    /// Returns the highest numeric suffix already used for serials matching the given prefix today.
    /// Used by SimulatedModbusScanService to continue the counter across sessions.
    /// Query filters by CaptureDate (ddMMyyyy) which is fast and avoids a full table scan.
    /// </summary>
    public async Task<int> GetMaxSerialCounterAsync(string serialPrefix, string captureDate)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT COALESCE(MAX(CAST(substr(SerialNumber, $offset) AS INTEGER)), 0)
            FROM DataCapture
            WHERE CaptureDate = $cd
              AND SerialNumber LIKE $prefix || '%'
              AND IsActive = 1
            """;
        cmd.Parameters.AddWithValue("$offset", serialPrefix.Length + 1);
        cmd.Parameters.AddWithValue("$cd",     captureDate);
        cmd.Parameters.AddWithValue("$prefix", serialPrefix);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<bool> IsDuplicateAsync(string serialNumber, string captureDate)
    {
        // same serial + same month+year (chars 3-8 of ddMMyyyy) = duplicate
        var monthYear = captureDate.Length == 8 ? captureDate.Substring(2, 6) : captureDate;
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*) FROM DataCapture
            WHERE SerialNumber = $s
              AND substr(CaptureDate, 3, 6) = $my
              AND IsActive = 1
            """;
        cmd.Parameters.AddWithValue("$s",  serialNumber);
        cmd.Parameters.AddWithValue("$my", monthYear);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }

    public async Task<int> InsertAsync(DataCapture capture)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO DataCapture (SerialNumber, CaptureDate, ModelNumber, IsActive, CreatedOn, CreatedBy)
            VALUES ($sn, $cd, $mn, 1, $co, $cb);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("$sn", capture.SerialNumber);
        cmd.Parameters.AddWithValue("$cd", capture.CaptureDate);
        cmd.Parameters.AddWithValue("$mn", capture.ModelNumber);
        cmd.Parameters.AddWithValue("$co", capture.CreatedOn);
        cmd.Parameters.AddWithValue("$cb", capture.CreatedBy);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<List<DataCapture>> GetRecentAsync(int count)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT dc.Id, dc.SerialNumber, dc.CaptureDate, dc.ModelNumber, dc.IsActive, dc.CreatedOn, dc.CreatedBy, u.Username
            FROM DataCapture dc LEFT JOIN Users u ON dc.CreatedBy = u.Id
            WHERE dc.IsActive = 1
            ORDER BY dc.Id DESC
            LIMIT $c
            """;
        cmd.Parameters.AddWithValue("$c", count);
        return await ReadListAsync(cmd);
    }

    public async Task<List<DataCapture>> GetFilteredAsync(
        string? fromDate, string? toDate, string? serialNumber, string? modelNumber)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();

        var where = new List<string> { "dc.IsActive = 1" };

        // fromDate / toDate arrive as IST date strings (yyyy-MM-dd); convert to UTC for DB comparison
        if (!string.IsNullOrWhiteSpace(fromDate))
        {
            var istStart = DateTime.Parse(fromDate + "T00:00:00");
            var utcStart = TimeZoneInfo.ConvertTimeToUtc(istStart, Ist);
            where.Add("dc.CreatedOn >= $from");
            cmd.Parameters.AddWithValue("$from", utcStart.ToString("o"));
        }
        if (!string.IsNullOrWhiteSpace(toDate))
        {
            var istEnd = DateTime.Parse(toDate + "T23:59:59");
            var utcEnd = TimeZoneInfo.ConvertTimeToUtc(istEnd, Ist);
            where.Add("dc.CreatedOn <= $to");
            cmd.Parameters.AddWithValue("$to", utcEnd.ToString("o"));
        }
        if (!string.IsNullOrWhiteSpace(serialNumber))
        {
            where.Add("dc.SerialNumber LIKE $sn");
            cmd.Parameters.AddWithValue("$sn", $"%{serialNumber}%");
        }
        if (!string.IsNullOrWhiteSpace(modelNumber))
        {
            where.Add("dc.ModelNumber LIKE $mn");
            cmd.Parameters.AddWithValue("$mn", $"%{modelNumber}%");
        }

        cmd.CommandText = $"""
            SELECT dc.Id, dc.SerialNumber, dc.CaptureDate, dc.ModelNumber, dc.IsActive, dc.CreatedOn, dc.CreatedBy, u.Username
            FROM DataCapture dc LEFT JOIN Users u ON dc.CreatedBy = u.Id
            WHERE {string.Join(" AND ", where)}
            ORDER BY dc.Id DESC
            """;

        return await ReadListAsync(cmd);
    }

    private static async Task<List<DataCapture>> ReadListAsync(SqliteCommand cmd)
    {
        var list = new List<DataCapture>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new DataCapture
            {
                Id            = r.GetInt32(0),
                SerialNumber  = r.GetString(1),
                CaptureDate   = r.GetString(2),
                ModelNumber   = r.GetString(3),
                IsActive      = r.GetInt32(4) == 1,
                CreatedOn     = r.GetString(5),
                CreatedBy     = r.GetInt32(6),
                CreatedByName = r.IsDBNull(7) ? string.Empty : r.GetString(7),
            });
        }
        return list;
    }
}
