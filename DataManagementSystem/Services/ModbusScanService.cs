using System.Net;
using System.Text;
using DataManagementSystem.Config;
using FluentModbus;

namespace DataManagementSystem.Services;

public class ModbusScanService : IModbusScanService
{
    private readonly ModbusSettings      _settings;
    private ModbusTcpClient?             _client;
    private System.Timers.Timer?         _timer;
    private ushort                       _lastTriggerValue;

    public event Action<string, string>? DataReceived;
    public bool IsConnected { get; private set; }

    public ModbusScanService(ModbusSettings settings) => _settings = settings;

    public bool Connect()
    {
        try
        {
            var client   = new ModbusTcpClient();
            var endpoint = new IPEndPoint(IPAddress.Parse(_settings.IpAddress), _settings.Port);
            client.Connect(endpoint, ModbusEndianness.BigEndian);

            _client     = client;
            IsConnected = true;
            Logger.LogInfo($"Modbus TCP connected to {_settings.IpAddress}:{_settings.Port}", "ModbusScanService");
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Modbus TCP connect failed ({_settings.IpAddress}:{_settings.Port})", ex, "ModbusScanService");
            _client     = null;
            IsConnected = false;
            return false;
        }
    }

    public void Disconnect()
    {
        StopPolling();
        try { _client?.Dispose(); } catch { }
        _client     = null;
        IsConnected = false;
        Logger.LogInfo("Modbus TCP disconnected.", "ModbusScanService");
    }

    public void StartPolling(int intervalMs = 500)
    {
        _lastTriggerValue = 0;
        _timer = new System.Timers.Timer(intervalMs) { AutoReset = true };
        _timer.Elapsed += OnTimerElapsed;
        _timer.Start();
    }

    public void StopPolling()
    {
        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;
    }

    private void OnTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        try
        {
            if (_client == null) return;

            var triggerSpan = _client.ReadHoldingRegisters<ushort>(
                (byte)_settings.SlaveAddress, _settings.TriggerRegister, 1);
            var trigger = triggerSpan[0];

            if (trigger == _lastTriggerValue || trigger == 0) return;
            _lastTriggerValue = trigger;

            var serial = ReadString(_settings.SerialNumberRegister, _settings.SerialNumberRegisters);
            var model  = ReadString(_settings.ModelNumberRegister,  _settings.ModelNumberRegisters);

            if (string.IsNullOrWhiteSpace(serial)) return;

            Logger.LogInfo($"Modbus data: serial={serial} model={model}", "ModbusScanService");
            DataReceived?.Invoke(serial.Trim(), model.Trim());
        }
        catch (Exception ex) { Logger.LogError("Modbus poll error", ex, "ModbusScanService"); }
    }

    // Each register holds 2 ASCII chars (high byte = first char, low byte = second char)
    private string ReadString(ushort startAddress, int registerCount)
    {
        var regs = _client!.ReadHoldingRegisters<ushort>(
            (byte)_settings.SlaveAddress, startAddress, (ushort)registerCount);

        var sb = new StringBuilder(registerCount * 2);
        foreach (var reg in regs)
        {
            var hi = (byte)(reg >> 8);
            var lo = (byte)(reg & 0xFF);
            if (hi > 0) sb.Append((char)hi);
            if (lo > 0) sb.Append((char)lo);
        }
        return sb.ToString().TrimEnd('\0', ' ');
    }

    public void Dispose() => Disconnect();
}
