namespace DataManagementSystem.Services;

public interface IModbusScanService : IDisposable
{
    bool IsConnected { get; }
    bool Connect();
    void Disconnect();

    event Action<string, string, string>? DataReceived;  // (captureDate ddMMyyyy, serialNumber, modelNumber)

    void StartPolling(int intervalMs = 500);
    void StopPolling();
}
