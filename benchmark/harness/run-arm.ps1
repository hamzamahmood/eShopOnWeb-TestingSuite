#requires -Version 7
<#
  Runs one benchmark trial for an arm. Sets up an isolated workspace (clean copy of the pinned
  eShopOnWeb baseline), composes the per-arm prompt (only the Maxio-reference block differs),
  places the arm material (Arm A: maxio-sdk plugin via --plugin-dir; Arm B: the OpenAPI spec in the
  tree), drops an arm-facing gate wrapper, launches the agent headless, captures tokens, then runs
  the gate for DONE (public) and ROBUST (holdout) and writes a manifest.

  -DryRun sets everything up and prints the exact claude command WITHOUT launching the agent
  (spends no tokens) — use it to validate the harness before the pilot.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('A','B')][string]$Arm,
    [string]$RunId    = (Get-Date -Format 'yyyyMMdd-HHmmss'),
    [string]$Model        = '',   # pin at pilot; empty => CLI default
    [string]$Effort       = '',   # low|medium|high|xhigh|max; empty => CLI default
    [double]$MaxBudgetUsd = 0,    # safety backstop via --max-budget-usd; 0 => no cap
    [switch]$DryRun
)
$ErrorActionPreference = 'Stop'

$Repo     = 'C:\repos\eShopOnWeb-TestingSuite'
$Bench    = Join-Path $Repo 'benchmark'
$Baseline = Join-Path $Repo 'eShopOnWeb'
$SpecDir  = Join-Path $Repo 'openAPI'
$Gate     = Join-Path $Bench 'gate\Gate.csproj'
$Mock     = Join-Path $Bench 'mock\MaxioMock.csproj'
$Plugin   = 'C:\repos\v4-plugins\plugins\maxio-sdk'
$RunDir   = Join-Path $Bench "runs\$RunId-arm$Arm"
$Ws       = Join-Path $RunDir 'workspace'
$PubApi   = Join-Path $Ws 'src\PublicApi\PublicApi.csproj'

Write-Host "== run-arm: Arm $Arm  RunId $RunId ==" -ForegroundColor Cyan

# 1) isolated workspace = clean copy of the pinned baseline (no build artifacts, no .git)
New-Item -ItemType Directory -Force $RunDir | Out-Null
New-Item -ItemType Directory -Force $Ws | Out-Null
robocopy $Baseline $Ws /MIR /XD bin obj .vs .git /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy failed ($LASTEXITCODE)" }
$global:LASTEXITCODE = 0

# 2) arm material (+ spec placement for Arm B)
$armMaterial = if ($Arm -eq 'A') {
@'
The Maxio Advanced Billing SDK is available via the `maxio-sdk` plugin (its skills are loaded in this
session). Use it — it guides installing the NuGet package, navigating the SDK source, authentication,
calling endpoints, models, error handling, and resilience.
'@
} else {
@'
The Maxio Advanced Billing OpenAPI specification is provided in this repository at
`./maxio-openapi/openapi.yaml` (with referenced components under `./maxio-openapi/components/`).
'@
}
if ($Arm -eq 'B') {
    $specDst = Join-Path $Ws 'maxio-openapi'
    New-Item -ItemType Directory -Force $specDst | Out-Null
    Copy-Item (Join-Path $SpecDir 'openapi.yaml') $specDst          # APIMATIC-META.json deliberately excluded
    Copy-Item (Join-Path $SpecDir 'components')  $specDst -Recurse
}

# 3) compose the prompt (only ARM_MATERIAL differs between arms)
$prompt = (Get-Content (Join-Path $Bench 'harness\prompt.md') -Raw).Replace('{{ARM_MATERIAL}}', $armMaterial.Trim())
Set-Content (Join-Path $RunDir 'prompt.txt') $prompt -NoNewline

# 4) arm-facing gate wrapper — the agent runs `.\gate.cmd`; it cannot read the gate's source
$gateCmd = "@echo off`r`ndotnet run --project `"$Gate`" --no-build -- --app-project `"$PubApi`" --mock-project `"$Mock`" --mode public %*`r`n"
Set-Content (Join-Path $Ws 'gate.cmd') $gateCmd -NoNewline

# 5) plugin toggle + composed claude args
$pluginArgs = if ($Arm -eq 'A') { @('--plugin-dir', $Plugin) } else { @() }
$claudeArgs = @('-p', $prompt) + $pluginArgs +
              @('--output-format','json','--dangerously-skip-permissions')
if ($Model)              { $claudeArgs += @('--model', $Model) }
if ($Effort)             { $claudeArgs += @('--effort', $Effort) }
if ($MaxBudgetUsd -gt 0) { $claudeArgs += @('--max-budget-usd', "$MaxBudgetUsd") }

if ($DryRun) {
    Write-Host "DRY RUN — no agent launched, no tokens spent" -ForegroundColor Yellow
    Write-Host "  workspace : $Ws"
    Write-Host "  prompt    : $(Join-Path $RunDir 'prompt.txt')  ($($prompt.Length) chars)"
    Write-Host "  gate cmd  : $(Join-Path $Ws 'gate.cmd')"
    if ($Arm -eq 'B') { Write-Host "  spec      : $(Join-Path $Ws 'maxio-openapi\openapi.yaml')" }
    Write-Host "  plugin    : $(if ($Arm -eq 'A') { $Plugin } else { '(disabled)' })"
    $shown = ($claudeArgs | ForEach-Object { if ($_ -eq $prompt) { '"<prompt.txt>"' } else { $_ } }) -join ' '
    Write-Host "  command   : claude $shown"
    return
}

# 6) prebuild gate + mock so the agent's `.\gate.cmd` (--no-build) is fast
Write-Host "building gate + mock ..."
dotnet build $Gate -v quiet | Out-Null
dotnet build $Mock -v quiet | Out-Null

# 7) launch the agent headless, cwd = workspace, with an ISOLATED per-run config dir.
#    Seeding a fresh CLAUDE_CONFIG_DIR with only the credentials authenticates the child WITHOUT
#    pulling in the globally-enabled plugins (maxio-sdk/skill-creator/frontend-design) or any global
#    CLAUDE.md — the isolation --bare would give, but --bare also strips auth. Arm A re-adds only the
#    maxio-sdk plugin via --plugin-dir; Arm B gets none.
Write-Host "launching agent ..." -ForegroundColor Magenta
$cfgDir = Join-Path $RunDir 'claude-config'
New-Item -ItemType Directory -Force $cfgDir | Out-Null
Copy-Item (Join-Path $env:USERPROFILE '.claude\.credentials.json') $cfgDir -Force
$env:CLAUDE_CONFIG_DIR = $cfgDir
$errLog = Join-Path $RunDir 'claude-stderr.log'
Push-Location $Ws
try { $result = & claude @claudeArgs 2>$errLog | Out-String }   # stderr separated so warnings can't pollute the JSON
finally { Pop-Location; Remove-Item Env:\CLAUDE_CONFIG_DIR -ErrorAction SilentlyContinue }
Set-Content (Join-Path $RunDir 'claude-result.json') $result -NoNewline

# 8) parse tokens (robust: locate the result object even if any stray text precedes it)
$usage=$null; $cost=$null; $turns=$null; $sid=$null; $apiError=$null
try {
    $k = $result.IndexOf('{"type":"result"')
    $j = ($(if ($k -ge 0) { $result.Substring($k) } else { $result })) | ConvertFrom-Json
    $usage=$j.usage; $cost=$j.total_cost_usd; $turns=$j.num_turns; $sid=$j.session_id; $apiError=$j.is_error
} catch { Write-Warning "could not parse claude JSON result (see claude-result.json / claude-stderr.log)" }

# 9) DONE (public) then ROBUST (holdout) — experimenter-verified on the produced tree
function Invoke-Gate([string]$mode) {
    dotnet run --project $Gate --no-build -- --app-project $PubApi --mock-project $Mock --mode $mode 2>&1 |
        Tee-Object -FilePath (Join-Path $RunDir "gate-$mode.txt") | Out-Null
    return ($LASTEXITCODE -eq 0)
}
$done   = Invoke-Gate 'public'
$robust = $done -and (Invoke-Gate 'holdout')

# 10) manifest
[ordered]@{
    runId=$RunId; arm=$Arm; model=$Model; effort=$Effort; maxBudgetUsd=$MaxBudgetUsd; sessionId=$sid
    tokens=$usage; totalCostUsd=$cost; numTurns=$turns; apiError=$apiError
    done=$done; robust=$robust; workspace=$Ws
} | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $RunDir 'manifest.json')

Write-Host "DONE=$done  ROBUST=$robust  apiError=$apiError  cost=`$$cost  turns=$turns" -ForegroundColor Green
Write-Host "manifest: $(Join-Path $RunDir 'manifest.json')"
