@echo off
cd /d "%~dp0"
echo Publicando MuVoidConfigEditor...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -o publish
echo.
echo Listo. Ejecutable en: %~dp0publish\MuVoidConfigEditor.exe
explorer publish
pause
