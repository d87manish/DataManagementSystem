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
    private string                        _lastSerial = string.Empty;

    public event Action<string, string, string>? DataReceived;
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
        _lastSerial = string.Empty;
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

            // D10-D18: packed ASCII block — ddmmyy (6) + serial (5) + model (6) = 17 chars in 9 registers
            int totalChars     = _settings.DateLength + _settings.SerialLength + _settings.ModelLength;
            int registerCount  = (totalChars + 1) / 2;
            var block          = ReadAsciiBlock(_settings.DataBlockRegister, registerCount);

            if (block.Length < totalChars) return;

            var dateRaw = block.Substring(0, _settings.DateLength);
            var serial  = block.Substring(_settings.DateLength, _settings.SerialLength).Trim();
            var model   = block.Substring(_settings.DateLength + _settings.SerialLength, _settings.ModelLength).Trim();

            if (string.IsNullOrWhiteSpace(serial) || serial == _lastSerial) return;
            _lastSerial = serial;

            // ddmmyy → ddMMyyyy
            var captureDate = dateRaw.Length == 6
                ? dateRaw[..4] + "20" + dateRaw[4..]
                : DateTime.Now.ToString("ddMMyyyy");

            Logger.LogInfo($"Modbus data: date={captureDate} serial={serial} model={model}", "ModbusScanService");
            DataReceived?.Invoke(captureDate, serial, model);
        }
        catch (Exception ex) { Logger.LogError("Modbus poll error", ex, "ModbusScanService"); }
    }

    // Each Modbus register holds 2 ASCII chars: high byte = first char, low byte = second char
    private string ReadAsciiBlock(ushort startAddress, int registerCount)
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
