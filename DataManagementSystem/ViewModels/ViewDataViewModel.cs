using DataManagementSystem.Services;
using Microsoft.Win32;

namespace DataManagementSystem.ViewModels;

public class ViewDataViewModel : ViewModelBase
{
    private readonly IDataCaptureService _service;

    private string _fromDate   = DateTime.Today.ToString("yyyy-MM-dd");
    private string _toDate     = DateTime.Today.ToString("yyyy-MM-dd");
    private string _serial     = string.Empty;
    private string _model      = string.Empty;
    private bool   _isBusy;
    private string _statusMessage = string.Empty;

    public string FromDate { get => _fromDate; set => SetProperty(ref _fromDate, value); }
    public string ToDate   { get => _toDate;   set => SetProperty(ref _toDate, value); }
    public string Serial   { get => _serial;   set => SetProperty(ref _serial, value); }
    public string Model    { get => _model;    set => SetProperty(ref _model, value); }
    public bool   IsBusy   { get => _isBusy;   set => SetProperty(ref _isBusy, value); }

    public string StatusMessage
    {
        get => _statusMessage;
        set { SetProperty(ref _statusMessage, value); OnPropertyChanged(nameof(HasStatus)); }
    }
    public bool HasStatus => !string.IsNullOrEmpty(_statusMessage);

    public ObservableCollection<DataCapture> Records { get; } = [];

    public AsyncRelayCommand LoadCommand      { get; }
    public AsyncRelayCommand ExportCsvCommand { get; }

    public ViewDataViewModel(IDataCaptureService service)
    {
        _service          = service;
        LoadCommand       = new AsyncRelayCommand(LoadAsync,      () => !IsBusy);
        ExportCsvCommand  = new AsyncRelayCommand(ExportCsvAsync, () => !IsBusy && Records.Count > 0);
    }

    public async Task LoadTodayAsync()
    {
        FromDate = DateTime.Today.ToString("yyyy-MM-dd");
        ToDate   = DateTime.Today.ToString("yyyy-MM-dd");
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            var list = await _service.GetFilteredAsync(FromDate, ToDate, Serial, Model);
            Records.Clear();
            foreach (var r in list) Records.Add(r);
            StatusMessage = $"{Records.Count} record(s) found.";
        }
        catch (Exception ex)
        {
            Logger.LogError("ViewData load failed", ex, "ViewDataViewModel");
            StatusMessage = "Failed to load data.";
        }
        finally { IsBusy = false; }
    }

    private async Task ExportCsvAsync()
    {
        var dlg = new SaveFileDialog
        {
            Title      = "Export to CSV",
            Filter     = "CSV Files (*.csv)|*.csv",
            FileName   = $"DMS_Export_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
            DefaultExt = ".csv",
        };

        if (dlg.ShowDialog() != true) return;

        IsBusy = true;
        try
        {
            await Task.Run(() => CsvExporter.WriteToFile(dlg.FileName, Records));
            StatusMessage = $"Exported {Records.Count} records to {System.IO.Path.GetFileName(dlg.FileName)}";
            Logger.LogInfo($"CSV exported: {dlg.FileName}", "ViewDataViewModel");
        }
        catch (Exception ex)
        {
            Logger.LogError("CSV export failed", ex, "ViewDataViewModel");
            StatusMessage = "Export failed: " + ex.Message;
        }
        finally { IsBusy = false; }
    }
}
