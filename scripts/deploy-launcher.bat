@echo off
chcp 65001 >nul
cd /d "%~dp0.."

echo.
echo === MuVoid - Deploy a Release (launcher + cliente) ===
echo Ejecuta compile-client.bat antes si actualizaste el cliente.
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0deploy-launcher.ps1"
if %ERRORLEVEL% neq 0 (
    echo.
    echo [ERROR] Fallo el deploy.
    pause
    exit /b 1
)
pause
