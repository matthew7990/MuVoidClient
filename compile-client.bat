@echo off
setlocal enabledelayedexpansion
chcp 65001 >nul
REM Compila el cliente MuMain (Mu Online)
echo Cerrando instancias previas si existen...
taskkill /f /im Main.exe 2>nul
echo Compilando cliente MuMain...

cd /d "%~dp0"

REM Aplicar Interface 99b si existen carpetas Interface/Custom (opcional)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\apply-interface-99.ps1"

cd /d "%~dp0Source"

REM Usar generador Visual Studio - evita el bloqueo en "Detecting C compiler ABI info"
REM No requiere vcvarsall: VS generator encuentra todo desde el registro
set "BUILD_DIR=out\build\vs-x86"
set "EXE_PATH="

REM Probar generadores (VS 2026, 2025, 2022, 2019) - usa el mas reciente instalado
set "GENERATOR="
if exist "C:\Program Files\Microsoft Visual Studio\2026\Community\MSBuild\Current\Bin\MSBuild.exe" set "GENERATOR=Visual Studio 18 2026"
if exist "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" if "%GENERATOR%"=="" set "GENERATOR=Visual Studio 18 2026"
if exist "C:\Program Files\Microsoft Visual Studio\17\Community\MSBuild\Current\Bin\MSBuild.exe" if "%GENERATOR%"=="" set "GENERATOR=Visual Studio 17 2022"
if exist "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" if "%GENERATOR%"=="" set "GENERATOR=Visual Studio 17 2022"
if exist "C:\Program Files\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe" if "%GENERATOR%"=="" set "GENERATOR=Visual Studio 17 2022"
if exist "C:\Program Files\Microsoft Visual Studio\16\Community\MSBuild\Current\Bin\MSBuild.exe" if "%GENERATOR%"=="" set "GENERATOR=Visual Studio 16 2019"

if "%GENERATOR%"=="" (
  echo ERROR: Visual Studio no encontrado. Instala VS 2022+ con workload "Desarrollo para C++"
  if "%1" neq "--no-pause" pause
  exit /b 1
)

REM Configurar si no existe el cache (incremental: omitir en builds siguientes)
if not exist "%BUILD_DIR%\CMakeCache.txt" (
  echo Configurando con %GENERATOR% -A Win32...
  cmake -B "%BUILD_DIR%" -G "%GENERATOR%" -A Win32 -DENABLE_EDITOR=OFF
) else (
  REM Re-configurar para asegurar que los cambios en CMakeLists.txt se apliquen y forzar editor desactivado
  echo Re-configurando...
  cmake -B "%BUILD_DIR%" -DENABLE_EDITOR=OFF
)
if errorlevel 1 (
  echo ERROR en configuracion.
  if "%1" neq "--no-pause" pause
  exit /b 1
)


REM Incremental: solo recompila archivos modificados.
REM --parallel y /maxcpucount usan todos los núcleos del procesador.
REM /MP en CMakeLists paraleliza .cpp dentro del mismo proyecto.
echo Compilando Release con %NUMBER_OF_PROCESSORS% núcleos...
cmake --build "%BUILD_DIR%" --config Release --parallel %NUMBER_OF_PROCESSORS% -- /maxcpucount:%NUMBER_OF_PROCESSORS%
if errorlevel 1 (
  echo ERROR en compilacion.
  if "%1" neq "--no-pause" pause
  exit /b 1
)

REM Buscar Main.exe (VS puede ponerlo en Release o src/Release)
if exist "%BUILD_DIR%\src\Release\Main.exe" set "EXE_PATH=%BUILD_DIR%\src\Release\Main.exe"
if exist "%BUILD_DIR%\Release\Main.exe" if "%EXE_PATH%"=="" set "EXE_PATH=%BUILD_DIR%\Release\Main.exe"
if exist "%BUILD_DIR%\src\Main\Release\Main.exe" if "%EXE_PATH%"=="" set "EXE_PATH=%BUILD_DIR%\src\Main\Release\Main.exe"

REM Generar version manifest del cliente (para que el launcher detecte el compilado)
if not "%EXE_PATH%"=="" (
    echo.
    echo Generando version manifest...
    set "FULL_EXE_PATH=%~dp0Source\%EXE_PATH%"
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\generate-client-manifest.ps1" "!FULL_EXE_PATH!"
    if errorlevel 1 echo [ADVERTENCIA] No se pudo generar el version manifest.

    REM Copiar automáticamente al repositorio de Release si existe
    set "RELEASE_DIR=%~dp0..\MuVoidClient-Release\MuVoidClient"
    if exist "%~dp0..\MuVoidClient-Release" (
        echo Sincronizando con repositorio de Release...
        if not exist "!RELEASE_DIR!" mkdir "!RELEASE_DIR!"
        
        REM Obtener directorio de compilación
        for %%I in ("!FULL_EXE_PATH!") do set "BUILD_OUT_DIR=%%~dpI"
        
        REM Sincronización completa (mirror: copia todo y elimina obsoletos en destino)
        robocopy "!BUILD_OUT_DIR!." "!RELEASE_DIR!." /MIR /NJH /NJS /NDL /NC /NS /NP
        echo [OK] Cliente sincronizado en: !RELEASE_DIR!
    ) else (
        echo [INFO] No se encontro MuVoidClient-Release, saltando sincronizacion.
    )
) else (
    echo [ADVERTENCIA] No se encontro Main.exe, saltando generacion de manifest e instalacion.
)

echo.
echo Compilacion exitosa.
if "%1"=="--no-pause" exit /b 0
pause
