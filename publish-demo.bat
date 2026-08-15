@echo off
REM Dvojklik = sestavi DEMOVERZI pro Windows x64 do dist\win-x64-demo.
REM Veskerou praci dela publish.cmd (viz publish-plna-hra.bat).
call "%~dp0publish.cmd" win-x64 demo
echo.
pause
