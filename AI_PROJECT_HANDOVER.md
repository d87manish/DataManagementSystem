# DataManagementSystem — AI Project Handover

> Read this file at the start of every new Claude session for this project.
> It covers what's built, how it works, what rules apply, and what to do next.

---

## Project location

```
D:\Braentech\DMS\DataManagementSystem\
├── DataManagementSystem\              ← main WPF app
│   ├── App.xaml / App.xaml.cs
│   ├── appsettings.json               ← dev config (Simulation.Enabled = true)
│   ├── application-mode.json          ← Mode=1 = bypass license check in dev
│   ├── Config\
│   │   ├── AppSettings.cs             ← typed settings (SimulationSettings, ModbusSettings, …)
│   │   ├── ConfigBootstrapper.cs      ← seeds %PROGRAMDATA% config; re-seeds if app file is newer
│   │   └── ApplicationModeService.cs  ← reads application-mode.json
│   ├── Data\
│   │   ├── SchemaScripts.cs           ← versioned SQL (V1 creates Users + DataCapture tables + index)
│   │   ├── DatabaseSetupService.cs    ← EnsureCreated(), seeds admin user
│   │   ├── UserRepository.cs
│   │   └── DataCaptureRepository.cs   ← GetMaxSerialCounterAsync, IsDuplicateAsync, Insert, GetRecent, GetFiltered
│   ├── Helpers\
│   │   ├── Logger.cs                  ← file logger → %PROGRAMDATA%\Braentech\Logs\
│   │   ├── CsvExporter.cs             ← uses CreatedOnIst (IST display value)
│   │   └── BoolToVisibilityConverter.cs
│   ├── Licensing\
│   │   ├── LicenseManager.cs          ← RSA-2048 validate + activate; public key placeholder inside
│   │   ├── LicenseData.cs
│   │   └── MachineIdService.cs        ← SHA-256(CPU|Disk|MAC) → XXXX-XXXX-XXXX-XXXX
│   ├── Models\
│   │   ├── User.cs
│   │   └── DataCapture.cs             ← has CreatedOnIst computed property (UTC→IST)
│   ├── Services\
│   │   ├── IModbusScanService.cs      ← Connect(), Disconnect(), StartPolling(), StopPolling(), DataReceived event
│   │   ├── ModbusScanService.cs       ← FluentModbus TCP; reads holding registers; decodes hi/lo byte as ASCII
│   │   ├── SimulatedModbusScanService.cs ← dev only; seeds counter from DB on StartPolling
│   │   ├── AuthService.cs
│   │   ├── DataCaptureService.cs
│   │   └── SessionManager.cs
│   ├── ViewModels\
│   │   ├── ViewModelBase.cs / RelayCommand.cs / AsyncRelayCommand.cs
│   │   ├── LoginViewModel.cs
│   │   ├── MainViewModel.cs           ← nav commands: stop capture + auto-load on nav to View Data
│   │   ├── DataCaptureViewModel.cs    ← StopIfCapturing(); grid capped at 20 records
│   │   └── ViewDataViewModel.cs       ← LoadTodayAsync(); IST date filter → UTC conversion in repo
│   └── Views\
│       ├── MainWindow.xaml            ← 200px sidebar + ContentControl; DataTemplates per VM type
│       ├── LoginWindow.xaml
│       ├── DataCapturePage.xaml       ← Start/Stop button; 3 stat cards; DataGrid (20 rows max)
│       ├── ViewDataPage.xaml          ← 2-row filter (dates+buttons / serial+model); DataGrid
│       └── LicenseWindow.xaml
└── DataManagementSystem.LicenseGenerator\
    └── Program.cs                     ← --genkeys / --create / --info
```

---

## Running in dev (simulation mode)

1. `appsettings.json` has `"Simulation": { "Enabled": true, "IntervalSeconds": 3 }`
2. `application-mode.json` has `{ "Mode": 1 }` — bypasses license check
3. Build: `dotnet build --configuration Debug`
4. Run: `dotnet run` or launch the `.exe` from `bin\Debug\net10.0-windows\`
5. Login: **admin** / **admin**
6. Click **Start Capture** → random records appear every 3 seconds

---

## Running in production

1. Set `Simulation.Enabled = false` in `%PROGRAMDATA%\Braentech\DMS\appsettings.json`
2. Set PLC IP in `Modbus.IpAddress` and `Modbus.Port` (default `192.168.1.1:502`)
3. Generate license: run `DMS.LicenseGenerator.exe --genkeys`, then `--create --machine <id> --client "Name" --project DMS --days 365`
4. Copy public key into `LicenseManager.cs` `PublicKeyPem` field and rebuild before making installer
5. On client machine: open app → activation window → paste license string

---

## Key rules (enforced by user)

| Rule | Detail |
|------|--------|
| UTC in DB | `CreatedOn` stored as `DateTime.UtcNow.ToString("o")` |
| IST in UI | All display uses `CreatedOnIst` (computed on model, UTC→IST via `TimeZoneInfo "India Standard Time"`) |
| No TZ label on columns | Just "Created On" / "Saved At" — no "(IST)" suffix |
| Date filter = IST | `GetFilteredAsync` converts IST date input to UTC bounds before querying |
| Simulation flag | In `appsettings.json`, not `application-mode.json` |
| Capture grid limit | Max 20 records; oldest drops on live insert |
| Stop on nav away | `MainViewModel` calls `StopIfCapturing()` before any navigation away from Data Capture |
| View Data auto-load | `NavViewDataCommand` always fires `LoadTodayAsync()` |
| Counter seeding | `SimulatedModbusScanService.StartPolling()` queries DB for max existing counter before starting timer |

---

## Database schema (V1)

```sql
CREATE TABLE Users (
    Id           INTEGER PRIMARY KEY AUTOINCREMENT,
    Username     TEXT    NOT NULL UNIQUE,
    PasswordHash TEXT    NOT NULL,
    IsActive     INTEGER NOT NULL DEFAULT 1,
    CreatedOn    TEXT    NOT NULL
);

CREATE TABLE DataCapture (
    Id           INTEGER PRIMARY KEY AUTOINCREMENT,
    SerialNumber TEXT    NOT NULL,
    CaptureDate  TEXT    NOT NULL,   -- ddMMyyyy
    ModelNumber  TEXT    NOT NULL,
    IsActive     INTEGER NOT NULL DEFAULT 1,
    CreatedOn    TEXT    NOT NULL,   -- ISO 8601 UTC
    CreatedBy    INTEGER NOT NULL REFERENCES Users(Id)
);

-- same serial allowed in different month; duplicate within same month blocked
CREATE UNIQUE INDEX IX_DataCapture_Serial_MonthYear
    ON DataCapture (SerialNumber, substr(CaptureDate, 3, 6))
    WHERE IsActive = 1;
```

---

## appsettings.json (dev reference)

```json
{
  "Simulation":  { "Enabled": true, "IntervalSeconds": 3 },
  "Database":    { "ConnectionString": "Data Source=%PROGRAMDATA%\\Braentech\\DMS\\dms.db" },
  "Backup":      { "IntervalHours": 4, "RetainCount": 7 },
  "Modbus": {
    "IpAddress": "192.168.1.1", "Port": 502,
    "SlaveAddress": 1,
    "TriggerRegister": 100,
    "SerialNumberRegister": 101, "SerialNumberRegisters": 8,
    "ModelNumberRegister":  109, "ModelNumberRegisters":  4,
    "PollIntervalMs": 500, "ConnectTimeoutMs": 3000
  },
  "ProjectProfile": { "ProjectCode": "DMS", "ProjectName": "Data Management System" }
}
```

---

## Modbus TCP wiring (production)

- Library: `FluentModbus 5.0.3` — fully .NET-native, no serial port dependency
- `ModbusScanService.Connect()` opens TCP to `IpAddress:Port` (sync, timeout via `ConnectTimeoutMs`)
- Polls `TriggerRegister` every `PollIntervalMs` ms; fires only when register value **changes** and is non-zero
- Reads `SerialNumberRegisters` holding registers from `SerialNumberRegister`; decodes each register as 2 ASCII chars (high byte first)
- Same for model number registers

---

## NuGet packages

| Package | Version | Purpose |
|---------|---------|---------|
| Microsoft.Data.Sqlite | 10.0.* | SQLite ADO.NET |
| Microsoft.Extensions.Hosting | 10.0.* | DI + IHost |
| FluentModbus | 5.0.3 | Modbus TCP client |
| System.Management | 10.0.* | Machine ID (CPU/Disk WMI) |

---

## Known gotchas

- **`Run.Text` binding** in WPF defaults to TwoWay — must add `Mode=OneWay` on read-only properties or it crashes at runtime
- **`LetterSpacing`** does not exist on WPF `TextBlock` — remove it
- **FluentModbus v5** `ReadHoldingRegisters<T>` returns `Span<T>` directly — do not call `.Span` on the result
- **FluentModbus v5** `Connect()` is synchronous — there is no `ConnectAsync` in this version
- **ProgramData config** only gets re-seeded when app-side file is newer — if you add new config keys, touch/rebuild `appsettings.json` so the timestamp triggers re-seed on next run
- **`System.IO.Ports`** was removed from the project — Modbus is TCP only; do not add it back unless serial is needed

---

## Pending / future work

- [ ] Installer / NSIS or WiX setup — should bundle all DLLs, preserve DB on upgrade, prompt for license key
- [ ] `LicenseManager.cs` — `PublicKeyPem` placeholder must be replaced with real key before any prod build
- [ ] Run `DMS.LicenseGenerator.exe --genkeys` once and paste public key into `LicenseManager.cs`
- [ ] Test real Modbus TCP PLC connection with actual register map
- [ ] Decide on installer update flow — currently DB backup happens on startup + exit; reinstall should not wipe `%PROGRAMDATA%`
