#requires -Version 7
<#
  Re-scores produced arm trees on D1-D4 and PERSISTS each per-tree report to
  runs/<tree>/quality.json — the machine-readable inputs `aggregate-scorecard.ps1` consumes.

  Why this exists: the original Stage-2Q scoring pass read the quality tool's console output and
  hand-aggregated it; the per-tree JSON was never kept. That made the published scorecard
  un-reproducible from artifacts. This script regenerates the inputs so the scorecard is derived, not
  transcribed.

  MUST run sequentially: every tree's quality run binds app :5121 + mock :8085. Two at once collide.

  Runtime: each tree does a full dotnet build + app/mock boot + D1 (10 checks) + D2 (22 drift cells)
  + D3/D4 static analysis. Budget a few minutes per tree.

  Usage:
    pwsh benchmark/harness/score-all.ps1                 # every arm tree, skip ones already scored
    pwsh benchmark/harness/score-all.ps1 -Force          # re-score everything
    pwsh benchmark/harness/score-all.ps1 -Trees scope22-armA,scope22-armB
    pwsh benchmark/harness/score-all.ps1 -DryRun         # print the plan, run nothing
#>
[CmdletBinding()]
param(
    [string[]]$Trees,                 # default: every *-arm{A,B} tree except the D5 extend runs
    [string]$Mode = 'all',            # all | dynamic | static | deep | drift | metrics | security
    [switch]$Force,                   # re-score trees that already have quality.json
    [switch]$DryRun
)
$ErrorActionPreference = 'Stop'

$Bench   = Split-Path -Parent $PSScriptRoot
$Quality = Join-Path $Bench 'quality\Quality.csproj'
$Mock    = Join-Path $Bench 'mock\MaxioMock.csproj'
$RunsDir = Join-Path $Bench 'runs'

# The arm build trees. `extend-*` are D5 artifacts (they end in -armA/-armB but are NOT arm builds),
# so they are filtered out — including them would double-count a tree under a different task.
if (-not $Trees) {
    $Trees = Get-ChildItem $RunsDir -Directory |
             Where-Object { $_.Name -match '-arm[AB]$' -and $_.Name -notlike 'extend-*' } |
             Select-Object -ExpandProperty Name | Sort-Object
}

Write-Host "== score-all: $($Trees.Count) tree(s), mode=$Mode ==" -ForegroundColor Cyan
Write-Host "   sequential by necessity (each run binds :5121 + :8085)" -ForegroundColor DarkGray

if ($DryRun) {
    foreach ($t in $Trees) {
        $exists = Test-Path (Join-Path $RunsDir "$t\quality.json")
        $action = if ($exists -and -not $Force) { 'SKIP (already scored)' } else { 'score' }
        Write-Host ("  {0,-22} {1}" -f $t, $action)
    }
    return
}

Write-Host 'building quality tool + mock ...'
dotnet build $Quality -v quiet | Out-Null
dotnet build $Mock    -v quiet | Out-Null

$ok = @(); $failed = @(); $skipped = @()
foreach ($t in $Trees) {
    $ws  = Join-Path $RunsDir "$t\workspace"
    $out = Join-Path $RunsDir "$t\quality.json"

    if (-not (Test-Path $ws)) { Write-Warning "$t — workspace missing, skipping"; $failed += $t; continue }
    if ((Test-Path $out) -and -not $Force) { Write-Host "  $t — already scored (use -Force to redo)" -ForegroundColor DarkGray; $skipped += $t; continue }

    Write-Host "→ scoring $t ..." -ForegroundColor Magenta
    $sw = [Diagnostics.Stopwatch]::StartNew()
    dotnet run --project $Quality --no-build -- --tree $ws --mode $Mode --label $t --out $out 2>&1 |
        Tee-Object -FilePath (Join-Path $RunsDir "$t\quality-run.log") | Out-Null
    $sw.Stop()

    # exit 0 = scored; 2 = build/boot failure; anything else = unexpected
    if ($LASTEXITCODE -eq 0 -and (Test-Path $out)) {
        Write-Host ("  ok  {0}  ({1:N0}s)" -f $t, $sw.Elapsed.TotalSeconds) -ForegroundColor Green
        $ok += $t
    } else {
        Write-Host "  FAIL $t (exit $LASTEXITCODE) — see runs/$t/quality-run.log" -ForegroundColor Red
        $failed += $t
    }
    $global:LASTEXITCODE = 0
}

Write-Host ''
Write-Host "scored $($ok.Count) · skipped $($skipped.Count) · failed $($failed.Count)" -ForegroundColor Cyan
if ($failed) { Write-Host "failed: $($failed -join ', ')" -ForegroundColor Red }
Write-Host 'next: pwsh benchmark/harness/aggregate-scorecard.ps1'
