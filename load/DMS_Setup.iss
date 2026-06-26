; ============================================================
;  Data Management System — Production Installer
;  Braentech Software
;
;  Build:   load\build.bat
;           CreateRelease.ps1  (full automated release)
;  Output:  load\Output\DMS_Setup_1.0.0.exe
;
;  No installer passkey. Installs files only.
;  On first launch the app shows a License Activation window —
;  the user pastes the key from LicenseKey.txt supplied with
;  the release package.
; ============================================================

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
;  PASCAL SCRIPT — seeds appsettings.json on first install only
; ══════════════════════════════════════════════════════════════════════════════
[Code]
procedure CurStepChanged(CurStep: TSetupStep);
var
  PD : String;
  AD : String;
begin
  if CurStep <> ssPostInstall then Exit;
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
end;


