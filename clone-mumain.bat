@echo off
chcp 65001 >nul
REM Descarga MuMain fresco desde el repo de Sven (https://github.com/sven-n/MuMain)
echo Descargando MuMain de Sven...
cd /d "%~dp0"

if exist "MuMain\.git" (
  echo La carpeta MuMain ya existe. Eliminando para clonar fresco...
  rmdir /s /q MuMain 2>nul
  if exist MuMain (
    echo ERROR: No se pudo eliminar MuMain. Cierra programas que la usen e intenta de nuevo.
    pause
    exit /b 1
  )
)

git clone --recurse-submodules https://github.com/sven-n/MuMain.git MuMain
if errorlevel 1 (
  echo ERROR en el clone. Verifica conexión a internet.
  pause
  exit /b 1
)

echo.
echo MuMain descargado correctamente. Ejecuta compile-client.bat para compilar.
pause
