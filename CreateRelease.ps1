#Requires -Version 5.1
<#
.SYNOPSIS
    DMS release builder.
    Builds the app + installer and packages Setup.exe, ReleaseNotes.txt,
    ReleaseInfo.txt, and HowToActivate.txt into a timestamped Releases/ folder.

    NOTE: This script does NOT generate a license key. Licenses are now
    locked to the customer's machine. After the customer installs and reads
    their Machine ID off the activation screen, issue a key with:

        .\IssueLicense.ps1 -MachineId XXXX-XXXX-XXXX-XXXX -Client "Name"

.PARAMETER Version
    Version number embedded in the release folder name (default: 1.0.0).

.PARAMETER ReleaseNotes
    Optional release notes text to write into ReleaseNotes.txt.
    If omitted, the script prompts interactively (leave blank to skip).
#>
param (
    [string] $Version      = '1.0.0',
    [string] $ReleaseNotes = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ── Paths ─────────────────────────────────────────────────────────────────────
$Root        = $PSScriptRoot
$AppProject  = Join-Path $Root 'DataManagementSystem\DataManagementSystem.csproj'
$IssFile     = Join-Path $Root 'load\DMS_Setup.iss'
$ISCC        = if (Test-Path (Join-Path $Root 'tools\innosetup\ISCC.exe')) {
                   Join-Path $Root 'tools\innosetup\ISCC.exe'
               } else {
                   'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
               }
$PublishDir  = Join-Path $Root 'publish\app'
$InstallerOut= Join-Path $Root 'load\Output'
$ReleasesDir = Join-Path $Root 'Releases'

$Timestamp   = Get-Date -Format 'yyyy-MM-dd_HHmmss'
$ReleaseId   = "DMS_v${Version}_${Timestamp}"
$ReleaseDir  = Join-Path $ReleasesDir $ReleaseId

# ── Helpers ───────────────────────────────────────────────────────────────────
function Step([string]$Msg) {
    Write-Host "`n>>> $Msg" -ForegroundColor Cyan
}

function Fail([string]$Msg) {
    Write-Host "`n[FAILED] $Msg" -ForegroundColor Red
    exit 1
}

# ─────────────────────────────────────────────────────────────────────────────
# 1. PREFLIGHT CHECKS
# ─────────────────────────────────────────────────────────────────────────────
Step 'Preflight checks'

if (-not (Test-Path $ISCC))         { Fail "Inno Setup not found: $ISCC" }
if (-not (Test-Path $AppProject))   { Fail "App project not found: $AppProject" }
if (-not (Test-Path $IssFile))      { Fail "Installer script not found: $IssFile" }

Write-Host '  Inno Setup    : OK' -ForegroundColor Green
Write-Host '  App project   : OK' -ForegroundColor Green
Write-Host '  Installer ISS : OK' -ForegroundColor Green

# ─────────────────────────────────────────────────────────────────────────────
# 2. CLEAN PREVIOUS PUBLISH
# ─────────────────────────────────────────────────────────────────────────────
Step 'Cleaning previous publish output'

if (Test-Path $PublishDir) {
    Remove-Item $PublishDir -Recurse -Force
    Write-Host "  Removed $PublishDir" -ForegroundColor DarkGray
}

# ─────────────────────────────────────────────────────────────────────────────
# 3. PUBLISH APPLICATION
# ─────────────────────────────────────────────────────────────────────────────
Step 'Publishing application (Release, win-x64, self-contained)'

$publishArgs = @(
    'publish', $AppProject,
    '-c', 'Release',
    '-r', 'win-x64',
    '--self-contained', 'true',
    '-o', $PublishDir,
    '--nologo'
)

& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { Fail "dotnet publish failed (exit $LASTEXITCODE)." }

$AppExe = Join-Path $PublishDir 'DataManagementSystem.exe'
if (-not (Test-Path $AppExe)) { Fail "Published exe not found at: $AppExe" }
Write-Host "  Published to: $PublishDir" -ForegroundColor Green

# ─────────────────────────────────────────────────────────────────────────────
# 4. BUILD INSTALLER
# ─────────────────────────────────────────────────────────────────────────────
Step 'Building installer with Inno Setup'

& $ISCC '/Q' $IssFile
if ($LASTEXITCODE -ne 0) { Fail "ISCC.exe failed (exit $LASTEXITCODE)." }

$SetupExe = Get-ChildItem -Path $InstallerOut -Filter '*.exe' |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1

if (-not $SetupExe) { Fail "No installer .exe found in $InstallerOut after build." }
Write-Host "  Installer built: $($SetupExe.Name)" -ForegroundColor Green

# ─────────────────────────────────────────────────────────────────────────────
# 5. COMPUTE SETUP.EXE SHA-256
# ─────────────────────────────────────────────────────────────────────────────
Step 'Computing SHA-256 of installer'

$SetupHash = (Get-FileHash -Path $SetupExe.FullName -Algorithm SHA256).Hash.ToLower()
Write-Host "  SHA256: $SetupHash" -ForegroundColor DarkGray

# ─────────────────────────────────────────────────────────────────────────────
# 6. ASSEMBLE RELEASE FOLDER
# ─────────────────────────────────────────────────────────────────────────────
Step "Creating release folder: $ReleaseDir"

New-Item -ItemType Directory -Force -Path $ReleaseDir | Out-Null

# Setup.exe
Copy-Item $SetupExe.FullName (Join-Path $ReleaseDir 'Setup.exe')

# UTF-8 without BOM
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

# ReleaseInfo.txt
$InfoLines = @(
    "Release ID   : $ReleaseId",
    "Version      : $Version",
    "Build date   : $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') UTC",
    "",
    "Setup.exe SHA256 : $SetupHash"
)
[System.IO.File]::WriteAllLines(
    (Join-Path $ReleaseDir 'ReleaseInfo.txt'),
    $InfoLines,
    $utf8NoBom
)

# ReleaseNotes.txt
if ($ReleaseNotes) {
    $NotesText = $ReleaseNotes
} else {
    Write-Host ''
    Write-Host '  Enter release notes (leave blank to skip, Ctrl+Z then Enter when done):' -ForegroundColor Cyan
    $lines = @()
    while ($true) {
        $line = Read-Host
        if ($null -eq $line) { break }
        $lines += $line
    }
    $NotesText = $lines -join "`n"
}

$NotesContent = "DMS v$Version - Release Notes`n" +
                "Released: $(Get-Date -Format 'yyyy-MM-dd')`n" +
                "-" * 50 + "`n" +
                $(if ($NotesText.Trim()) { $NotesText.Trim() } else { '(no release notes provided)' })
[System.IO.File]::WriteAllText(
    (Join-Path $ReleaseDir 'ReleaseNotes.txt'),
    $NotesContent,
    $utf8NoBom
)

# HowToActivate.txt - ship this to the customer alongside Setup.exe
$ActivateText = @"
DATA MANAGEMENT SYSTEM - ACTIVATION INSTRUCTIONS
==================================================

Your license is locked to ONE computer. Follow these steps:

  1. Run Setup.exe and install the application.

  2. Launch Data Management System. On first launch it shows a
     "License Activation" window with your MACHINE ID, e.g.

         Machine ID:  A1B2-C3D4-E5F6-7890

  3. Click "Copy Machine ID" and send that ID to Braentech
     (email it to your supplier).

  4. Braentech sends back your personal license key.

  5. Paste the key into the activation window and click "Activate".

The key only works on the machine whose Machine ID you sent.
It cannot be reused on another computer.
"@
[System.IO.File]::WriteAllText(
    (Join-Path $ReleaseDir 'HowToActivate.txt'),
    $ActivateText,
    $utf8NoBom
)

Write-Host '  Setup.exe        : copied'  -ForegroundColor Green
Write-Host '  ReleaseInfo.txt  : written' -ForegroundColor Green
Write-Host '  ReleaseNotes.txt : written' -ForegroundColor Green
Write-Host '  HowToActivate.txt: written' -ForegroundColor Green

# ─────────────────────────────────────────────────────────────────────────────
# 7. SUCCESS SUMMARY
# ─────────────────────────────────────────────────────────────────────────────
$sep = '=' * 70
Write-Host ''
Write-Host $sep -ForegroundColor Yellow
Write-Host ' RELEASE COMPLETE' -ForegroundColor Yellow
Write-Host $sep -ForegroundColor Yellow
Write-Host "  Release folder : $ReleaseDir"
Write-Host "  Installer      : Setup.exe ($([math]::Round($SetupExe.Length / 1MB, 1)) MB)"
Write-Host "  SHA256         : $SetupHash"
Write-Host ''
Write-Host '  Next steps (per-machine licensing):' -ForegroundColor Cyan
Write-Host '    1. Send the customer Setup.exe AND HowToActivate.txt.'
Write-Host '    2. Customer installs, launches, and sends you their Machine ID.'
Write-Host '    3. You issue a locked key:' -ForegroundColor Cyan
Write-Host '         .\IssueLicense.ps1 -MachineId XXXX-XXXX-XXXX-XXXX -Client "Name"' -ForegroundColor Yellow
Write-Host '    4. Send the customer the generated LicenseKey.txt.'
Write-Host '    5. Customer pastes the key and clicks Activate.'
Write-Host $sep -ForegroundColor Yellow
