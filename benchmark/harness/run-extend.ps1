#requires -Version 7
<#
  D5 modifiability / human-dev-speed. Measures agent-effort-to-EXTEND an already-produced integration:
  start from a completed arm tree, ask a fresh agent to add ONE new endpoint that composes existing
  operations, and measure tokens/turns to make the extend-check green. Same token rig + isolation as
  run-arm.ps1. The extension is a composite summary endpoint (subscription state + its invoices) that
  reuses ops both arms already built, so it needs no mock changes and is verified deterministically.

  -DryRun sets everything up and prints the command WITHOUT launching the agent (no tokens).
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('A','B')][string]$Arm,
    [Parameter(Mandatory)][string]$SourceRun,     # e.g. scope22-armA  (a produced run under benchmark/runs)
    [string]$RunId  = (Get-Date -Format 'yyyyMMdd-HHmmss'),
    [string]$Model  = 'claude-opus-4-8',
    [string]$Effort = 'high',
    [double]$MaxBudgetUsd = 8,                     # safety backstop — extend is small; cap low
    [switch]$DryRun
)
$ErrorActionPreference = 'Stop'
$Repo    = 'C:\repos\eShopOnWeb-TestingSuite'
$Bench   = Join-Path $Repo 'benchmark'
$Src     = Join-Path $Bench "runs\$SourceRun\workspace"
$Quality = Join-Path $Bench 'quality\Quality.csproj'
$Mock    = Join-Path $Bench 'mock\MaxioMock.csproj'
$Plugin  = 'C:\repos\v4-plugins\plugins\maxio-sdk'
$RunDir  = Join-Path $Bench "runs\extend-$RunId-arm$Arm"
$Ws      = Join-Path $RunDir 'workspace'
$PubApi  = Join-Path $Ws 'src\PublicApi\PublicApi.csproj'

if (-not (Test-Path $Src)) { throw "source workspace not found: $Src" }
Write-Host "== run-extend: Arm $Arm  from $SourceRun  RunId $RunId ==" -ForegroundColor Cyan

# 1) isolated workspace = copy of the produced tree (drop build artifacts)
New-Item -ItemType Directory -Force $Ws | Out-Null
robocopy $Src $Ws /MIR /XD bin obj .vs .git /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy failed ($LASTEXITCODE)" }
$global:LASTEXITCODE = 0

# 2) arm material (Arm A re-adds the plugin; Arm B's spec is already in the copied tree at ./maxio-openapi)
$armMaterial = if ($Arm -eq 'A') {
@'
The Maxio Advanced Billing SDK is available via the `maxio-sdk` plugin (its skills are loaded) and is already
installed as the NuGet package `AsadAli.AdvancedBilling.Sdk` in this solution. Use it as you did originally.
'@
} else {
@'
The Maxio Advanced Billing OpenAPI specification is in your working tree at `./maxio-openapi/openapi.yaml`
(components under `./maxio-openapi/components/`), as in the original build.
'@
}

# 3) extend prompt — add ONE composite endpoint reusing existing operations
$prompt = @"
This application already has a working Maxio billing integration under /api/billing (you built it).
Add exactly ONE new endpoint, reusing your existing provider client/service code:

    GET /api/billing/subscriptions/{subscriptionId}/summary

It must return a combined summary for the subscription: the subscription's state and plan, PLUS the
list of that subscription's invoices (each invoice's uid/number, total, and status). Implement it by
calling the provider operations you already use for reading a subscription and for listing a
subscription's invoices — do not hardcode any values; the endpoint must genuinely call the provider.

Keep the same code style, layering, error handling, and resilience as the rest of your integration.

MAXIO API REFERENCE
$($armMaterial.Trim())

DEFINITION OF DONE
Run the extend gate and iterate until it passes:
    .\gate.cmd
It reports pass/fail. You cannot read its source. Do not modify anything under benchmark/.
"@
New-Item -ItemType Directory -Force $RunDir | Out-Null
Set-Content (Join-Path $RunDir 'prompt.txt') $prompt -NoNewline

# 4) arm-facing extend gate (the quality tool in extendcheck mode)
$gateCmd = "@echo off`r`ndotnet run --project `"$Quality`" --no-build -- --app-project `"$PubApi`" --mock-project `"$Mock`" --mode extendcheck %*`r`n"
Set-Content (Join-Path $Ws 'gate.cmd') $gateCmd -NoNewline

$pluginArgs = if ($Arm -eq 'A') { @('--plugin-dir', $Plugin) } else { @() }
$claudeArgs = @('-p', $prompt) + $pluginArgs + @('--output-format','json','--dangerously-skip-permissions')
if ($Model)  { $claudeArgs += @('--model', $Model) }
if ($Effort) { $claudeArgs += @('--effort', $Effort) }
if ($MaxBudgetUsd -gt 0) { $claudeArgs += @('--max-budget-usd', "$MaxBudgetUsd") }

if ($DryRun) {
    Write-Host "DRY RUN — no agent launched" -ForegroundColor Yellow
    Write-Host "  workspace : $Ws"
    Write-Host "  plugin    : $(if ($Arm -eq 'A') { $Plugin } else { '(disabled)' })"
    Write-Host "  prompt    : $((Join-Path $RunDir 'prompt.txt'))  ($($prompt.Length) chars)"
    return
}

Write-Host "building quality tool + mock ..."
dotnet build $Quality -v quiet | Out-Null
dotnet build $Mock -v quiet | Out-Null

Write-Host "launching agent ..." -ForegroundColor Magenta
$cfgDir = Join-Path $RunDir 'claude-config'
New-Item -ItemType Directory -Force $cfgDir | Out-Null
Copy-Item (Join-Path $env:USERPROFILE '.claude\.credentials.json') $cfgDir -Force
$env:CLAUDE_CONFIG_DIR = $cfgDir
$errLog = Join-Path $RunDir 'claude-stderr.log'
Push-Location $Ws
try { $result = & claude @claudeArgs 2>$errLog | Out-String }
finally { Pop-Location; Remove-Item Env:\CLAUDE_CONFIG_DIR -ErrorAction SilentlyContinue }
Set-Content (Join-Path $RunDir 'claude-result.json') $result -NoNewline

$usage=$null; $cost=$null; $turns=$null; $apiError=$null
try {
    $k = $result.IndexOf('{"type":"result"')
    $j = ($(if ($k -ge 0) { $result.Substring($k) } else { $result })) | ConvertFrom-Json
    $usage=$j.usage; $cost=$j.total_cost_usd; $turns=$j.num_turns; $apiError=$j.is_error
} catch { Write-Warning "could not parse claude JSON result" }

dotnet run --project $Quality --no-build -- --app-project $PubApi --mock-project $Mock --mode extendcheck 2>&1 |
    Tee-Object -FilePath (Join-Path $RunDir 'extendcheck.txt') | Out-Null
$extended = ($LASTEXITCODE -eq 0)

[ordered]@{
    runId=$RunId; arm=$Arm; sourceRun=$SourceRun; model=$Model; effort=$Effort
    tokens=$usage; totalCostUsd=$cost; numTurns=$turns; apiError=$apiError; extended=$extended; workspace=$Ws
} | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $RunDir 'manifest.json')
Write-Host "EXTENDED=$extended  apiError=$apiError  cost=`$$cost  turns=$turns" -ForegroundColor Green
