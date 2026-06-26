#Requires -Version 5.1
<#
.SYNOPSIS
    Issues a machine-locked DMS license key.

    Run this AFTER the customer installs the app and sends you the Machine ID
    shown on their activation screen. The generated key only validates on that
    one machine - it cannot be reused on another computer.

.PARAMETER MachineId
    The customer's Machine ID, formatted XXXX-XXXX-XXXX-XXXX
    (copied from the app's License Activation window).

.PARAMETER Client
    Client name recorded in the license (default: Customer).

.PARAMETER LicenseDays
    Number of days the license is valid (default: 365).

.PARAMETER PrivKeyPath
    Path to your RSA private key PEM file.
    Default: $HOME\braentech-keys\private.pem

.PARAMETER OutDir
    Where to write LicenseKey.txt. Default: Licenses\<Client>_<MachineId>\

.EXAMPLE
    .\IssueLicense.ps1 -MachineId A1B2-C3D4-E5F6-7890 -Client "Acme Corp"
#>
param (
    [Parameter(Mandatory = $true)]
    [string] $MachineId,
    [string] $Client      = 'Customer',
    [int]    $LicenseDays  = 365,
    [string] $PrivKeyPath  = (Join-Path $env:USERPROFILE 'braentech-keys\private.pem'),
    [string] $OutDir       = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$Root       = $PSScriptRoot
$LicProject = Join-Path $Root 'DataManagementSystem.LicenseGenerator\DataManagementSystem.LicenseGenerator.csproj'

function Step([string]$Msg) { Write-Host "`n>>> $Msg" -ForegroundColor Cyan }
function Fail([string]$Msg) { Write-Host "`n[FAILED] $Msg" -ForegroundColor Red; exit 1 }

# ── Check .NET SDK ────────────────────────────────────────────────────────────
Step 'Checking .NET 10 SDK'
$dotnetCmd = Get-Command dotnet -ErrorAction SilentlyContinue
if ($dotnetCmd) {
    $sdkVersion = dotnet --version 2>$null
    Write-Host "  .NET SDK found: $sdkVersion" -ForegroundColor Green
} else {
    Write-Host '  .NET 10 SDK not found on this machine.' -ForegroundColor Yellow
    Write-Host '  Opening download page in your browser...' -ForegroundColor Yellow
    Start-Process 'https://dotnet.microsoft.com/download/dotnet/10.0'
    Fail 'Install .NET 10 SDK, then re-run this script.'
}

# ── Validate inputs ──────────────────────────────────────────────────────────
Step 'Validating inputs'

$MachineId = $MachineId.Trim().ToUpperInvariant()
if ($MachineId -eq '*') {
    Fail "Refusing to issue a wildcard license. Provide the customer's real Machine ID."
}
if ($MachineId -notmatch '^[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{4}-[0-9A-F]{4}$') {
    Fail "Machine ID must look like XXXX-XXXX-XXXX-XXXX (hex). Got: $MachineId"
}
if (-not (Test-Path $PrivKeyPath)) {
    Fail ("Private key not found: $PrivKeyPath`n" +
          "  This file is never committed to the repo.`n" +
          "  Obtain private.pem from a team member and place it at that path,`n" +
          "  or pass -PrivKeyPath 'C:\path\to\private.pem' to specify a custom location.`n" +
          "  See DEVELOPER_GUIDE.md section 2b for details.")
}
if (-not (Test-Path $LicProject))  { Fail "LicenseGenerator project not found: $LicProject" }

Write-Host "  Machine ID  : $MachineId" -ForegroundColor Green
Write-Host "  Client      : $Client"    -ForegroundColor Green
Write-Host "  Valid days  : $LicenseDays" -ForegroundColor Green
Write-Host "  Private key : OK"          -ForegroundColor Green

# ── Generate the license ─────────────────────────────────────────────────────
Step 'Generating machine-locked license key'

& dotnet build $LicProject -c Release --nologo -v q
if ($LASTEXITCODE -ne 0) { Fail "LicenseGenerator build failed (exit $LASTEXITCODE)." }

$licArgs = @(
    'run', '--project', $LicProject,
    '--no-build',
    '--',
    '--create',
    '--machine', $MachineId,
    '--client',  $Client,
    '--project', 'DMS',
    '--days',    $LicenseDays,
    '--privkey', $PrivKeyPath
)

$licOutput = & dotnet @licArgs 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host $licOutput
    Fail "LicenseGenerator failed (exit $LASTEXITCODE)."
}

$LicenseKey = ($licOutput | Where-Object { $_ -match '^V1\.' } | Select-Object -First 1)
if (-not $LicenseKey) {
    Write-Host $licOutput
    Fail 'Could not extract license key from LicenseGenerator output.'
}
$LicenseKey = $LicenseKey.Trim()
Write-Host "  Key generated." -ForegroundColor Green

# ── Write output ─────────────────────────────────────────────────────────────
Step 'Writing LicenseKey.txt'

if (-not $OutDir) {
    $safeClient = ($Client -replace '[^\w\-]', '_')
    $OutDir = Join-Path $Root "Licenses\${safeClient}_${MachineId}"
}
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

# LicenseKey.txt - just the key, ready to paste
[System.IO.File]::WriteAllText(
    (Join-Path $OutDir 'LicenseKey.txt'),
    $LicenseKey,
    $utf8NoBom
)

# LicenseInfo.txt - your record of who got what
$InfoLines = @(
    "Issued on   : $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') UTC",
    "Client      : $Client",
    "Machine ID  : $MachineId",
    "Valid days  : $LicenseDays",
    "",
    "License key :",
    $LicenseKey
)
[System.IO.File]::WriteAllLines(
    (Join-Path $OutDir 'LicenseInfo.txt'),
    $InfoLines,
    $utf8NoBom
)

# ── Summary ──────────────────────────────────────────────────────────────────
$sep = '=' * 70
Write-Host ''
Write-Host $sep -ForegroundColor Yellow
Write-Host ' LICENSE ISSUED' -ForegroundColor Yellow
Write-Host $sep -ForegroundColor Yellow
Write-Host "  Client     : $Client"
Write-Host "  Machine ID : $MachineId"
Write-Host "  Output     : $OutDir"
Write-Host ''
Write-Host '  Send LicenseKey.txt to the customer. They paste it into the' -ForegroundColor Cyan
Write-Host '  activation window and click Activate. It works ONLY on this machine.' -ForegroundColor Cyan
Write-Host $sep -ForegroundColor Yellow
