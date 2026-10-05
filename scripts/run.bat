@echo off
cd /d "%~dp0.."
echo Starting Smart Hadith Tree...

echo Starting Backend API...
start cmd /k "cd src\SmartHadithTree.Api && dotnet run"

:: Wait for 3 seconds
timeout /t 3 /nobreak >nul

echo Starting Next.js Frontend...
start cmd /k "cd frontend && npm run dev"

echo All services started!
echo The application should be available at: http://localhost:3000
