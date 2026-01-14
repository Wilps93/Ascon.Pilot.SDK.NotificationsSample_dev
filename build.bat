@echo off
echo Building Ascon.Pilot.SDK.NotificationsSample Extension...

REM Check if .NET Framework is available
where msbuild >nul 2>nul
if %errorlevel% neq 0 (
    echo Error: MSBuild not found. Please ensure .NET Framework is installed.
    pause
    exit /b 1
)

REM Check if required DLL exists
if not exist "..\Lib\Ascon.Pilot.SDK.dll" (
    echo Warning: Ascon.Pilot.SDK.dll not found in ..\Lib\ directory
    echo Please ensure the DLL is available before building.
    echo.
)

REM Restore NuGet packages
echo Restoring NuGet packages...
nuget restore Ascon.Pilot.SDK.NotificationsSample.ext2.sln

REM Build the solution
echo Building solution...
msbuild Ascon.Pilot.SDK.NotificationsSample.ext2.sln /p:Configuration=Debug /p:Platform="Any CPU"

if %errorlevel% equ 0 (
    echo.
    echo Build completed successfully!
    echo Output files should be in the bin\Debug\ directory.
) else (
    echo.
    echo Build failed with error code %errorlevel%
)

pause