using System.Windows.Input;
using DataManagementSystem.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DataManagementSystem.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.LogoutRequested += () =>
        {
            App.Services.GetRequiredService<SessionManager>().Logout();
            var login = App.Services.GetRequiredService<LoginWindow>();
            login.Show();
            Close();
        };
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) MaxRestore_Click(sender, e);
        else if (WindowState == WindowState.Normal) DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)  => WindowState = WindowState.Minimized;
    private void MaxRestore_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void Close_Click(object sender, RoutedEventArgs e)      => Application.Current.Shutdown();
}
