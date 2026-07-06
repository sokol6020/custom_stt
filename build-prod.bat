@echo off
chcp 65001 >nul
setlocal EnableDelayedExpansion

set "ROOT=%~dp0"
set "APP_DIR=%ROOT%customSTT"
set "PROJECT=%APP_DIR%\customSTT.csproj"
set "BIN_DIR=%APP_DIR%\bin"
set "OBJ_DIR=%APP_DIR%\obj"
set "ICON_TOOL=%ROOT%tools\GenerateAppIcon\GenerateAppIcon.csproj"
set "ICON_OUT=%APP_DIR%\Assets\app.ico"
set "OUTPUT=%ROOT%publish"
set "EXE=%OUTPUT%\customSTT.exe"
set "DLL=%OUTPUT%\customSTT.dll"
set "SKIP_PAUSE="

if /I "%~1"=="nopause" set "SKIP_PAUSE=1"
if /I "%~1"=="--nopause" set "SKIP_PAUSE=1"

echo === customSTT: prod-сборка ===
echo.

echo Остановка запущенных экземпляров...
set "KILL_ATTEMPTS=0"
:wait_process_exit
taskkill /F /IM customSTT.exe >nul 2>&1
ping 127.0.0.1 -n 2 >nul
tasklist /FI "IMAGENAME eq customSTT.exe" 2>nul | find /I "customSTT.exe" >nul
if %errorlevel%==0 (
    set /a KILL_ATTEMPTS+=1
    if !KILL_ATTEMPTS! LSS 15 goto wait_process_exit
    echo ОШИБКА: не удалось завершить customSTT.exe. Закройте приложение вручную.
    if not defined SKIP_PAUSE pause
    exit /b 1
)

echo Полная очистка bin, obj и publish...
if exist "%OUTPUT%" rd /s /q "%OUTPUT%"
if exist "%BIN_DIR%" rd /s /q "%BIN_DIR%"
if exist "%OBJ_DIR%" rd /s /q "%OBJ_DIR%"

if exist "%OUTPUT%" (
    echo ОШИБКА: папка publish занята. Закройте customSTT.exe и повторите.
    if not defined SKIP_PAUSE pause
    exit /b 1
)

dotnet clean "%PROJECT%" -c Release -r win-x64 --nologo
if %errorlevel% neq 0 (
    echo ОШИБКА очистки
    if not defined SKIP_PAUSE pause
    exit /b %errorlevel%
)

echo Генерация иконки...
dotnet run --project "%ICON_TOOL%" -c Release --no-build -- "%ICON_OUT%" 2>nul
if %errorlevel% neq 0 (
    dotnet run --project "%ICON_TOOL%" -c Release -- "%ICON_OUT%"
)
if %errorlevel% neq 0 (
    echo ОШИБКА генерации иконки
    if not defined SKIP_PAUSE pause
    exit /b %errorlevel%
)

echo Восстановление пакетов...
dotnet restore "%PROJECT%" --runtime win-x64 --force
if %errorlevel% neq 0 (
    echo ОШИБКА восстановления пакетов
    if not defined SKIP_PAUSE pause
    exit /b %errorlevel%
)

echo Сборка Release win-x64...
dotnet publish "%PROJECT%" ^
    --configuration Release ^
    --runtime win-x64 ^
    --self-contained true ^
    --no-restore ^
    -p:ContinuousIntegrationBuild=true ^
    -p:PublishReadyToRun=false ^
    -p:DebugType=none ^
    -p:DebugSymbols=false ^
    --output "%OUTPUT%"

if %errorlevel% neq 0 (
    echo ОШИБКА СБОРКИ
    if not defined SKIP_PAUSE pause
    exit /b %errorlevel%
)

if not exist "%EXE%" (
    echo ОШИБКА: не найден %EXE%
    if not defined SKIP_PAUSE pause
    exit /b 1
)

for /f "usebackq delims=" %%V in (`powershell -NoProfile -Command "(Select-Xml -Path '%PROJECT%' -XPath '//Version').Node.InnerText"`) do set "APP_VERSION=%%V"
for /f "usebackq delims=" %%V in (`powershell -NoProfile -Command "[System.Reflection.AssemblyName]::GetAssemblyName('%DLL%').Version.ToString(3)"`) do set "BUILT_VERSION=%%V"

echo.
echo === ГОТОВО ===
echo Версия в проекте: %APP_VERSION%
echo Версия в сборке:  %BUILT_VERSION%
echo Запуск: %EXE%
for %%F in ("%EXE%") do echo Размер exe: %%~zF bytes, изменён: %%~tF
for %%F in ("%DLL%") do echo Размер dll: %%~zF bytes, изменён: %%~tF
echo.
echo Запускайте только: %EXE%
echo Не путайте с bin\Debug\... или releases\... от прошлых сборок.
echo.

if not "%APP_VERSION%"=="%BUILT_VERSION%" (
    echo ПРЕДУПРЕЖДЕНИЕ: версия в csproj и в DLL не совпадают.
)

if not defined SKIP_PAUSE pause
