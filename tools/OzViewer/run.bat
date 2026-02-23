@echo off
cd /d "%~dp0"
dotnet run --project OzViewer.csproj
if errorlevel 1 pause
