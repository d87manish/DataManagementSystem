# DMS Developer Guide

Data Management System — Braentech Software  
WPF desktop app, .NET 10, self-contained win-x64, RSA offline licensing.

---

## Table of Contents

1. [Prerequisites](#1-prerequisites)
2. [First-Time Setup](#2-first-time-setup)
3. [Project Structure](#3-project-structure)
4. [Building a Release](#4-building-a-release)
5. [Issuing a License to a Customer](#5-issuing-a-license-to-a-customer)
6. [Customer Activation Flow](#6-customer-activation-flow)
7. [RSA Key Management](#7-rsa-key-management)
8. [Branching & Git Workflow](#8-branching--git-workflow)

---

## 1. Prerequisites

| Tool | Required for | Notes |
|---|---|---|
| .NET 10 SDK | Building + publishing the app | `CreateRelease.ps1` will offer to install it if missing |
| Inno Setup 6 | Building the installer | Optional — bundled copy in `tools\innosetup\` is used automatically if not installed |
| Private key `.pem` | Issuing license keys | See [RSA Key Management](#7-rsa-key-management) |

> **Customer machines need nothing.** Setup.exe is fully self-contained (no .NET runtime required).

---

## 2. First-Time Setup

### 2a. Clone the repo

```powershell
git clone https://github.com/d87manish/DataManagementSystem.git
cd DataManagementSystem
git checkout develop
```

### 2b. Get the private key

The RSA private key is **never committed**. Obtain `private.pem` from a team member and place it at:

```
C:\Users\<you>\braentech-keys\private.pem
```

> If this is a brand-new project with no existing key, generate a fresh pair — see [RSA Key Management](#7-rsa-key-management).

### 2c. Verify everything works

```powershell
.\CreateRelease.ps1 -Version "1.0.0" -ReleaseNotes "Test build"
```

The script checks for .NET SDK and Inno Setup automatically. If either is missing it will prompt to install or use the bundled copy.

---

## 3. Project Structure

```
DataManagementSystem/
├── DataManagementSystem/           # WPF application
│   └── Licensing/
│       ├── LicenseManager.cs       # RSA verification + activation logic
│       ├── MachineIdProvider.cs    # Derives machine ID from CPU/disk/MAC
│       └── LicenseWindow.xaml      # Activation UI shown on first launch
├── DataManagementSystem.LicenseGenerator/  # Dev-only CLI tool (never ship)
├── load/
│   ├── DMS_Setup.iss               # Inno Setup installer script
│   └── Output/                     # (gitignored) compiled Setup.exe lands here
├── tools/
│   └── innosetup/                  # Bundled ISCC.exe — no install needed
├── publish/app/                    # (gitignored) dotnet publish output
├── Releases/                       # (gitignored) release packages from CreateRelease.ps1
├── Licenses/                       # (gitignored) issued license keys from IssueLicense.ps1
├── CreateRelease.ps1               # Builds a release package
└── IssueLicense.ps1                # Issues a machine-locked license key
```

---

## 4. Building a Release

Run from the repo root in PowerShell:

```powershell
.\CreateRelease.ps1 -Version "1.2.0" -ReleaseNotes "Fixed export bug, added dark mode"
```

**What it does:**

1. Checks .NET SDK — installs automatically if missing (prompts first)
2. Checks Inno Setup — uses system install, falls back to `tools\innosetup\`, or prompts for path
3. `dotnet publish` — self-contained win-x64 to `publish\app\`
4. ISCC compiles `load\DMS_Setup.iss` — output goes to `load\Output\`
5. Assembles `Releases\DMS_v1.2.0_<timestamp>\` containing:
   - `Setup.exe` — the installer to send the customer
   - `HowToActivate.txt` — instructions to send alongside Setup.exe
   - `ReleaseNotes.txt` — changelog for this version
   - `ReleaseInfo.txt` — build date + SHA-256 of Setup.exe

**Send the customer:** `Setup.exe` + `HowToActivate.txt`

> **No license key is generated here.** Keys are machine-locked and issued separately after the customer sends their Machine ID.

---

## 5. Issuing a License to a Customer

After the customer installs and sends their Machine ID (from the activation screen):

```powershell
.\IssueLicense.ps1 -MachineId "A1B2-C3D4-E5F6-7890" -Client "Acme Corp" -LicenseDays 365
```

**What it does:**

1. Validates the Machine ID format (`XXXX-XXXX-XXXX-XXXX`) — rejects wildcards
2. Calls `LicenseGenerator --create` with the customer's Machine ID
3. Writes to `Licenses\AcmeCorp_A1B2-C3D4-E5F6-7890\`:
   - `LicenseKey.txt` — the key to send the customer (paste into activation window)
   - `LicenseInfo.txt` — your record of who got what key

**Send the customer:** `LicenseKey.txt`

The key is cryptographically bound to that Machine ID. It will be rejected on any other machine.

### Optional parameters

| Parameter | Default | Description |
|---|---|---|
| `-MachineId` | *(required)* | Customer's Machine ID from the activation window |
| `-Client` | `Customer` | Client name embedded in the license |
| `-LicenseDays` | `365` | License validity in days |
| `-PrivKeyPath` | `%USERPROFILE%\braentech-keys\private.pem` | Path to RSA private key |
| `-OutDir` | `Licenses\<Client>_<MachineId>\` | Output directory |

---

## 6. Customer Activation Flow

What the customer experiences:

1. Run `Setup.exe` — installs to `C:\Program Files\Braentech\DMS\`
2. Launch **Data Management System**
3. On first launch, a **License Activation** window appears showing their Machine ID
4. They click **Copy Machine ID** and send it to you (email, etc.)
5. You run `IssueLicense.ps1` and send back `LicenseKey.txt`
6. They paste the key into the activation window and click **Activate**
7. App starts normally. License stored at `C:\ProgramData\Braentech\DMS\license.lic`

> **Re-installation:** The installer deletes `license.lic` on every install. The customer will need to activate again after upgrading — but the same key works (same machine, same Machine ID).

---

## 7. RSA Key Management

### Generate a new key pair (first time only)

```powershell
dotnet run --project DataManagementSystem.LicenseGenerator -- --genkeys --out "$env:USERPROFILE\braentech-keys"
```

This prints the public key to the console. Copy it into `LicenseManager.cs`:

```csharp
internal const string PublicKeyPem =
    """
    -----BEGIN PUBLIC KEY-----
    <paste new public key here>
    -----END PUBLIC KEY-----
    """;
```

Then rebuild and publish a new release. **All previously issued licenses are invalidated** when you rotate keys.

### Rules

- `private.pem` — never commit, never email, store securely
- `public.pem` — embedded in the app source, committed, safe to share
- If the private key is lost, generate a new pair and issue fresh licenses to all customers

---

## 8. Branching & Git Workflow

| Branch | Purpose |
|---|---|
| `main` | Stable, tested code |
| `develop` | Active development |

```powershell
# Normal work
git checkout develop
# ... make changes, commit ...
git push

# Release to main (after testing)
git checkout main
git merge develop
git push
```

All commits from `CreateRelease.ps1` and `IssueLicense.ps1` go on `develop`. PR to `main` only after testing passes.
