using System.Windows.Media;
using DataManagementSystem.Licensing;

namespace DataManagementSystem.Views;

public partial class LicenseWindow : Window
{
    private readonly LicenseManager _manager;

    public LicenseWindow(LicenseManager manager, LicenseValidationResult initialResult)
    {
        InitializeComponent();
        _manager = manager;
        MachineIdText.Text = manager.MachineId;
        ShowStatus(initialResult.Message ?? "Please enter your license key.", false);
    }

    private void Activate_Click(object sender, RoutedEventArgs e)
    {
        var key = LicenseKeyBox.Text.Trim();
        if (string.IsNullOrEmpty(key)) { ShowStatus("Please paste your license key.", false); return; }

        var result = _manager.Activate(key);
        if (result.IsValid)
        {
            ShowStatus($"Activated successfully! Client: {result.License!.ClientName}", true);
            DialogResult = true;
        }
        else
        {
            ShowStatus(result.Message ?? "Activation failed.", false);
        }
    }

    private void CopyMachineId_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(_manager.MachineId);
        ShowStatus("Machine ID copied to clipboard.", true);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
    private void Close_Click(object sender, RoutedEventArgs e)  => DialogResult = false;

    private void ShowStatus(string message, bool isSuccess)
    {
        StatusText.Text       = message;
        StatusText.Foreground = isSuccess
            ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"))
            : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
    }
}
