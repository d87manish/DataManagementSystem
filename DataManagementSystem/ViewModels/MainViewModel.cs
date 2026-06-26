namespace DataManagementSystem.ViewModels;

public class MainViewModel : ViewModelBase
{
    private object _currentPage = null!;

    public DataCaptureViewModel DataCaptureVM { get; }
    public ViewDataViewModel    ViewDataVM    { get; }

    public object CurrentPage
    {
        get => _currentPage;
        set => SetProperty(ref _currentPage, value);
    }

    public string UserDisplay { get; }

    public RelayCommand NavDataCaptureCommand { get; }
    public RelayCommand NavViewDataCommand    { get; }

    public event Action? LogoutRequested;

    public RelayCommand LogoutCommand { get; }

    public MainViewModel(DataCaptureViewModel dataCaptureVm, ViewDataViewModel viewDataVm, SessionManager session)
    {
        DataCaptureVM = dataCaptureVm;
        ViewDataVM    = viewDataVm;
        UserDisplay   = session.CurrentUser?.Username ?? "admin";

        NavDataCaptureCommand = new RelayCommand(() =>
        {
            CurrentPage = DataCaptureVM;
        });

        NavViewDataCommand = new RelayCommand(() =>
        {
            DataCaptureVM.StopIfCapturing();
            CurrentPage = ViewDataVM;
            _ = ViewDataVM.LoadTodayAsync();
        });

        LogoutCommand = new RelayCommand(() =>
        {
            DataCaptureVM.StopIfCapturing();
            LogoutRequested?.Invoke();
        });

        CurrentPage = DataCaptureVM;
    }
}
