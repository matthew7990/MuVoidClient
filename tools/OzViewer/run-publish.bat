@echo off
cd /d "%~dp0"
if not exist publish\OzViewer.exe (
    echo Ejecutando publish.bat primero...
    call publish.bat
)
echo Ejecutando OzViewer...
start "" publish\OzViewer.exe
timeout /t 2 /nobreak >nul
echo.
echo Log deberia estar en: publish\OzViewer.log
if exist publish\OzViewer.log (
    echo Abriendo log...
    notepad publish\OzViewer.log
) else (
    echo No se encontro log. El exe puede extraerse a temp - busca OzViewer.log en %%TEMP%%
    pause
)
