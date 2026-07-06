@echo off
chcp 65001 >nul
setlocal

set "ROOT=%~dp0"
set "PROJECT=%ROOT%customSTT\customSTT.csproj"
set "EXE=%ROOT%customSTT\bin\Debug\net10.0-windows\customSTT.exe"

echo === customSTT: сборка и запуск ===
echo.

taskkill /F /IM customSTT.exe >nul 2>&1

dotnet build "%PROJECT%" --configuration Debug
if %errorlevel% neq 0 (
    echo ОШИБКА СБОРКИ
    pause
    exit /b %errorlevel%
)

if not exist "%EXE%" (
    echo Не найден: %EXE%
    pause
    exit /b 1
)

echo Запуск: %EXE%
start "" "%EXE%"
