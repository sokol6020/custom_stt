@echo off
setlocal EnableDelayedExpansion

set "ROOT=%~dp0"
set "PROJECT=%ROOT%customSTT\customSTT.csproj"
set "ICON_TOOL=%ROOT%tools\GenerateAppIcon\GenerateAppIcon.csproj"
set "ICON_OUT=%ROOT%customSTT\Assets\app.ico"
set "OUTPUT=%ROOT%publish"
set "EXE=%OUTPUT%\customSTT.exe"

echo === customSTT: prod-сборка ===
echo.

echo Остановка запущенных экземпляров...
taskkill /F /IM customSTT.exe >nul 2>&1
timeout /t 2 /nobreak >nul

echo Генерация иконки...
dotnet run --project "%ICON_TOOL%" -c Release -- "%ICON_OUT%"
if %errorlevel% neq 0 (
    echo ОШИБКА генерации иконки
    pause
    exit /b %errorlevel%
)

echo Восстановление пакетов...
dotnet restore "%PROJECT%"
if %errorlevel% neq 0 (
    echo ОШИБКА восстановления пакетов
    pause
    exit /b %errorlevel%
)

echo Сборка Release win-x64...
dotnet publish "%PROJECT%" ^
    --configuration Release ^
    --runtime win-x64 ^
    --self-contained true ^
    -p:PublishReadyToRun=false ^
    -p:DebugType=none ^
    -p:DebugSymbols=false ^
    --output "%OUTPUT%"

if %errorlevel% neq 0 (
    echo ОШИБКА СБОРКИ
    pause
    exit /b %errorlevel%
)

if not exist "%EXE%" (
    echo ОШИБКА: не найден %EXE%
    pause
    exit /b 1
)

echo.
echo === ГОТОВО ===
echo Запуск: %EXE%
echo.
pause
