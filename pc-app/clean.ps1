# Скрипт очистки временных файлов и артефактов сборки MobileAudio

$ErrorActionPreference = "SilentlyContinue"

$projectDir = "$PSScriptRoot\MobileAudio"

Write-Host "=== Очистка временных файлов ===" -ForegroundColor Cyan

# .NET / MSBuild
$paths = @(
    "$projectDir\bin"
    "$projectDir\obj"
    "$projectDir\publish"
    "$PSScriptRoot\MobileAudio-Setup.exe"
)

foreach ($path in $paths) {
    if (Test-Path $path) {
        Write-Host "Удаляется: $path" -ForegroundColor Yellow
        Remove-Item $path -Recurse -Force
    }
}

# Inno Setup: удалить скомпилированный setup
$compiledSetup = "$PSScriptRoot\Setup\Output"
if (Test-Path $compiledSetup) {
    Write-Host "Удаляется: $compiledSetup" -ForegroundColor Yellow
    Remove-Item $compiledSetup -Recurse -Force
}

Write-Host "=== Очистка завершена ===" -ForegroundColor Green
