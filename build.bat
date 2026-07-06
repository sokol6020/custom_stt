@echo off
echo === Building customSTT ===
dotnet build "customSTT.sln" --configuration Debug
if %errorlevel% neq 0 (
    echo.
    echo === BUILD FAILED ===
    pause
    exit /b %errorlevel%
)
echo.
echo === BUILD SUCCESS ===
pause
