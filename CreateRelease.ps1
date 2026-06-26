#Requires -Version 5.1
<#
.SYNOPSIS
    DMS release builder.
    Checks for .NET SDK and Inno Setup (installing or using the bundled copy
    as needed), then builds the app + installer and packages Setup.exe,
    ReleaseNotes.txt, ReleaseInfo.txt, and HowToActivate.txt into a
    timestamped Releases/ folder.

    Licenses are machine-locked. After the customer installs and sends
    their Machine ID, issue a key with:
        .\IssueLicense.ps1 -MachineId XXXX-XXXX-XXXX-XXXX -Client "Name"

.PARAMETER Version
    Version number embedded in the release folder name (default: 1.0.0).

.PARAMETER ReleaseNotes
    Release notes text. If omitted the script prompts interactively.
#>
param (
    [string] $Version      = '1.0.0',
    [string] $ReleaseNotes = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$Root         = $PSScriptRoot
$AppProject   = Join-Path $Root 'DataManagementSystem\DataManagementSystem.csproj'
$IssFile      = Join-Path $Root 'load\DMS_Setup.iss'
$PublishDir   = Join-Path $Root 'publish\app'
$InstallerOut = Join-Path $Root 'load\Output'
$ReleasesDir  = Join-Path $Root 'Releases'
$Timestamp    = Get-Date -Format 'yyyy-MM-dd_HHmmss'
$ReleaseId    = "DMS_v${Version}_${Timestamp}"
$ReleaseDir   = Join-Path $ReleasesDir $ReleaseId

function Step([string]$Msg) { Write-Host "`n>>> $Msg" -ForegroundColor Cyan }
function Ok([string]$Msg)   { Write-Host "  $Msg" -ForegroundColor Green }
function Info([string]$Msg) { Write-Host "  $Msg" -ForegroundColor DarkGray }
function Fail([string]$Msg) { Write-Host "`n[FAILED] $Msg" -ForegroundColor Red; exit 1 }

function Ask([string]$Prompt) {
    Write-Host "  $Prompt " -ForegroundColor Yellow -NoNewline
    return (Read-Host).Trim()
}

# =============================================================================
# 1. CHECK .NET SDK
# =============================================================================
Step 'Checking .NET SDK'

$dotnetCmd = Get-Command dotnet -ErrorAction SilentlyContinue
if ($dotnetCmd) {
    $sdkVersion = dotnet --version 2>$null
    Ok ".NET SDK found: $sdkVersion"
} else {
    Write-Host '  .NET SDK not found on this machine.' -ForegroundColor Yellow
    $ans = Ask 'Install .NET SDK now? (Y/N):'
    if ($ans -notmatch '^[Yy]') { Fail 'Cannot continue without .NET SDK. Install from https://dot.net and re-run.' }

    Step 'Downloading and installing .NET SDK (this may take a few minutes)...'
    $installerScript = Join-Path $env:TEMP 'dotnet-install.ps1'
    Invoke-WebRequest 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installerScript -UseBasicParsing
    & $installerScript -Channel LTS -InstallDir (Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet')

    # Refresh PATH for this session
    $env:PATH = (Join-Path $env:LOCALAPPDATA 'Microsoft\dotnet') + ';' + $env:PATH
    $dotnetCmd = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $dotnetCmd) { Fail '.NET SDK installation failed. Please install manually from https://dot.net' }
    Ok ".NET SDK installed: $(dotnet --version)"
}

# =============================================================================
# 2. CHECK INNO SETUP
# =============================================================================
Step 'Checking Inno Setup'

$systemISCC  = 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
$systemISCC2 = 'C:\Program Files\Inno Setup 6\ISCC.exe'
$bundledISCC = Join-Path $Root 'tools\innosetup\ISCC.exe'

if (Test-Path $systemISCC) {
    $ISCC = $systemISCC
    Ok "Inno Setup found (system): $ISCC"
} elseif (Test-Path $systemISCC2) {
    $ISCC = $systemISCC2
    Ok "Inno Setup found (system): $ISCC"
} elseif (Test-Path $bundledISCC) {
    $ISCC = $bundledISCC
    Ok "Inno Setup not installed - using bundled copy."
} else {
    Write-Host '  Inno Setup not found (system or bundled).' -ForegroundColor Yellow
    $ans = Ask 'Do you have Inno Setup 6 installed somewhere else? Enter path to ISCC.exe, or leave blank to abort:'
    if ($ans -and (Test-Path $ans)) {
        $ISCC = $ans
        Ok "Using: $ISCC"
    } else {
        Fail 'Inno Setup not found. Bundled copy is missing and no system install detected. Re-clone the repo to restore tools\innosetup\.'
    }
}

# =============================================================================
# 3. PREFLIGHT CHECKS
# =============================================================================
Step 'Preflight checks'

if (-not (Test-Path $AppProject)) { Fail "App project not found: $AppProject" }
if (-not (Test-Path $IssFile))    { Fail "Installer script not found: $IssFile" }

Ok 'App project   : OK'
Ok 'Installer ISS : OK'

# =============================================================================
# 4. CLEAN PREVIOUS PUBLISH
# =============================================================================
Step 'Cleaning previous publish output'

if (Test-Path $PublishDir) {
    Remove-Item $PublishDir -Recurse -Force
    Info "Removed $PublishDir"
}

# =============================================================================
# 5. PUBLISH APPLICATION
# =============================================================================
Step 'Publishing application (Release, win-x64, self-contained)'

& dotnet publish $AppProject -c Release -r win-x64 --self-contained true -o $PublishDir --nologo
if ($LASTEXITCODE -ne 0) { Fail "dotnet publish failed (exit $LASTEXITCODE)." }

if (-not (Test-Path (Join-Path $PublishDir 'DataManagementSystem.exe'))) {
    Fail "Published exe not found in: $PublishDir"
}
Ok "Published to: $PublishDir"

# =============================================================================
# 6. BUILD INSTALLER
# =============================================================================
Step 'Building installer with Inno Setup'

& $ISCC '/Q' $IssFile
if ($LASTEXITCODE -ne 0) { Fail "ISCC.exe failed (exit $LASTEXITCODE)." }

$SetupExe = Get-ChildItem -Path $InstallerOut -Filter '*.exe' |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1

if (-not $SetupExe) { Fail "No installer .exe found in $InstallerOut after build." }
Ok "Installer built: $($SetupExe.Name)"

# =============================================================================
# 7. SHA-256
# =============================================================================
Step 'Computing SHA-256 of installer'

$SetupHash = (Get-FileHash -Path $SetupExe.FullName -Algorithm SHA256).Hash.ToLower()
Info "SHA256: $SetupHash"

# =============================================================================
# 8. RELEASE NOTES
# =============================================================================
Step 'Release notes'

if (-not $ReleaseNotes) {
    Write-Host '  Enter release notes. Press Enter after each line.' -ForegroundColor Cyan
    Write-Host '  Leave a blank line and press Enter to finish.' -ForegroundColor DarkGray
    $lines = @()
    while ($true) {
        $line = Read-Host
        if ($line -eq '') { break }
        $lines += $line
    }
    $ReleaseNotes = $lines -join "`n"
}

# =============================================================================
# 9. ASSEMBLE RELEASE FOLDER
# =============================================================================
Step "Creating release folder: $ReleaseDir"

New-Item -ItemType Directory -Force -Path $ReleaseDir | Out-Null
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

Copy-Item $SetupExe.FullName (Join-Path $ReleaseDir 'Setup.exe')

[System.IO.File]::WriteAllLines(
    (Join-Path $ReleaseDir 'ReleaseInfo.txt'),
    @(
        "Release ID       : $ReleaseId",
        "Version          : $Version",
        "Build date (UTC) : $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')",
        "",
        "Setup.exe SHA256 : $SetupHash"
    ),
    $utf8NoBom
)

[System.IO.File]::WriteAllText(
    (Join-Path $ReleaseDir 'ReleaseNotes.txt'),
    ("DMS v$Version - Release Notes`n" +
     "Released: $(Get-Date -Format 'yyyy-MM-dd')`n" +
     ('-' * 50) + "`n" +
     $(if ($ReleaseNotes.Trim()) { $ReleaseNotes.Trim() } else { '(no release notes provided)' })),
    $utf8NoBom
)

$activateText = "DATA MANAGEMENT SYSTEM - ACTIVATION INSTRUCTIONS`n" +
    "==================================================`n`n" +
    "Your license is locked to ONE computer. Follow these steps:`n`n" +
    "  1. Run Setup.exe and install the application.`n`n" +
    "  2. Launch Data Management System. On first launch it shows a`n" +
    "     License Activation window with your MACHINE ID, e.g.`n`n" +
    "         Machine ID:  A1B2-C3D4-E5F6-7890`n`n" +
    "  3. Click Copy Machine ID and send that ID to Braentech.`n`n" +
    "  4. Braentech sends back your personal license key.`n`n" +
    "  5. Paste the key into the activation window and click Activate.`n`n" +
    "The key only works on the machine whose Machine ID you sent.`n" +
    "It cannot be reused on another computer.`n"
[System.IO.File]::WriteAllText(
    (Join-Path $ReleaseDir 'HowToActivate.txt'),
    $activateText,
    $utf8NoBom
)

Ok 'Setup.exe         : copied'
Ok 'ReleaseInfo.txt   : written'
Ok 'ReleaseNotes.txt  : written'
Ok 'HowToActivate.txt : written'

# =============================================================================
# 10. DONE
# =============================================================================
$sep = '=' * 70
Write-Host ''
Write-Host $sep -ForegroundColor Yellow
Write-Host ' RELEASE COMPLETE' -ForegroundColor Yellow
Write-Host $sep -ForegroundColor Yellow
Write-Host "  Release folder : $ReleaseDir"
Write-Host "  Installer      : Setup.exe ($([math]::Round($SetupExe.Length / 1MB, 1)) MB)"
Write-Host "  SHA256         : $SetupHash"
Write-Host ''
Write-Host '  Next steps:' -ForegroundColor Cyan
Write-Host '    1. Send customer: Setup.exe + HowToActivate.txt'
Write-Host '    2. Customer installs, launches, copies their Machine ID'
Write-Host '    3. Run: .\IssueLicense.ps1 -MachineId XXXX-XXXX-XXXX-XXXX -Client "Name"' -ForegroundColor Yellow
Write-Host '    4. Send customer the generated LicenseKey.txt'
Write-Host $sep -ForegroundColor Yellow
