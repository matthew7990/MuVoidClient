@echo off
cd /d "%~dp0"
echo Publicando OzViewer (exe autocontenido)...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -p:IncludeNativeLibrariesForSelfExtract=true -o publish
echo.
echo Listo. Ejecutable en: %~dp0publish\OzViewer.exe
explorer publish
pause
