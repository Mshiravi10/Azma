@echo off
setlocal
chcp 65001 >nul
cd /d "%~dp0"
title Azma - Stopping All Services

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\stop-azma.ps1" %*
set EXIT_CODE=%ERRORLEVEL%

if /i not "%AZMA_NO_PAUSE%"=="1" (
    echo.
    echo Press any key to close this window...
    pause >nul
)

exit /b %EXIT_CODE%
