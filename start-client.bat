@echo off
REM Inicia el cliente MuMain (Mu Online)
REM Conecta a localhost puerto 44406 (puerto para el cliente open source)
cd /d "%~dp0MuMain"

REM Buscar Main.exe (Ninja: windows-x86, VS: vs-x86)
set "EXE="
if exist "out\build\windows-x86\src\Release\Main.exe" set "EXE=out\build\windows-x86\src\Release\Main.exe"
if exist "out\build\windows-x86\src\Debug\Main.exe" if "%EXE%"=="" set "EXE=out\build\windows-x86\src\Debug\Main.exe"
if exist "out\build\vs-x86\src\Release\Main.exe" if "%EXE%"=="" set "EXE=out\build\vs-x86\src\Release\Main.exe"
if exist "out\build\vs-x86\Release\Main.exe" if "%EXE%"=="" set "EXE=out\build\vs-x86\Release\Main.exe"
if exist "out\build\vs-x86\src\Main\Release\Main.exe" if "%EXE%"=="" set "EXE=out\build\vs-x86\src\Main\Release\Main.exe"
if "%EXE%"=="" (
    echo Error: No se encontro Main.exe. Ejecuta compile-client.bat primero.
    pause
    exit /b 1
)

REM 127.127.127.127 = local (evita bloqueo del cliente a 127.0.0.1)
REM Para otros usuarios en la red: usar IP privada del servidor (ej: /u192.168.1.100)
REM Puerto 44406 = cliente open source (MuMain)
echo Conectando a servidor local (127.127.127.127:44406)...
start "" "%EXE%" connect /u127.127.127.127 /p44406
