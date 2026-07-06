@echo off
chcp 65001 >nul
setlocal

set "ARGS="
if /I "%~1"=="nopause" set "ARGS=-NoPause"
if /I "%~1"=="--nopause" set "ARGS=-NoPause"

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\build-prod.ps1" %ARGS%
set "EXIT_CODE=%errorlevel%"

if %EXIT_CODE% neq 0 (
    pause
    exit /b %EXIT_CODE%
)

if not defined ARGS pause
exit /b 0
