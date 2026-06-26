#Requires -Version 5.1
<#
.SYNOPSIS
    One-click DMS release generator.
    Builds the app, compiles the installer, generates a unique license key,
    and packages everything into a timestamped Releases/ folder.

.PARAMETER Version
    Version number embedded in the release folder name (default: 1.0.0).

.PARAMETER Client
    Client name recorded in the license (default: Customer).

.PARAMETER LicenseDays
    Number of days the license is valid (default: 365).

.PARAMETER PrivKeyPath
    Path to your RSA private key PEM file.
    Default: $HOME\braentech-keys\private.pem
#>
param (
    [string] $Version     = '1.0.0',
    [string] $Client      = 'Customer',
    [int]    $LicenseDays = 365,
    [string] $PrivKeyPath = (Join-Path $env:USERPROFILE 'braentech-keys\private.pem')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ── Paths ────────────────────────────────────────────────────────────────────
$Root        = $PSScriptRoot
$AppProject  = Join-Path $Root 'DataManagementSystem\DataManagementSystem.csproj'
$LicProject  = Join-Path $Root 'DataManagementSystem.LicenseGenerator\DataManagementSystem.LicenseGenerator.csproj'
$IssFile     = Join-Path $Root 'Installer\DMS_Setup.iss'
$ISCC        = 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
$PublishDir  = Join-Path $Root 'publish\app'
$InstallerOut= Join-Path $Root 'Installer\Output'
$ReleasesDir = Join-Path $Root 'Releases'

$Timestamp   = Get-Date -Format 'yyyy-MM-dd_HHmmss'
$ReleaseId   = "DMS_v${Version}_${Timestamp}"
$ReleaseDir  = Join-Path $ReleasesDir $ReleaseId

# ── Helper: write a step header ───────────────────────────────────────────────
function Step([string]$Msg) {
    Write-Host "`n>>> $Msg" -ForegroundColor Cyan
}

# ── Helper: die with a red message ───────────────────────────────────────────
function Fail([string]$Msg) {
    Write-Host "`n[FAILED] $Msg" -ForegroundColor Red
    exit 1
}

# ─────────────────────────────────────────────────────────────────────────────
# 1. PREFLIGHT CHECKS
# ─────────────────────────────────────────────────────────────────────────────
Step 'Preflight checks'

if (-not (Test-Path $PrivKeyPath))  { Fail "Private key not found: $PrivKeyPath" }
if (-not (Test-Path $ISCC))         { Fail "Inno Setup not found: $ISCC" }
if (-not (Test-Path $AppProject))   { Fail "App project not found: $AppProject" }
if (-not (Test-Path $LicProject))   { Fail "LicenseGenerator project not found: $LicProject" }
if (-not (Test-Path $IssFile))      { Fail "Installer script not found: $IssFile" }

Write-Host '  Private key   : OK' -ForegroundColor Green
Write-Host '  Inno Setup    : OK' -ForegroundColor Green
Write-Host '  App project   : OK' -ForegroundColor Green
Write-Host '  LicGenerator  : OK' -ForegroundColor Green
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
Write-Host "  Installer: $($SetupExe.FullName)" -ForegroundColor Green

# ─────────────────────────────────────────────────────────────────────────────
# 5. GENERATE LICENSE KEY
# ─────────────────────────────────────────────────────────────────────────────
Step 'Generating unique license key (machine wildcard)'

$licArgs = @(
    'run', '--project', $LicProject,
    '--no-build',
    '--',
    '--create',
    '--machine', '*',
    '--client',  $Client,
    '--project', 'DMS',
    '--days',    $LicenseDays,
    '--privkey', $PrivKeyPath
)

# Build LicenseGenerator first so --no-build works
& dotnet build $LicProject -c Release --nologo -v q
if ($LASTEXITCODE -ne 0) { Fail "LicenseGenerator build failed (exit $LASTEXITCODE)." }

$licOutput = & dotnet @licArgs 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host $licOutput
    Fail "LicenseGenerator failed (exit $LASTEXITCODE)."
}

# Extract the line starting with V1.
$LicenseKey = ($licOutput | Where-Object { $_ -match '^V1\.' } | Select-Object -First 1)
if (-not $LicenseKey) {
    Write-Host $licOutput
    Fail 'Could not extract license key from LicenseGenerator output.'
}
$LicenseKey = $LicenseKey.Trim()
Write-Host "  Key extracted successfully." -ForegroundColor Green

# ─────────────────────────────────────────────────────────────────────────────
# 6. COMPUTE SETUP.EXE SHA-256
# ─────────────────────────────────────────────────────────────────────────────
Step 'Computing SHA-256 of installer'

$SetupHash = (Get-FileHash -Path $SetupExe.FullName -Algorithm SHA256).Hash.ToLower()
Write-Host "  SHA256: $SetupHash" -ForegroundColor DarkGray

# ─────────────────────────────────────────────────────────────────────────────
# 7. ASSEMBLE RELEASE FOLDER
# ─────────────────────────────────────────────────────────────────────────────
Step "Creating release folder: $ReleaseDir"

New-Item -ItemType Directory -Force -Path $ReleaseDir | Out-Null

# Setup.exe
Copy-Item $SetupExe.FullName (Join-Path $ReleaseDir 'Setup.exe')

# LicenseKey.txt — key only, no trailing newline
[System.IO.File]::WriteAllText(
    (Join-Path $ReleaseDir 'LicenseKey.txt'),
    $LicenseKey,
    [System.Text.Encoding]::UTF8
)

# ReleaseInfo.txt
$InfoLines = @(
    "Release ID   : $ReleaseId",
    "Version      : $Version",
    "Build date   : $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') UTC",
    "Client       : $Client",
    "License days : $LicenseDays",
    "",
    "License key  : $LicenseKey",
    "",
    "Setup.exe SHA256 : $SetupHash"
)
[System.IO.File]::WriteAllLines(
    (Join-Path $ReleaseDir 'ReleaseInfo.txt'),
    $InfoLines,
    [System.Text.Encoding]::UTF8
)

Write-Host '  Setup.exe      → copied' -ForegroundColor Green
Write-Host '  LicenseKey.txt → written' -ForegroundColor Green
Write-Host '  ReleaseInfo.txt→ written' -ForegroundColor Green

# ─────────────────────────────────────────────────────────────────────────────
# 8. SUCCESS SUMMARY
# ─────────────────────────────────────────────────────────────────────────────
Write-Host "`n" + ('=' * 70) -ForegroundColor Yellow
Write-Host ' RELEASE COMPLETE' -ForegroundColor Yellow
Write-Host ('=' * 70) -ForegroundColor Yellow
Write-Host "  Release folder : $ReleaseDir"
Write-Host "  Installer      : Setup.exe ($([math]::Round($SetupExe.Length / 1MB, 1)) MB)"
Write-Host "  SHA256         : $SetupHash"
Write-Host ''
Write-Host '  LICENSE KEY (paste into LicenseKey.txt to send to customer):' -ForegroundColor Cyan
Write-Host "  $LicenseKey" -ForegroundColor Yellow
Write-Host ''
Write-Host '  Customer instructions:' -ForegroundColor Cyan
Write-Host '    1. Run Setup.exe to install Data Management System.'
Write-Host '    2. Launch the application.'
Write-Host '    3. When the License Activation window appears, paste the key from LicenseKey.txt.'
Write-Host '    4. Click Activate.'
Write-Host ('=' * 70) -ForegroundColor Yellow
