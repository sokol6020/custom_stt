@echo off
chcp 65001 >nul
setlocal EnableDelayedExpansion

set "ROOT=%~dp0"
set "PROJECT=%ROOT%customSTT\customSTT.csproj"
set "PUBLISH=%ROOT%publish"
set "RELEASES=%ROOT%releases"
set "NOTES_TEMPLATE=%ROOT%release-notes\template.txt"
set "VERSION=%~1"

echo === customSTT: создание релиза ===
echo.

if "%VERSION%"=="" (
    set /p VERSION=Версия релиза ^(например 1.2.0^): 
)
if "%VERSION%"=="" (
    echo ОШИБКА: версия не указана
    pause
    exit /b 1
)

echo %VERSION%| findstr /r "^[0-9][0-9]*\.[0-9][0-9]*\.[0-9][0-9]*$" >nul
if errorlevel 1 (
    echo ОШИБКА: версия должна быть в формате X.Y.Z
    pause
    exit /b 1
)

set "RELEASE_DIR=%RELEASES%\v%VERSION%"
set "NOTES_FILE=%RELEASE_DIR%\RELEASE_NOTES.txt"

if exist "%RELEASE_DIR%" (
    echo Папка релиза уже существует: %RELEASE_DIR%
    set /p OVERWRITE=Перезаписать? [y/N]: 
    if /I not "!OVERWRITE!"=="y" (
        echo Отменено.
        pause
        exit /b 1
    )
)

mkdir "%RELEASE_DIR%" 2>nul

if not "%~2"=="" (
    echo %~2> "%NOTES_FILE%"
) else (
    if exist "%ROOT%release-notes\%VERSION%.txt" (
        copy /Y "%ROOT%release-notes\%VERSION%.txt" "%NOTES_FILE%" >nul
        echo Используются заметки: release-notes\%VERSION%.txt
    ) else (
        copy /Y "%NOTES_TEMPLATE%" "%NOTES_FILE%" >nul
        echo.
        echo Откроется файл RELEASE_NOTES - опишите изменения, сохраните и закройте Notepad.
        pause
        notepad "%NOTES_FILE%"
    )
)

echo.
echo Обновление версии в проекте...
powershell -NoProfile -ExecutionPolicy Bypass -File "%ROOT%scripts\update-version.ps1" -Version "%VERSION%" -ProjectPath "%PROJECT%"
if %errorlevel% neq 0 (
    echo ОШИБКА обновления версии
    pause
    exit /b %errorlevel%
)

echo.
call "%ROOT%build-prod.bat" nopause
if %errorlevel% neq 0 (
    echo ОШИБКА prod-сборки
    pause
    exit /b %errorlevel%
)

echo.
echo Упаковка релиза...
powershell -NoProfile -ExecutionPolicy Bypass -File "%ROOT%scripts\package-release.ps1" -Version "%VERSION%" -PublishDir "%PUBLISH%" -ReleaseDir "%RELEASE_DIR%" -NotesFile "%NOTES_FILE%"
if %errorlevel% neq 0 (
    echo ОШИБКА упаковки релиза
    pause
    exit /b %errorlevel%
)

echo.
echo === РЕЛИЗ ГОТОВ ===
echo Папка: %RELEASE_DIR%
echo   customSTT.exe
echo   customSTT-%VERSION%-win-x64.zip
echo   RELEASE_NOTES.txt
echo   BUILD_INFO.txt
echo.
pause
