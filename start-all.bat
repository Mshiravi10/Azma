@echo off
setlocal
chcp 65001 >nul
cd /d "%~dp0"
title Azma - Starting All Services

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\start-azma.ps1" %*
set EXIT_CODE=%ERRORLEVEL%

if /i not "%AZMA_NO_PAUSE%"=="1" (
    echo.
    if "%EXIT_CODE%"=="0" (
        echo Press any key to open the Web UI in your browser...
        pause >nul
        start http://localhost:5173
    ) else (
        echo Startup failed. Press any key to close this window...
        pause >nul
    )
)

exit /b %EXIT_CODE%
