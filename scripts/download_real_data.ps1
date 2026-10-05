$ErrorActionPreference = "Stop"
$url = "https://raw.githubusercontent.com/fawazahmed0/hadith-api/1/editions/ara-bukhari.json"
$outPath = Join-Path $PSScriptRoot "..\data\ara-bukhari.json"

Write-Host "Downloading Sahih al-Bukhari from fawazahmed0/hadith-api..."
Invoke-WebRequest -Uri $url -OutFile $outPath
Write-Host "Download complete: $outPath"
