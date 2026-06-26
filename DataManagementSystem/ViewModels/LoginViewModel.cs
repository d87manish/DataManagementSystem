using DataManagementSystem.Services;

namespace DataManagementSystem.ViewModels;

public class LoginViewModel : ViewModelBase
{
    private readonly IAuthService    _auth;
    private readonly SessionManager  _session;

    private string _username  = string.Empty;
    private string _password  = string.Empty;
    private string _message   = string.Empty;
    private bool   _isBusy;

    public event Action? LoginSuccess;

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public string Message
    {
        get => _message;
        set { SetProperty(ref _message, value); OnPropertyChanged(nameof(HasMessage)); }
    }

    public bool HasMessage => !string.IsNullOrEmpty(_message);

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public string CopyrightYear => $"© {DateTime.Now.Year} Braentech Software";

    public AsyncRelayCommand LoginCommand { get; }

    public LoginViewModel(IAuthService auth, SessionManager session)
    {
        _auth    = auth;
        _session = session;
        LoginCommand = new AsyncRelayCommand(LoginAsync, () => !IsBusy);
    }

    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Username)) { Message = "Username is required."; return; }
        if (string.IsNullOrWhiteSpace(Password))  { Message = "Password is required.";  return; }

        IsBusy  = true;
        Message = string.Empty;
        try
        {
            var user = await _auth.LoginAsync(Username, Password);
            if (user == null) { Message = "Invalid username or password."; return; }

            _session.Login(user);
            Logger.LogInfo($"User '{user.Username}' logged in.", "LoginViewModel");
            LoginSuccess?.Invoke();
        }
        catch (Exception ex)
        {
            Logger.LogError("Login failed", ex, "LoginViewModel");
            Message = "Login failed. Please try again.";
        }
        finally { IsBusy = false; }
    }
}
