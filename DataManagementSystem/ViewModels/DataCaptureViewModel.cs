using DataManagementSystem.Config;
using DataManagementSystem.Services;

namespace DataManagementSystem.ViewModels;

public class DataCaptureViewModel : ViewModelBase
{
    private readonly IDataCaptureService _captureService;
    private readonly IModbusScanService  _modbus;
    private readonly ModbusSettings      _modbusSettings;
    private readonly SessionManager      _session;

    private bool   _isCapturing;
    private string _statusMessage = "Ready. Press Start Capture to begin.";
    private bool   _hasStatusError;

    public bool IsCapturing
    {
        get => _isCapturing;
        set { SetProperty(ref _isCapturing, value); OnPropertyChanged(nameof(IsNotCapturing)); }
    }
    public bool IsNotCapturing => !_isCapturing;

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool HasStatusError
    {
        get => _hasStatusError;
        set => SetProperty(ref _hasStatusError, value);
    }

    public ObservableCollection<DataCapture> RecentCaptures { get; } = [];
    public ObservableCollection<DataCapture> AllCaptures    { get; } = [];

    public AsyncRelayCommand StartCaptureCommand { get; }
    public AsyncRelayCommand StopCaptureCommand  { get; }

    public DataCaptureViewModel(IDataCaptureService captureService, IModbusScanService modbus,
        ModbusSettings modbusSettings, SessionManager session)
    {
        _captureService  = captureService;
        _modbus          = modbus;
        _modbusSettings  = modbusSettings;
        _session         = session;

        _modbus.DataReceived += OnDataReceived;

        StartCaptureCommand = new AsyncRelayCommand(StartCaptureAsync, () => !IsCapturing);
        StopCaptureCommand  = new AsyncRelayCommand(StopCaptureAsync,  () => IsCapturing);
    }

    private async Task StartCaptureAsync()
    {
        SetStatus($"Connecting to PLC at {_modbusSettings.IpAddress}:{_modbusSettings.Port}...", false);

        var connected = await Task.Run(() => _modbus.Connect());
        if (!connected)
        {
            SetStatus($"Failed to connect to {_modbusSettings.IpAddress}:{_modbusSettings.Port}. Check IP/network settings.", true);
            return;
        }

        IsCapturing = true;
        _modbus.StartPolling(_modbusSettings.PollIntervalMs);
        SetStatus($"Capturing — connected to {_modbusSettings.IpAddress}:{_modbusSettings.Port}...", false);

        // Load existing records for the grid
        await RefreshGridAsync();
    }

    private async Task StopCaptureAsync()
    {
        _modbus.StopPolling();
        _modbus.Disconnect();
        IsCapturing = false;
        SetStatus("Capture stopped.", false);
        await Task.CompletedTask;
    }

    private async void OnDataReceived(string serialNumber, string modelNumber)
    {
        var captureDate = DateTime.Now.ToString("ddMMyyyy");

        var capture = new DataCapture
        {
            SerialNumber  = serialNumber,
            CaptureDate   = captureDate,
            ModelNumber   = modelNumber,
            CreatedOn     = DateTime.UtcNow.ToString("o"),
            CreatedBy     = _session.CurrentUser?.Id ?? 1,
            CreatedByName = _session.CurrentUser?.Username ?? "admin",
        };

        var (success, error) = await _captureService.SaveAsync(capture);

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            if (success)
            {
                SetStatus($"Saved: {serialNumber} | {modelNumber}", false);

                // Update recent (last 3) list
                RecentCaptures.Insert(0, capture);
                while (RecentCaptures.Count > 3) RecentCaptures.RemoveAt(3);

                // Prepend to full grid, keep capped at GridLimit
                AllCaptures.Insert(0, capture);
                while (AllCaptures.Count > GridLimit) AllCaptures.RemoveAt(AllCaptures.Count - 1);
            }
            else
            {
                SetStatus($"Skipped [{serialNumber}]: {error}", true);
            }
        });
    }

    private const int GridLimit = 20;

    private async Task RefreshGridAsync()
    {
        var records = await _captureService.GetRecentAsync(GridLimit);
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            AllCaptures.Clear();
            foreach (var r in records) AllCaptures.Add(r);
        });

        var recent = records.Take(3).ToList();
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            RecentCaptures.Clear();
            foreach (var r in recent) RecentCaptures.Add(r);
        });
    }

    public void StopIfCapturing()
    {
        if (!IsCapturing) return;
        _modbus.StopPolling();
        _modbus.Disconnect();
        IsCapturing = false;
        SetStatus("Capture stopped.", false);
    }

    private void SetStatus(string message, bool isError)
    {
        StatusMessage  = message;
        HasStatusError = isError;
    }
}
