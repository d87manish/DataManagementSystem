using Microsoft.Data.Sqlite;

namespace DataManagementSystem.Data;

public class UserRepository
{
    private readonly string _connectionString;

    public UserRepository(string connectionString) => _connectionString = connectionString;

    public async Task<User?> FindByUsernameAsync(string username)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Username, PasswordHash, IsActive, CreatedOn FROM Users WHERE Username = $u AND IsActive = 1";
        cmd.Parameters.AddWithValue("$u", username);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;
        return Map(reader);
    }

    private static User Map(SqliteDataReader r) => new()
    {
        Id           = r.GetInt32(0),
        Username     = r.GetString(1),
        PasswordHash = r.GetString(2),
        IsActive     = r.GetInt32(3) == 1,
        CreatedOn    = r.GetString(4),
    };
}
