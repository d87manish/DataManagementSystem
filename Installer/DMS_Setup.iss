; ============================================================
;  Data Management System — Production Installer
;  Braentech Software
;
;  Build with:  Installer\build.bat
;  Output:      Installer\Output\DMS_Setup_1.0.0.exe
;
;  Installation passkey gate
;  ─────────────────────────
;  The installer shows a passkey page BEFORE any files or
;  folders are created. Only the SHA-256 of the passkey is
;  stored here — the plain passkey is never in this file.
;
;  To change the passkey for a new release:
;    1. Generate a new 16-char alphanumeric passkey.
;    2. Run this PowerShell command to get its SHA-256:
;         $k = 'YourNewPasskey'
;         [System.BitConverter]::ToString(
;           [System.Security.Cryptography.SHA256]::Create().ComputeHash(
;             [System.Text.Encoding]::UTF8.GetBytes($k)
;           )).Replace('-','').ToLower()
;    3. Replace the PasskeyHash value below with the new hash.
;    4. Rebuild with build.bat.
;
;  Activation flow (after passkey gate):
;    1. Files are installed to {app}.
;    2. {app}\DataManagementSystem.exe --activate <key> is run.
;       Exit 0 -> license.lic written -> install completes.
;       Exit != 0 -> error shown -> install rolled back.
;    3. If license key skipped: app enforces activation on every
;       launch — cannot be used without a valid license.lic.
; ============================================================

#define AppName        "Data Management System"
#define AppVersion     "1.0.0"
#define AppPublisher   "Braentech Software"
#define AppExeName     "DataManagementSystem.exe"
#define AppId          "{{B4E2A7C1-3D8F-4E56-9A2B-C7D3E8F1A4B5}"
#define SourceDir      "..\publish\app"

; ── Passkey configuration ─────────────────────────────────────────────────────
; Only the SHA-256 hash of the passkey is stored here (lowercase hex).
; The plain passkey is NEVER written in this file.
; To update: follow instructions in the header comment above.
#define PasskeyHash "66f859ed6050ae11fcd26edf52c6b5b00b28c6eb93566114c4fd8d028795f056"

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

; ── Post-install launch offer (only when license activation succeeded) ─────────
[Run]
Filename: "{app}\{#AppExeName}"; \
  Description: "Launch {#AppName}"; \
  Flags: nowait postinstall skipifsilent; \
  Check: WasLicenseActivated

; ══════════════════════════════════════════════════════════════════════════════
;  PASCAL SCRIPT
; ══════════════════════════════════════════════════════════════════════════════
[Code]

// ── Global state ──────────────────────────────────────────────────────────────
var
  // Passkey wizard page (shown first, before directory selection)
  PasskeyPage     : TWizardPage;
  PasskeyEdit     : TEdit;
  PasskeyHint     : TLabel;
  PasskeyError    : TLabel;

  // License key wizard page (shown after files are installed)
  LicenseKeyPage  : TWizardPage;
  KeyLabel        : TLabel;
  KeyEdit         : TEdit;
  KeyHint         : TLabel;

  // Result page
  ResultPage      : TWizardPage;
  ResultHeading   : TLabel;
  ResultDetail    : TLabel;
  MachineIdLabel  : TLabel;
  MachineIdEdit   : TEdit;
  CopyBtn         : TButton;
  CopyHint        : TLabel;

  // Flags
  PasskeyVerified    : Boolean;   // set to True when correct passkey entered
  LicenseActivated   : Boolean;   // set to True when license.lic written
  MachineIdStr       : String;

// ── IsAlphanumeric: reject any non-alphanumeric character ─────────────────────
function IsAlphanumeric(const S: String): Boolean;
var
  I : Integer;
  C : Char;
begin
  Result := True;
  for I := 1 to Length(S) do
  begin
    C := S[I];
    if not (((C >= 'A') and (C <= 'Z')) or
            ((C >= 'a') and (C <= 'z')) or
            ((C >= '0') and (C <= '9'))) then
    begin
      Result := False;
      Exit;
    end;
  end;
end;

// ── SHA256OfString ────────────────────────────────────────────────────────────
// Computes the SHA-256 of a UTF-8 string by writing a small PowerShell script
// to a temp file and executing it. Returns the lowercase hex digest, or ''
// on error. PowerShell is always available on Windows 10/11 (our min target).
//
// Writing a .ps1 file avoids all inline command-line quoting problems.
// The input S must already be validated as alphanumeric before calling this
// function; no shell injection is possible from alphanumeric-only input.
function SHA256OfString(const S: String): String;
var
  ScriptFile : String;
  OutFile    : String;
  Lines      : TArrayOfString;
  ExitCode   : Integer;
  I          : Integer;
  Line       : String;
begin
  Result     := '';
  ScriptFile := GetTempDir() + '\dms_pk.ps1';
  OutFile    := GetTempDir() + '\dms_pk_out.txt';

  if FileExists(ScriptFile) then DeleteFile(ScriptFile);
  if FileExists(OutFile)    then DeleteFile(OutFile);

  // Build a PowerShell script as an array of lines
  SetArrayLength(Lines, 5);
  Lines[0] := '$bytes = [System.Text.Encoding]::UTF8.GetBytes(''' + S + ''')';
  Lines[1] := '$sha   = [System.Security.Cryptography.SHA256]::Create()';
  Lines[2] := '$hash  = $sha.ComputeHash($bytes)';
  Lines[3] := '$hex   = [System.BitConverter]::ToString($hash).Replace(''-'', '''').ToLower()';
  Lines[4] := '$hex   | Out-File -Encoding ASCII -NoNewline -FilePath ''' + OutFile + '''';

  if not SaveStringsToFile(ScriptFile, Lines, False) then Exit;

  Exec(ExpandConstant('{sys}') + '\WindowsPowerShell\v1.0\powershell.exe',
    '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' + ScriptFile + '"',
    '', SW_HIDE, ewWaitUntilTerminated, ExitCode);

  // Read result
  SetArrayLength(Lines, 0);
  if LoadStringsFromFile(OutFile, Lines) then
    for I := 0 to GetArrayLength(Lines) - 1 do
    begin
      Line := Trim(Lines[I]);
      if Length(Line) = 64 then
      begin
        Result := LowerCase(Line);
        Break;
      end;
    end;

  DeleteFile(ScriptFile);
  DeleteFile(OutFile);
end;

// ── ValidatePasskey: hash the entered key and compare ─────────────────────────
function ValidatePasskey(const Entered: String): Boolean;
var
  Digest : String;
begin
  Result := False;
  if Length(Entered) <> 16          then Exit;
  if not IsAlphanumeric(Entered)    then Exit;
  Digest := SHA256OfString(Entered);
  Result := (Digest = '{#PasskeyHash}');
end;

// ── Helpers ───────────────────────────────────────────────────────────────────
function ReadFileContents(FileName: String): String;
var
  Lines : TArrayOfString;
  I     : Integer;
begin
  Result := '';
  if not LoadStringsFromFile(FileName, Lines) then Exit;
  for I := 0 to GetArrayLength(Lines) - 1 do
    Result := Result + Lines[I];
end;

procedure CopyTextToClipboard(const Text: String);
var
  TmpFile : String;
  Arr     : TArrayOfString;
  ExitCode: Integer;
begin
  TmpFile := GetTempDir() + '\dms_clip.txt';
  SetArrayLength(Arr, 1);
  Arr[0] := Text;
  if SaveStringsToFile(TmpFile, Arr, False) then
  begin
    Exec('cmd.exe', '/c clip < "' + TmpFile + '"', '',
         SW_HIDE, ewWaitUntilTerminated, ExitCode);
    DeleteFile(TmpFile);
  end;
end;

procedure CopyMachineId(Sender: TObject);
begin
  if MachineIdStr <> '' then
  begin
    CopyTextToClipboard(MachineIdStr);
    CopyHint.Caption :=
      'Copied! Send this Machine ID to {#AppPublisher} to request a license key.';
  end;
end;

function GetMachineId: String;
var
  ExePath    : String;
  TmpA       : String;
  TmpB       : String;
  ResultCode : Integer;
begin
  Result  := '';
  ExePath := ExpandConstant('{app}\{#AppExeName}');
  TmpA    := ExpandConstant('{tmp}\DMS_MachineId.txt');
  TmpB    := GetTempDir() + '\DMS_MachineId.txt';

  if FileExists(TmpA) then DeleteFile(TmpA);
  if FileExists(TmpB) then DeleteFile(TmpB);
  if not FileExists(ExePath) then Exit;

  Exec(ExePath, '--machine-id', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

  if FileExists(TmpA) then
  begin
    Result := ReadFileContents(TmpA);
    DeleteFile(TmpA);
  end else if FileExists(TmpB) then
  begin
    Result := ReadFileContents(TmpB);
    DeleteFile(TmpB);
  end;
end;

function TryActivate(LicenseKey: String): String;
var
  ExePath    : String;
  TmpA       : String;
  TmpB       : String;
  ResultCode : Integer;
  Raw        : String;
  PipePos    : Integer;
begin
  Result  := '';
  ExePath := ExpandConstant('{app}\{#AppExeName}');
  TmpA    := ExpandConstant('{tmp}\DMS_Activate_Result.txt');
  TmpB    := GetTempDir() + '\DMS_Activate_Result.txt';

  if FileExists(TmpA) then DeleteFile(TmpA);
  if FileExists(TmpB) then DeleteFile(TmpB);

  if not Exec(ExePath, '--activate "' + LicenseKey + '"', '',
              SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    Result := 'Could not run activation process. Installation may be incomplete.';
    Exit;
  end;

  Raw := '';
  if FileExists(TmpA) then
  begin
    Raw := ReadFileContents(TmpA);
    DeleteFile(TmpA);
  end else if FileExists(TmpB) then
  begin
    Raw := ReadFileContents(TmpB);
    DeleteFile(TmpB);
  end;

  if ResultCode = 0 then Exit;

  PipePos := Pos('|', Raw);
  if (PipePos > 0) and (Length(Raw) > PipePos) then
    Result := Copy(Raw, PipePos + 1, Length(Raw))
  else if Raw <> '' then
    Result := Raw
  else
    Result := 'License validation failed (code ' + IntToStr(ResultCode) + ').';
end;

// ── [Run] Check function ──────────────────────────────────────────────────────
function WasLicenseActivated: Boolean;
begin
  Result := LicenseActivated;
end;

// ── ShowResult ────────────────────────────────────────────────────────────────
procedure ShowResult(Success: Boolean; Detail: String; MachineId: String);
begin
  if Success then
  begin
    ResultHeading.Caption    := 'Activation Successful';
    ResultHeading.Font.Color := clGreen;
    ResultDetail.Caption     := Detail;
    MachineIdLabel.Visible   := False;
    MachineIdEdit.Visible    := False;
    CopyBtn.Visible          := False;
    CopyHint.Visible         := False;
  end else begin
    ResultHeading.Caption    := 'Activation Required';
    ResultHeading.Font.Color := clRed;
    ResultDetail.Caption     := Detail;
    if MachineId <> '' then
    begin
      MachineIdLabel.Visible := True;
      MachineIdEdit.Text     := MachineId;
      MachineIdEdit.Visible  := True;
      CopyBtn.Visible        := True;
      CopyHint.Caption       :=
        'Copy your Machine ID and send it to {#AppPublisher} to receive a license key.';
      CopyHint.Visible       := True;
    end;
  end;
end;

// ── CreateCustomPages ─────────────────────────────────────────────────────────
procedure CreateCustomPages;
var
  S : TWinControl;
begin
  // ── PAGE 1: Installation Passkey ──────────────────────────────────────────
  // Inserted right after wpWelcome — before directory selection or any
  // other wizard page. No files or folders exist at this point.
  PasskeyPage := CreateCustomPage(
    wpWelcome,
    'Installation Passkey',
    'Enter the passkey to authorise this installation.');

  S := PasskeyPage.Surface;

  with TLabel.Create(PasskeyPage) do
  begin
    Parent   := S;
    Caption  := 'INSTALLATION PASSKEY';
    Left     := 0;
    Top      := ScaleY(6);
    Font.Style := [fsBold];
  end;

  PasskeyEdit := TEdit.Create(PasskeyPage);
  PasskeyEdit.Parent       := S;
  PasskeyEdit.Left         := 0;
  PasskeyEdit.Top          := ScaleY(24);
  PasskeyEdit.Width        := S.Width;
  PasskeyEdit.Height       := ScaleY(24);
  PasskeyEdit.Font.Name    := 'Consolas';
  PasskeyEdit.Font.Size    := 12;
  PasskeyEdit.MaxLength    := 16;
  PasskeyEdit.PasswordChar := #0;  // visible — passkey is shared, not personal

  PasskeyError := TLabel.Create(PasskeyPage);
  PasskeyError.Parent      := S;
  PasskeyError.Left        := 0;
  PasskeyError.Top         := ScaleY(56);
  PasskeyError.Caption     := '';
  PasskeyError.Font.Color  := clRed;
  PasskeyError.Font.Style  := [fsBold];
  PasskeyError.AutoSize    := True;
  PasskeyError.Visible     := False;

  PasskeyHint := TLabel.Create(PasskeyPage);
  PasskeyHint.Parent   := S;
  PasskeyHint.Left     := 0;
  PasskeyHint.Top      := ScaleY(76);
  PasskeyHint.Width    := S.Width;
  PasskeyHint.Height   := ScaleY(60);
  PasskeyHint.AutoSize := False;
  PasskeyHint.WordWrap := True;
  PasskeyHint.Caption  :=
    'The passkey is exactly 16 characters (letters and numbers).' + #13#10 +
    'It is provided by {#AppPublisher} with your software licence.' + #13#10 + #13#10 +
    'Installation cannot proceed without the correct passkey.';

  // ── PAGE 2: License Activation ────────────────────────────────────────────
  LicenseKeyPage := CreateCustomPage(
    wpSelectTasks,
    'License Activation',
    'Enter your license key to activate {#AppName}.');

  S := LicenseKeyPage.Surface;

  KeyLabel := TLabel.Create(LicenseKeyPage);
  KeyLabel.Parent  := S;
  KeyLabel.Caption := 'LICENSE KEY';
  KeyLabel.Left    := 0;
  KeyLabel.Top     := ScaleY(6);
  KeyLabel.Font.Style := [fsBold];

  KeyEdit := TEdit.Create(LicenseKeyPage);
  KeyEdit.Parent     := S;
  KeyEdit.Left       := 0;
  KeyEdit.Top        := ScaleY(24);
  KeyEdit.Width      := S.Width;
  KeyEdit.Height     := ScaleY(24);
  KeyEdit.Font.Name  := 'Consolas';
  KeyEdit.Font.Size  := 9;

  KeyHint := TLabel.Create(LicenseKeyPage);
  KeyHint.Parent   := S;
  KeyHint.Left     := 0;
  KeyHint.Top      := ScaleY(56);
  KeyHint.Width    := S.Width;
  KeyHint.Height   := ScaleY(80);
  KeyHint.AutoSize := False;
  KeyHint.WordWrap := True;
  KeyHint.Caption  :=
    'Paste the license key provided by {#AppPublisher} (format: V1.xxxxx.xxxxx).' + #13#10 + #13#10 +
    'If you do not yet have a key, leave this field empty.' + #13#10 +
    'The application will display your Machine ID on first launch.' + #13#10 +
    'Send it to {#AppPublisher} to receive your license key.';

  // ── PAGE 3: Activation Result ─────────────────────────────────────────────
  ResultPage := CreateCustomPage(
    wpInfoAfter,
    'Activation Status',
    'Review the result before finishing.');

  S := ResultPage.Surface;

  ResultHeading := TLabel.Create(ResultPage);
  ResultHeading.Parent     := S;
  ResultHeading.Left       := 0;
  ResultHeading.Top        := 0;
  ResultHeading.Font.Style := [fsBold];
  ResultHeading.Font.Size  := 11;
  ResultHeading.AutoSize   := True;

  ResultDetail := TLabel.Create(ResultPage);
  ResultDetail.Parent   := S;
  ResultDetail.Left     := 0;
  ResultDetail.Top      := ScaleY(28);
  ResultDetail.Width    := S.Width;
  ResultDetail.Height   := ScaleY(60);
  ResultDetail.AutoSize := False;
  ResultDetail.WordWrap := True;

  MachineIdLabel := TLabel.Create(ResultPage);
  MachineIdLabel.Parent     := S;
  MachineIdLabel.Left       := 0;
  MachineIdLabel.Top        := ScaleY(96);
  MachineIdLabel.Caption    := 'YOUR MACHINE ID';
  MachineIdLabel.Font.Style := [fsBold];
  MachineIdLabel.Visible    := False;

  MachineIdEdit := TEdit.Create(ResultPage);
  MachineIdEdit.Parent    := S;
  MachineIdEdit.Left      := 0;
  MachineIdEdit.Top       := ScaleY(114);
  MachineIdEdit.Width     := S.Width - ScaleX(80);
  MachineIdEdit.Height    := ScaleY(24);
  MachineIdEdit.ReadOnly  := True;
  MachineIdEdit.Font.Name := 'Consolas';
  MachineIdEdit.Font.Size := 11;
  MachineIdEdit.Visible   := False;

  CopyBtn := TButton.Create(ResultPage);
  CopyBtn.Parent   := S;
  CopyBtn.Caption  := 'Copy';
  CopyBtn.Left     := S.Width - ScaleX(72);
  CopyBtn.Top      := ScaleY(112);
  CopyBtn.Width    := ScaleX(72);
  CopyBtn.Height   := ScaleY(26);
  CopyBtn.OnClick  := @CopyMachineId;
  CopyBtn.Visible  := False;

  CopyHint := TLabel.Create(ResultPage);
  CopyHint.Parent   := S;
  CopyHint.Left     := 0;
  CopyHint.Top      := ScaleY(148);
  CopyHint.Width    := S.Width;
  CopyHint.Height   := ScaleY(40);
  CopyHint.AutoSize := False;
  CopyHint.WordWrap := True;
  CopyHint.Visible  := False;
end;

// ── InitializeWizard ──────────────────────────────────────────────────────────
procedure InitializeWizard;
begin
  PasskeyVerified  := False;
  LicenseActivated := False;
  MachineIdStr     := '';
  CreateCustomPages;
end;

// ── NextButtonClick ───────────────────────────────────────────────────────────
function NextButtonClick(CurPageID: Integer): Boolean;
var
  Entered : String;
  Key     : String;
begin
  Result := True;

  // ── Passkey gate ──────────────────────────────────────────────────────────
  if CurPageID = PasskeyPage.ID then
  begin
    Entered := PasskeyEdit.Text;  // do NOT trim — whitespace = wrong key

    // Length check (fast, no hashing needed)
    if Length(Entered) <> 16 then
    begin
      PasskeyError.Caption := 'Invalid Activation Key.';
      PasskeyError.Visible := True;
      Result := False;
      Exit;
    end;

    // Character set check (must be alphanumeric)
    if not IsAlphanumeric(Entered) then
    begin
      PasskeyError.Caption := 'Invalid Activation Key.';
      PasskeyError.Visible := True;
      Result := False;
      Exit;
    end;

    // SHA-256 comparison
    if not ValidatePasskey(Entered) then
    begin
      PasskeyError.Caption := 'Invalid Activation Key.';
      PasskeyError.Visible := True;
      Result := False;
      Exit;
    end;

    // Correct passkey — allow progression
    PasskeyVerified      := True;
    PasskeyError.Visible := False;
    Exit;
  end;

  // ── License key format check ──────────────────────────────────────────────
  if CurPageID = LicenseKeyPage.ID then
  begin
    Key := Trim(KeyEdit.Text);
    if Key = '' then Exit;  // empty = deferred activation (allowed)

    if (Length(Key) < 20) or (Copy(Key, 1, 3) <> 'V1.') then
    begin
      MsgBox(
        'The license key format is not valid.' + #13#10 +
        'A valid key begins with "V1." and is provided by {#AppPublisher}.' + #13#10 + #13#10 +
        'Paste the complete key, or leave the field empty to activate later.',
        mbError, MB_OK);
      Result := False;
    end;
  end;
end;

// ── CurStepChanged ────────────────────────────────────────────────────────────
procedure CurStepChanged(CurStep: TSetupStep);
var
  Key     : String;
  ErrMsg  : String;
  FullMsg : String;
  PD      : String;
  AD      : String;
begin
  // ── Belt-and-suspenders guard: abort if passkey was never verified ─────────
  // This fires before ssInstall (which is when [Dirs] and [Files] are
  // processed). If PasskeyVerified is False, nothing has been written yet.
  if CurStep = ssInstall then
  begin
    if not PasskeyVerified then
    begin
      MsgBox(
        'Installation cannot proceed: passkey verification was not completed.' + #13#10 +
        'Please restart the installer and enter the correct passkey.',
        mbCriticalError, MB_OK);
      Abort;
    end;
  end;

  if CurStep <> ssPostInstall then Exit;

  // ── Seed appsettings.json to ProgramData (first install only) ────────────
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

  // ── License activation ────────────────────────────────────────────────────
  Key := Trim(KeyEdit.Text);

  if Key = '' then
  begin
    MachineIdStr := GetMachineId();
    ShowResult(False,
      'No license key was entered.' + #13#10 + #13#10 +
      '{#AppName} has been installed but cannot be used until activated.' + #13#10 +
      'On first launch, the app will show your Machine ID.' + #13#10 +
      'Send it to {#AppPublisher} to receive a license key.',
      MachineIdStr);
    Exit;
  end;

  ErrMsg := TryActivate(Key);

  if ErrMsg = '' then
  begin
    LicenseActivated := True;
    ShowResult(True,
      '{#AppName} has been successfully activated and is ready to use.' + #13#10 +
      'Click Finish to launch the application.',
      '');
    Exit;
  end;

  // Activation failed — collect Machine ID, show error, roll back
  MachineIdStr := GetMachineId();
  FullMsg :=
    'License activation failed.' + #13#10 + #13#10 +
    ErrMsg + #13#10 + #13#10;
  if MachineIdStr <> '' then
    FullMsg := FullMsg +
      'Your Machine ID is: ' + MachineIdStr + #13#10 +
      'Contact {#AppPublisher} with this ID to obtain a valid license key.' + #13#10 + #13#10;
  FullMsg := FullMsg +
    'The installation will now be rolled back.' + #13#10 +
    'Re-run this installer with a valid key, or leave the key field ' +
    'empty and activate from inside the application.';

  MsgBox(FullMsg, mbCriticalError, MB_OK);
  Abort;
end;

// ── ShouldSkipPage ────────────────────────────────────────────────────────────
function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := False;
  // Block all pages after the passkey page if the passkey has not been verified.
  // This prevents the user from clicking Back after seeing the error and then
  // forward-navigating past the passkey page without re-entering the key.
  if (PageID <> PasskeyPage.ID) and (PageID <> wpWelcome) then
    if not PasskeyVerified then
      Result := True;  // skip every subsequent page; user is stuck at passkey
end;
