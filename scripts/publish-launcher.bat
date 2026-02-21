@echo off
chcp 65001 >nul
cd /d "%~dp0.."

echo.
echo === MuVoid - Publicar Launcher (local) ===
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0publish-launcher.ps1"
if %ERRORLEVEL% neq 0 (
    echo.
    echo [ERROR] Fallo la publicacion.
    pause
    exit /b 1
)
pause
