namespace DataManagementSystem.Services;

public interface IAuthService
{
    Task<User?> LoginAsync(string username, string password);
}
