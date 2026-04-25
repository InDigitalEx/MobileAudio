# Скрипт сборки установщика MobileAudioPC
# 1. Self-contained publish (не single-file)
# 2. Сборка MSI через WiX

$ErrorActionPreference = "Stop"

$projectDir = "$PSScriptRoot\MobileAudioPC"
$installerDir = "$PSScriptRoot\MobileAudioPC.Installer"
$publishDir = "$projectDir\bin\Release\net8.0-windows\win-x64\publish"
$msiOutput = "$installerDir\MobileAudioPC.msi"

Write-Host "=== Step 1: Self-contained publish ===" -ForegroundColor Cyan
dotnet publish "$projectDir\MobileAudioPC.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false

if ($LASTEXITCODE -ne 0) {
    throw "Publish failed"
}

Write-Host "=== Step 2: Build MSI ===" -ForegroundColor Cyan
Push-Location $installerDir
wix build Package.wxs Files.wxs -o MobileAudioPC.msi
Pop-Location

if ($LASTEXITCODE -ne 0) {
    throw "WiX build failed"
}

Write-Host "=== Done! ===" -ForegroundColor Green
Write-Host "MSI: $msiOutput" -ForegroundColor Yellow

