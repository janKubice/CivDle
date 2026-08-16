@echo off
REM ============================================================
REM  CivDle - natoceni zaberu do traileru (Windows)
REM
REM  Pouziti z prikazove radky:
REM    trailer.cmd              ... ostra verze 1920x1080, 60 fps
REM    trailer.cmd nahled       ... rychly nahled 960x540, 30 fps
REM
REM  Na dvojklik jsou vedle tohohle souboru:
REM    trailer-zabery.bat
REM    trailer-nahled.bat
REM
REM  Vysledek jsou sekvence PNG ve slozce trailer\<zaber>\. Video z nich
REM  udela ffmpeg - prikaz hra na konci vypise hotovy.
REM
REM  Bez diakritiky schvalne: cmd.exe bezi v jine kodove strance nez
REM  UTF-8 a hacky by se vypsaly jako smeti prave ve chvili, kdy si ma
REM  clovek precist chybovou hlasku.
REM ============================================================
setlocal
cd /d "%~dp0"

set "REZIM=%~1"
if "%REZIM%"=="" set "REZIM=ostra"

if /i "%REZIM%"=="ostra" (
    set "REZIM_ARG="
    set "POPIS=OSTRA VERZE (1920x1080, 60 fps)"
) else if /i "%REZIM%"=="nahled" (
    set "REZIM_ARG=--nahled"
    set "POPIS=NAHLED (960x540, 30 fps)"
) else (
    echo [CHYBA] Neznamy rezim "%REZIM%". Pouzij "ostra" nebo "nahled".
    goto :chyba
)

set "VYSTUP=trailer"

echo.
echo ============================================================
echo   CivDle - natacim zabery do traileru
echo   Rezim:  %POPIS%
echo   Vystup: %VYSTUP%
echo ============================================================
echo.
echo   Chvili to potrva - kazdy snimek se pocita v plnem detailu,
echo   bez zjednoduseni pri oddaleni. Okno hry blikne a zase zmizi,
echo   to je v poradku: kresli se mimo obrazovku.
echo.

where dotnet >nul 2>nul
if errorlevel 1 (
    echo [CHYBA] Nenasel jsem prikaz "dotnet".
    echo.
    echo   Nainstaluj .NET 8 SDK z https://dotnet.microsoft.com/download
    echo   a spust skript znovu.
    goto :chyba
)

REM Stara sekvence se maze, aby v nove nezustaly snimky z minuleho
REM natoceni - ffmpeg cte cislovanou radu a stare snimky by se do ni
REM zamichaly. Kdyz mazani selze, neni to duvod skoncit.
if exist "%VYSTUP%" (
    rmdir /s /q "%VYSTUP%" 2>nul
    if exist "%VYSTUP%" (
        echo [POZOR] Nejde smazat "%VYSTUP%" - nemas ji otevrenou v prohlizeci?
        echo         Pokracuji, ale zkontroluj si, co ve slozce zbylo.
        echo.
    )
)

dotnet run --project src\CivDle\CivDle.csproj -c Release -- --trailer "%VYSTUP%" %REZIM_ARG%
if errorlevel 1 goto :chyba

echo.
echo ============================================================
echo   HOTOVO
echo   Zabery jsou v: %CD%\%VYSTUP%
echo ============================================================
goto :konec

:chyba
echo.
echo ============================================================
echo   NATACENI SELHALO
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
