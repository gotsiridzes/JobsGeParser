@echo off

start "backend" cmd /k "dotnet run --project "%~dp0backend\JobsGeParser\JobsGeParser.csproj""

timeout /t 2 /nobreak >nul

start "frontend" cmd /k "cd /d "%~dp0frontend" && npm run dev"

exit