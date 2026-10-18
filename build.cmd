@echo off
setlocal
cd /d "%~dp0"

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo Could not find the .NET Framework C# compiler.
    exit /b 1
)

if not exist dist mkdir dist

"%CSC%" -nologo -target:winexe -platform:anycpu -optimize+ ^
    -out:dist\Gear-Batt.exe ^
    -win32icon:app.ico ^
    -win32manifest:app.manifest ^
    -r:System.dll ^
    -r:System.Core.dll ^
    -r:System.Drawing.dll ^
    -r:System.Windows.Forms.dll ^
    src\AssemblyInfo.cs ^
    src\Native.cs ^
    src\Models.cs ^
    src\Providers.cs ^
    src\Hidpp.cs ^
    src\HidDevices.cs ^
    src\VendorProtocols.cs ^
    src\IconRenderer.cs ^
    src\Settings.cs ^
    src\TrayApp.cs ^
    src\Program.cs

if errorlevel 1 exit /b 1
echo Built dist\Gear-Batt.exe

"%CSC%" -nologo -target:winexe -platform:anycpu -optimize+ ^
    -out:dist\Gear-Batt-Collector.exe ^
    -win32icon:app.ico ^
    -win32manifest:collector.manifest ^
    -r:System.dll ^
    -r:System.Core.dll ^
    -r:System.Drawing.dll ^
    -r:System.Windows.Forms.dll ^
    src\Native.cs ^
    src\Models.cs ^
    src\Providers.cs ^
    src\Hidpp.cs ^
    src\HidDevices.cs ^
    src\VendorProtocols.cs ^
    collector\CollectorAssemblyInfo.cs ^
    collector\CollectorProgram.cs ^
    collector\CollectorForm.cs ^
    collector\CollectorEngine.cs

if errorlevel 1 exit /b 1
echo Built dist\Gear-Batt-Collector.exe
endlocal
