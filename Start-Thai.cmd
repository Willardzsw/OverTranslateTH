@echo off
setlocal
cd /d "%~dp0"
set "THAI_EXE=%~dp0src\OverTranslate\bin\Release\net8.0-windows10.0.26100.0\win-x64\TextTack.exe"
if not exist "%THAI_EXE%" (
  echo Build first: dotnet build src\OverTranslate\OverTranslate.csproj -c Release
  pause
  exit /b 1
)
start "TextTack" "%THAI_EXE%"
