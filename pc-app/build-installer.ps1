# Скрипт сборки установщика MobileAudio
# 1. Self-contained publish (не single-file)
# 2. Сборка установщика через Inno Setup

$ErrorActionPreference = "Stop"

$projectDir = "$PSScriptRoot\MobileAudio"
$setupDir = "$PSScriptRoot\Setup"
$setupOutput = "$PSScriptRoot\MobileAudio-Setup.exe"

Write-Host "=== Step 1: Self-contained publish ===" -ForegroundColor Cyan
dotnet publish "$projectDir\MobileAudio.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false

if ($LASTEXITCODE -ne 0) {
    throw "Publish failed"
}

Write-Host "=== Step 2: Build installer with Inno Setup ===" -ForegroundColor Cyan
$iscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $iscc)) {
    $iscc = "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
}
if (-not (Test-Path $iscc)) {
    throw "Inno Setup compiler (ISCC.exe) not found. Please install Inno Setup 6 from https://jrsoftware.org/isdl.php"
}

& $iscc "$setupDir\setup.iss"

if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup build failed"
}

Write-Host "=== Done! ===" -ForegroundColor Green
Write-Host "Installer: $setupOutput" -ForegroundColor Yellow
