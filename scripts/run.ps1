# Smart Hadith Tree - Development Runner Script
# This script starts both the .NET API and the Next.js Frontend in separate windows.

# Ensure we are running from the project root
Set-Location $PSScriptRoot\..

Write-Host "Starting Smart Hadith Tree..." -ForegroundColor Cyan

# 1. Start the .NET API
Write-Host "Starting Backend API (Port 5147)..." -ForegroundColor Green
Start-Process powershell -ArgumentList "-NoExit -Command `"cd src/SmartHadithTree.Api; dotnet run`"" -WindowStyle Normal

# Wait a couple of seconds to let the API start
Start-Sleep -Seconds 3

# 2. Start the Next.js Frontend
Write-Host "Starting Next.js Frontend (Port 3000)..." -ForegroundColor Green
Start-Process powershell -ArgumentList "-NoExit -Command `"cd frontend; npm run dev`"" -WindowStyle Normal

Write-Host "All services started!" -ForegroundColor Cyan
Write-Host "The application should be available at: http://localhost:3000" -ForegroundColor Yellow
