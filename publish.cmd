@echo off
REM ============================================================
REM  CivDle - sestaveni distribuce (Windows)
REM
REM  Pouziti z prikazove radky:
REM    publish.cmd                  ... win-x64, plna hra
REM    publish.cmd win-x64 demo     ... win-x64, DEMOVERZE
REM    publish.cmd linux-x64        ... linux-x64, plna hra
REM
REM  Na dvojklik jsou vedle tohohle souboru:
REM    publish-plna-hra.bat
REM    publish-demo.bat
REM
REM  Bez diakritiky schvalne: cmd.exe bezi v jine kodove strance nez
REM  UTF-8 a hacky by se vypsaly jako smeti prave ve chvili, kdy si ma
REM  clovek precist chybovou hlasku.
REM ============================================================
setlocal
cd /d "%~dp0"

set "RID=%~1"
if "%RID%"=="" set "RID=win-x64"

set "EDICE=%~2"
if "%EDICE%"=="" set "EDICE=full"

if /i "%EDICE%"=="full" (
    set "VYSTUP=dist\%RID%"
    set "EDICE_ARG="
    set "POPIS=PLNA HRA"
) else if /i "%EDICE%"=="demo" (
    set "VYSTUP=dist\%RID%-demo"
    set "EDICE_ARG=-p:GameEdition=Demo"
    set "POPIS=DEMOVERZE"
) else (
    echo [CHYBA] Neznama edice "%EDICE%". Pouzij "full" nebo "demo".
    goto :chyba
)

echo.
echo ============================================================
echo   CivDle - sestavuji: %POPIS%  ^(%RID%^)
echo   Vystup: %VYSTUP%
echo ============================================================
echo.

where dotnet >nul 2>nul
if errorlevel 1 (
    echo [CHYBA] Nenasel jsem prikaz "dotnet".
    echo.
    echo   Nainstaluj .NET 8 SDK z https://dotnet.microsoft.com/download
    echo   a spust skript znovu.
    goto :chyba
)

REM Stara slozka se maze, aby v ni nezustaly soubory z minuleho buildu
REM (hlavne po prepnuti edice). Kdyz mazani selze - typicky protoze hra
REM z te slozky prave bezi - neni to duvod skoncit: nove soubory stare
REM prepisou. Jen se to musi rict nahlas.
if exist "%VYSTUP%" (
    rmdir /s /q "%VYSTUP%" 2>nul
    if exist "%VYSTUP%" (
        echo [POZOR] Nejde smazat "%VYSTUP%" - nebezi hra z te slozky?
        echo         Pokracuji, nove soubory ty stare prepisou.
        echo.
    )
)

dotnet publish src\CivDle\CivDle.csproj -c Release -r "%RID%" --self-contained -p:PublishSingleFile=true %EDICE_ARG% -o "%VYSTUP%"
if errorlevel 1 goto :chyba

echo.
echo ============================================================
echo   HOTOVO: %POPIS%
echo   Hra je v: %CD%\%VYSTUP%
if /i "%EDICE%"=="demo" echo   Ze je to demo poznas v menu podle odznaku DEMO.
echo ============================================================
goto :konec

:chyba
echo.
echo ============================================================
echo   SESTAVENI SELHALO
echo   Duvod je ve vypisu vyse - hledej radky zacinajici "error".
echo ============================================================
call :pauza_pri_dvojkliku
endlocal
exit /b 1

:konec
call :pauza_pri_dvojkliku
endlocal
exit /b 0

REM Pauza jen kdyz uzivatel na soubor kliknul dvakrat. Kdyz skript vola
REM jiny skript nebo bezi z prikazove radky, cekat na klavesu nema smysl.
:pauza_pri_dvojkliku
echo %cmdcmdline% | find /i "%~nx0" >nul
if not errorlevel 1 (
    echo.
    pause
)
exit /b 0
