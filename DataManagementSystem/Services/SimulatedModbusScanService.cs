using DataManagementSystem.Config;
using DataManagementSystem.Data;

namespace DataManagementSystem.Services;

// Dev/simulation mode only. Enabled via appsettings.json Simulation.Enabled = true.
public class SimulatedModbusScanService : IModbusScanService
{
    private static readonly string[] Models = ["MDL-01", "MDL-02", "MDL-03", "MDL-A1", "MDL-B2"];
    private static readonly Random   Rng    = new();

    private readonly SimulationSettings      _settings;
    private readonly DataCaptureRepository   _repo;
    private System.Timers.Timer?             _timer;
    private int                              _counter;

    public bool IsConnected { get; private set; }
    public event Action<string, string>? DataReceived;

    public SimulatedModbusScanService(SimulationSettings settings, DataCaptureRepository repo)
    {
        _settings = settings;
        _repo     = repo;
    }

    public bool Connect()
    {
        IsConnected = true;
        Logger.LogInfo("SimulatedModbus: connected (simulation mode).", "SimulatedModbusScanService");
        return true;
    }

    public void Disconnect()
    {
        StopPolling();
        IsConnected = false;
        Logger.LogInfo("SimulatedModbus: disconnected.", "SimulatedModbusScanService");
    }

    public void StartPolling(int intervalMs = 500)
    {
        // Seed counter from DB so we never collide with previously saved serials
        var today       = DateTime.Now;
        var prefix      = $"SN{today:ddMMyy}";
        var captureDate = today.ToString("ddMMyyyy");
        _counter = _repo.GetMaxSerialCounterAsync(prefix, captureDate).GetAwaiter().GetResult();
        Logger.LogInfo($"SimulatedModbus: counter seeded to {_counter} for prefix {prefix}", "SimulatedModbusScanService");

        var fireEveryMs = _settings.IntervalSeconds * 1000;
        _timer = new System.Timers.Timer(fireEveryMs) { AutoReset = true };
        _timer.Elapsed += (_, _) =>
        {
            _counter++;
            var serial = $"SN{DateTime.Now:ddMMyy}{_counter:D4}";
            var model  = Models[Rng.Next(Models.Length)];
            Logger.LogInfo($"SimulatedModbus: serial={serial} model={model}", "SimulatedModbusScanService");
            DataReceived?.Invoke(serial, model);
        };
        _timer.Start();
    }

    public void StopPolling()
    {
        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;
    }

    public void Dispose() => Disconnect();
}
