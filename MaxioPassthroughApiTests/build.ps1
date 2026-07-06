<#
.SYNOPSIS
    Builds MaxioPassthroughApiTests in Release and copies the output DLLs into ./build.
#>

$ErrorActionPreference = 'Stop'

$scriptDir = $PSScriptRoot
$csproj = Join-Path $scriptDir 'MaxioPassthroughApiTests.csproj'
$buildDir = Join-Path $scriptDir 'build'

if (Test-Path $buildDir) {
    Remove-Item -Recurse -Force -Path (Join-Path $buildDir '*') -ErrorAction SilentlyContinue
} else {
    New-Item -ItemType Directory -Path $buildDir | Out-Null
}

dotnet build $csproj -c Release -o $buildDir
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE"
}

Write-Host "Build output copied to $buildDir"
