namespace DataManagementSystem.Config;

public class AppSettings
{
    public DatabaseSettings    Database       { get; set; } = new();
    public BackupSettings      Backup         { get; set; } = new();
    public ModbusSettings      Modbus         { get; set; } = new();
    public ProjectProfile      ProjectProfile { get; set; } = new();
    public SimulationSettings  Simulation     { get; set; } = new();
}

public class SimulationSettings
{
    /// <summary>
    /// When true, uses SimulatedModbusScanService (random data every 3 s).
    /// Set to false in production appsettings.json.
    /// </summary>
    public bool Enabled          { get; set; } = false;
    public int  IntervalSeconds  { get; set; } = 3;
}

public class DatabaseSettings
{
    public string ConnectionString { get; set; } = string.Empty;
}

public class BackupSettings
{
    public int IntervalHours { get; set; } = 4;
    public int RetainCount   { get; set; } = 7;
}

public class ModbusSettings
{
    public string IpAddress            { get; set; } = "192.168.1.1";
    public int    Port                 { get; set; } = 502;
    public int    SlaveAddress         { get; set; } = 1;
    public ushort TriggerRegister      { get; set; } = 100;
    public ushort SerialNumberRegister { get; set; } = 101;
    public int    SerialNumberRegisters{ get; set; } = 8;
    public ushort ModelNumberRegister  { get; set; } = 109;
    public int    ModelNumberRegisters { get; set; } = 4;
    public int    PollIntervalMs       { get; set; } = 500;
    public int    ConnectTimeoutMs     { get; set; } = 3000;
}

public class ProjectProfile
{
    public string ProjectCode { get; set; } = "DMS";
    public string ProjectName { get; set; } = "Data Management System";
}
