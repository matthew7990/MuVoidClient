@echo off
chcp 65001 >nul
REM Publica MuVoidClient/ en la rama 'client' (solo distribucion).
REM Ejecutar build-all.bat antes.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\deploy-client.ps1"
pause
