<#
.SYNOPSIS
    Builds MaxioApiTests in Release and copies the output DLLs into ./build.
#>

$ErrorActionPreference = 'Stop'

$scriptDir = $PSScriptRoot
$csproj = Join-Path $scriptDir 'MaxioApiTests.csproj'
$buildDir = Join-Path $scriptDir 'build'

if (Test-Path $buildDir) {
    Remove-Item -Recurse -Force -Path (Join-Path $buildDir '*') -ErrorAction SilentlyContinue
} else {
    New-Item -ItemType Directory -Path $buildDir | Out-Null
}

# Suppress pdb generation — the build output ships DLLs only, no debug symbols.
dotnet build $csproj -c Release -o $buildDir /p:DebugType=none /p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE"
}

Write-Host "Build output copied to $buildDir"
