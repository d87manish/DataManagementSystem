using System.Security.Cryptography;
using System.Text;
using DataManagementSystem.Data;

namespace DataManagementSystem.Services;

public class AuthService : IAuthService
{
    private readonly UserRepository _users;

    public AuthService(UserRepository users) => _users = users;

    public async Task<User?> LoginAsync(string username, string password)
    {
        var user = await _users.FindByUsernameAsync(username);
        if (user == null) return null;

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password))).ToLowerInvariant();
        return hash == user.PasswordHash ? user : null;
    }
}
