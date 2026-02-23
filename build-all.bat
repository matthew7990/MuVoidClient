@echo off
chcp 65001 >nul
REM Compila launcher + cliente MuMain y empaqueta todo en MuVoidClient/
setlocal
set "ROOT=%~dp0"
set "OUT=%ROOT%MuVoidClient"

echo.
echo ═══════════════════════════════════════════════════════════
echo   MuVoidClient - Build completo (launcher + cliente)
echo ═══════════════════════════════════════════════════════════
echo.

REM ── 1. Compilar cliente Source ────────────────────────────────────────────
echo [1/4] Compilando cliente Source (MuMain HQ)...
call "%ROOT%compile-client.bat" --no-pause
if errorlevel 1 (
  echo ERROR: Fallo compilacion del cliente.
  exit /b 1
)

REM ── 2. Usar directorio que generate-client-manifest escribio (ruta exacta donde compilo)
set "CLIENT_DIR="
if exist "%ROOT%.client-build-dir" (
  set /p "CLIENT_DIR=" < "%ROOT%.client-build-dir"
)
if not exist "%CLIENT_DIR%Main.exe" set "CLIENT_DIR="

if "%CLIENT_DIR%"=="" (
  echo ERROR: No se encontro Main.exe. Ejecuta compile-client.bat primero.
  exit /b 1
)

REM ── 3. Compilar launcher Tauri ──────────────────────────────────────────────
echo.
echo [2/4] Compilando launcher Tauri...
cd /d "%ROOT%launcher"
call npm run tauri build
if errorlevel 1 (
  echo ERROR: Fallo compilacion del launcher.
  exit /b 1
)
cd /d "%ROOT%"

REM ── 4. Crear carpeta MuVoidClient y copiar todo ─────────────────────────────
echo.
echo [3/4] Empaquetando en MuVoidClient/...

if exist "%OUT%" rmdir /s /q "%OUT%"
mkdir "%OUT%"

REM Copiar cliente (Main.exe, DLLs, version.json, etc.)
xcopy "%CLIENT_DIR%\*" "%OUT%\" /E /I /Y /Q >nul

REM Copiar launcher (buscar .exe en target/release)
set "LAUNCHER_EXE="
if exist "%ROOT%launcher\src-tauri\target\release\muvoid-launcher.exe" set "LAUNCHER_EXE=%ROOT%launcher\src-tauri\target\release\muvoid-launcher.exe"
if exist "%ROOT%launcher\src-tauri\target\release\MuVoid Launcher.exe" if "%LAUNCHER_EXE%"=="" set "LAUNCHER_EXE=%ROOT%launcher\src-tauri\target\release\MuVoid Launcher.exe"
if exist "%ROOT%launcher\src-tauri\target\release\*.exe" if "%LAUNCHER_EXE%"=="" (
  for %%F in ("%ROOT%launcher\src-tauri\target\release\*.exe") do set "LAUNCHER_EXE=%%F"
)

if not "%LAUNCHER_EXE%"=="" (
  copy /Y "%LAUNCHER_EXE%" "%OUT%\MuVoid Launcher.exe" >nul
  echo   - Launcher copiado
) else (
  echo [ADVERTENCIA] No se encontro ejecutable del launcher.
)

echo   - Cliente copiado (%CLIENT_DIR%)
echo   - version.json incluido

REM Copiar CLIENT_VERSION y CLIENT_CHANGELOG para referencia
if exist "%ROOT%CLIENT_VERSION" copy /Y "%ROOT%CLIENT_VERSION" "%OUT%\" >nul
if exist "%ROOT%CLIENT_CHANGELOG.md" copy /Y "%ROOT%CLIENT_CHANGELOG.md" "%OUT%\" >nul

echo.
echo [4/4] Listo.
echo.
echo ═══════════════════════════════════════════════════════════
echo   MuVoidClient/ creado correctamente.
echo   Para publicar en la rama client: deploy-client.bat
echo ═══════════════════════════════════════════════════════════
echo.
pause
