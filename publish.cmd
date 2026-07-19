@echo off
rem Vytvoří distribuci hry jako jedno spustitelné exe (self-contained, bez nutnosti
rem instalovat .NET). Použití:
rem   publish.cmd            → Windows x64 (výchozí)
rem   publish.cmd linux-x64  → Linux x64
setlocal
cd /d "%~dp0"

set RID=%1
if "%RID%"=="" set RID=win-x64
set OUT=dist\%RID%

dotnet publish src\CivDle\CivDle.csproj -c Release -r %RID% --self-contained -p:PublishSingleFile=true -o "%OUT%"
if errorlevel 1 exit /b 1

echo.
echo Hotovo: %OUT% (CivDle.exe + slozka data\)
endlocal
