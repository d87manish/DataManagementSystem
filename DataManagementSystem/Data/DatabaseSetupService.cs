using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace DataManagementSystem.Data;

public class DatabaseSetupService
{
    private readonly string _connectionString;

    public DatabaseSetupService(string connectionString)
    {
        _connectionString = Resolve(connectionString);
    }

    public static string Resolve(string cs)
    {
        var resolved = Environment.ExpandEnvironmentVariables(cs);
        var builder  = new SqliteConnectionStringBuilder(resolved);
        builder.DataSource = Path.GetFullPath(builder.DataSource);
        return builder.ToString();
    }

    public bool EnsureCreated(out string error)
    {
        error = string.Empty;
        try
        {
            var dbPath = new SqliteConnectionStringBuilder(_connectionString).DataSource;
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            int currentVersion = GetSchemaVersion(conn);

            foreach (var (version, sql) in SchemaScripts.Scripts)
            {
                if (version <= currentVersion) continue;

                ExecuteScript(conn, sql);
                SetSchemaVersion(conn, version);
                Logger.LogInfo($"Schema V{version} applied.", "DatabaseSetupService");
            }

            SeedDefaultUser(conn);
            Logger.LogInfo($"Database ready. Schema V{SchemaScripts.CurrentVersion}.", "DatabaseSetupService");
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            Logger.LogError("Database setup failed", ex, "DatabaseSetupService");
            return false;
        }
    }

    private static int GetSchemaVersion(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='SchemaVersion'";
        var exists = cmd.ExecuteScalar();
        if (exists == null) return 0;

        cmd.CommandText = "SELECT MAX(Version) FROM SchemaVersion";
        var result = cmd.ExecuteScalar();
        return result == DBNull.Value || result == null ? 0 : Convert.ToInt32(result);
    }

    private static void SetSchemaVersion(SqliteConnection conn, int version)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT OR REPLACE INTO SchemaVersion (Version, AppliedOn) VALUES ($v, $d)";
        cmd.Parameters.AddWithValue("$v", version);
        cmd.Parameters.AddWithValue("$d", DateTime.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    private static void ExecuteScript(SqliteConnection conn, string sql)
    {
        foreach (var statement in sql.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (string.IsNullOrWhiteSpace(statement)) continue;
            using var cmd = conn.CreateCommand();
            cmd.CommandText = statement;
            cmd.ExecuteNonQuery();
        }
    }

    private static void SeedDefaultUser(SqliteConnection conn)
    {
        using var check = conn.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM Users WHERE Username = 'admin'";
        var count = Convert.ToInt32(check.ExecuteScalar());
        if (count > 0) return;

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("admin")));
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Users (Username, PasswordHash, IsActive, CreatedOn)
            VALUES ('admin', $h, 1, $d)
            """;
        cmd.Parameters.AddWithValue("$h", hash.ToLowerInvariant());
        cmd.Parameters.AddWithValue("$d", DateTime.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();
        Logger.LogInfo("Default admin user seeded.", "DatabaseSetupService");
    }
}
