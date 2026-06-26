@echo off
setlocal

:: ============================================================
::  DMS Production Installer Build Script
::  Run this from ANY directory — resolves paths automatically.
::
::  Prerequisites:
::    - .NET 10 SDK
::    - Inno Setup 6  https://jrsoftware.org/isdl.php
::    - RSA keys generated: LicenseManager.cs must have the
::      real public key (not the placeholder).
::
::  Usage:
::    build.bat                         default Inno Setup path
::    build.bat "C:\path\to\ISCC.exe"   explicit ISCC path
::
::  Output:  Installer\Output\DMS_Setup_1.0.0.exe
::
::  IMPORTANT — key management:
::    Private key  C:\Users\<you>\braentech-keys\private.pem
::               NEVER commit, never ship, store securely.
::    Public key   embedded in LicenseManager.cs at build time.
::    License gen  dotnet run --project DataManagementSystem.LicenseGenerator
::                   -- --create --machine XXXX-XXXX-XXXX-XXXX
::                      --client "Name" --project DMS --days 365
::                      --privkey "C:\path\to\private.pem"
:: ============================================================

:: Locate solution root (one level up from this script's directory)
set "SCRIPT_DIR=%~dp0"
set "ROOT=%SCRIPT_DIR%.."
pushd "%ROOT%"
set "ROOT=%CD%"
popd

set "PUBLISH_DIR=%ROOT%\publish\app"
set "PROJECT=%ROOT%\DataManagementSystem\DataManagementSystem.csproj"
set "ISS_SCRIPT=%ROOT%\Installer\DMS_Setup.iss"

echo.
echo ============================================================
echo  Data Management System — Build + Package
echo ============================================================
echo  Solution root : %ROOT%
echo  Publish dir   : %PUBLISH_DIR%
echo.

:: ---- Step 1: Clean previous publish output ----
if exist "%PUBLISH_DIR%" (
    echo [1/3] Cleaning previous publish output...
    rd /s /q "%PUBLISH_DIR%"
)

:: ---- Step 2: Publish self-contained win-x64 ----
echo [2/3] Publishing application (self-contained, win-x64)...
echo.
dotnet publish "%PROJECT%" ^
    --configuration Release ^
    --runtime win-x64 ^
    --self-contained true ^
    -p:PublishSingleFile=false ^
    -p:PublishReadyToRun=true ^
    --output "%PUBLISH_DIR%"

if %ERRORLEVEL% neq 0 (
    echo.
    echo ERROR: dotnet publish failed. See output above.
    exit /b 1
)

echo.
echo   Publish succeeded. Files in: %PUBLISH_DIR%

:: ---- Step 3: Locate ISCC.exe ----
if not "%~1"=="" (
    set "ISCC=%~1"
) else (
    :: Try standard install locations (Inno Setup 6)
    if exist "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" (
        set "ISCC=C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
    ) else if exist "C:\Program Files\Inno Setup 6\ISCC.exe" (
        set "ISCC=C:\Program Files\Inno Setup 6\ISCC.exe"
    ) else (
        echo.
        echo ERROR: Inno Setup 6 not found.
        echo   Install from: https://jrsoftware.org/isdl.php
        echo   Or pass the ISCC.exe path as an argument:
        echo     build.bat "C:\path\to\ISCC.exe"
        exit /b 1
    )
)

:: ---- Step 4: Compile installer ----
echo.
echo [3/3] Compiling installer...
echo   Script : %ISS_SCRIPT%
echo   ISCC   : %ISCC%
echo.

"%ISCC%" "%ISS_SCRIPT%"

if %ERRORLEVEL% neq 0 (
    echo.
    echo ERROR: Inno Setup compilation failed. See output above.
    exit /b 1
)

echo.
echo ============================================================
echo  SUCCESS
echo  Installer: %ROOT%\Installer\Output\DMS_Setup_1.0.0.exe
echo ============================================================
echo.

endlocal
