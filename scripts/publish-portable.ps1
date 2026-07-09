#requires -Version 5
<#
.SYNOPSIS
  Publishes a self-contained, portable BudsMonitor build and zips it for distribution.

.DESCRIPTION
  Produces a folder that runs on a clean Windows 10 1809+/11 machine with no .NET
  install required (self-contained). Output goes to dist\ (git-ignored). Run from
  anywhere; paths are resolved relative to the repo root.

.EXAMPLE
  pwsh -File scripts\publish-portable.ps1
  pwsh -File scripts\publish-portable.ps1 -Configuration Release -Runtime win-x64
#>
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repo "src\BudsMonitor.App\BudsMonitor.App.csproj"
$publishDir = Join-Path $repo "dist\BudsMonitor-$Runtime"
$zipPath = Join-Path $repo "dist\BudsMonitor-portable-$Runtime.zip"

Write-Host "Publishing self-contained build ($Configuration / $Runtime)..." -ForegroundColor Cyan
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

dotnet publish $project `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)" }

$exe = Join-Path $publishDir "BudsMonitor.App.exe"
if (-not (Test-Path $exe)) { throw "expected executable not found: $exe" }

if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Write-Host "Zipping portable bundle..." -ForegroundColor Cyan
Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zipPath

$zipMb = [Math]::Round((Get-Item $zipPath).Length / 1MB, 1)
Write-Host ""
Write-Host "Portable build ready:" -ForegroundColor Green
Write-Host "  folder : $publishDir"
Write-Host "  exe    : $exe"
Write-Host "  zip    : $zipPath ($zipMb MB)"
Write-Host ""
Write-Host "Unzip anywhere and run BudsMonitor.App.exe. No .NET install required."
