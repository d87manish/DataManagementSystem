; ============================================================
;  Data Management System — Production Installer
;  Braentech Software
;
;  Build:   load\build.bat
;           CreateRelease.ps1  (full automated release)
;  Output:  load\Output\DMS_Setup_1.0.0.exe
;
;  License key is embedded at compile time by CreateRelease.ps1
;  via /DLICENSE_KEY=V1.xxx... and activated automatically
;  during installation. No manual key entry required.
; ============================================================

; Default empty key — overridden by CreateRelease.ps1 at compile time
#ifndef LICENSE_KEY
  #define LICENSE_KEY ""
#endif

#define AppName        "Data Management System"
#define AppVersion     "1.0.0"
#define AppPublisher   "Braentech Software"
#define AppExeName     "DataManagementSystem.exe"
#define AppId          "{{B4E2A7C1-3D8F-4E56-9A2B-C7D3E8F1A4B5}"
#define SourceDir      "..\publish\app"


[Setup]
; ── Identity ──────────────────────────────────────────────────────────────────
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} Installer

; ── Paths ─────────────────────────────────────────────────────────────────────
DefaultDirName={autopf}\Braentech\DMS
DefaultGroupName=Braentech\Data Management System
DisableDirPage=no
AllowNoIcons=yes

; ── Output ────────────────────────────────────────────────────────────────────
OutputDir=Output
OutputBaseFilename=DMS_Setup_{#AppVersion}
Compression=lzma2/ultra64
SolidCompression=yes

; ── Appearance ────────────────────────────────────────────────────────────────
WizardStyle=modern
WizardSizePercent=110

; ── Platform ──────────────────────────────────────────────────────────────────
; Self-contained win-x64 — no .NET runtime install required.
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763

; ── Privileges ────────────────────────────────────────────────────────────────
PrivilegesRequired=admin

; ── Upgrade ───────────────────────────────────────────────────────────────────
; Detects previous version via AppId, removes it, installs new version.
; %PROGRAMDATA%\Braentech\DMS\ is never touched (DB, license, config preserved).
CloseApplications=yes
CloseApplicationsFilter={#AppExeName}
RestartIfNeededByRun=no

; ── Uninstall ─────────────────────────────────────────────────────────────────
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExeName}
CreateUninstallRegKey=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

; ── Optional desktop shortcut ─────────────────────────────────────────────────
[Tasks]
Name: "desktopicon"; \
  Description: "Create a &desktop shortcut"; \
  GroupDescription: "Additional icons:"; \
  Flags: unchecked

; ── Data directories ──────────────────────────────────────────────────────────
; Users-modify allows the app to write DB, license, logs, backups without UAC.
[Dirs]
Name: "{commonappdata}\Braentech\DMS";          Permissions: users-modify
Name: "{commonappdata}\Braentech\DMS\Backups";  Permissions: users-modify
Name: "{commonappdata}\Braentech\Logs";         Permissions: users-modify

; ── Application files ─────────────────────────────────────────────────────────
; Full self-contained win-x64 publish. .pdb stripped — not for production.
[Files]
Source: "{#SourceDir}\*"; \
  DestDir: "{app}"; \
  Flags: recursesubdirs createallsubdirs ignoreversion; \
  Excludes: "*.pdb"

; ── Shortcuts ─────────────────────────────────────────────────────────────────
[Icons]
Name: "{group}\{#AppName}"; \
  Filename: "{app}\{#AppExeName}"; \
  Comment: "Industrial data capture and management"

Name: "{group}\Uninstall {#AppName}"; \
  Filename: "{uninstallexe}"

Name: "{commondesktop}\{#AppName}"; \
  Filename: "{app}\{#AppExeName}"; \
  Tasks: desktopicon; \
  Comment: "Industrial data capture and management"

; ── Post-install launch offer ─────────────────────────────────────────────────
[Run]
Filename: "{app}\{#AppExeName}"; \
  Description: "Launch {#AppName}"; \
  Flags: nowait postinstall skipifsilent

; ══════════════════════════════════════════════════════════════════════════════
;  PASCAL SCRIPT
;  1. Seeds appsettings.json to ProgramData on first install.
;  2. Auto-activates the license key embedded by CreateRelease.ps1.
; ══════════════════════════════════════════════════════════════════════════════
[Code]
procedure CurStepChanged(CurStep: TSetupStep);
var
  PD         : String;
  AD         : String;
  LicKey     : String;
  ExePath    : String;
  ResultCode : Integer;
begin
  if CurStep <> ssPostInstall then Exit;

  // ── Seed appsettings.json (first install only) ────────────────────────────
  PD := ExpandConstant('{commonappdata}\Braentech\DMS\appsettings.json');
  AD := ExpandConstant('{app}\appsettings.json');
  if not FileExists(PD) then
  begin
    if CopyFile(AD, PD, False) then
      Log('Seeded appsettings.json to ProgramData.')
    else
      Log('WARNING: could not seed appsettings.json.');
  end else
    Log('appsettings.json already in ProgramData; preserved.');

  // ── Auto-activate embedded license key ───────────────────────────────────
  LicKey  := '{#LICENSE_KEY}';
  ExePath := ExpandConstant('{app}\{#AppExeName}');

  if (LicKey = '') or (not FileExists(ExePath)) then
  begin
    Log('No license key embedded — skipping auto-activation.');
    Exit;
  end;

  Log('Activating license...');
  if Exec(ExePath, '--activate "' + LicKey + '"',
          '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    if ResultCode = 0 then
      Log('License activated successfully.')
    else
      Log('WARNING: Activation returned code ' + IntToStr(ResultCode) + '.');
  end else
    Log('WARNING: Could not launch activation process.');
end;


