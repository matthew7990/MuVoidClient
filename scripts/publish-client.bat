@echo off
chcp 65001 >nul
cd /d "%~dp0.."

echo.
echo === MuVoid - Publicar Cliente (local) ===
echo.
echo Buscando compilado de MuMain...
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0publish-client.ps1"
if %ERRORLEVEL% neq 0 (
    echo.
    echo [ERROR] Fallo la publicacion.
    pause
    exit /b 1
)
pause
