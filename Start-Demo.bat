@echo off
setlocal
cd /d "%~dp0"

where dotnet >nul 2>nul
if errorlevel 1 (
    echo [ERROR] .NET SDK was not found.
    echo Install .NET 8 SDK from: https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

dotnet --list-sdks | findstr /R /C:"^8\." >nul
if errorlevel 1 (
    echo [ERROR] .NET 8 SDK was not found.
    echo Install .NET 8 SDK from: https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)

echo Starting Generic Industrial Monitor Demo...
dotnet run --project "src\IndustrialMonitor.App\IndustrialMonitor.App.csproj"

if errorlevel 1 (
    echo.
    echo The application exited with an error.
    pause
)
