@echo off
REM Dvojklik = sestavi PLNOU HRU pro Windows x64 do dist\win-x64.
REM Veskerou praci dela publish.cmd, tenhle soubor je jen zkratka na
REM dvojklik - aby se logika buildu nemusela udrzovat na dvou mistech.
call "%~dp0publish.cmd" win-x64 full
echo.
pause
