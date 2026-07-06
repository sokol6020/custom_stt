@echo off
chcp 65001 >nul
echo === customSTT: сборка Debug ===
dotnet build "customSTT.sln" --configuration Debug
if %errorlevel% neq 0 (
    echo.
    echo === ОШИБКА СБОРКИ ===
    pause
    exit /b %errorlevel%
)
echo.
echo === СБОРКА УСПЕШНА ===
pause
