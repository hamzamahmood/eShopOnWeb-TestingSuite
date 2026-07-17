#!/usr/bin/env pwsh
# run.ps1 — one-shot turnkey benchmark: build the kit, then Part A (public + holdout) and Part B (D1-D4),
# capturing every output under runs/<name>/ and emitting a RUN_LOG skeleton. Chains the exact commands the
# PLAYBOOK documents so a run is one invocation instead of four hand-typed steps.
#
#   ./run.ps1 -Profile <name> -App <path>/YourApi.csproj [-Tree <treeRoot>] [-Name <run>] [-SkipHoldout] [-SkipBuild]
#
# -Tree defaults to the app's tree (derived from a /src/ segment in -App); -Name defaults to -Profile.
# A failed gate is a valid RESULT (recorded, not thrown) — the script only stops on a genuine error.
[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)][string]$Profile,
  [Parameter(Mandatory = $true)][string]$App,
  [string]$Tree,
  [string]$Name,
  [switch]$SkipHoldout,
  [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot   # the harness resolves profiles/ relative to cwd; pin it to the kit root

if (-not $Name) { $Name = $Profile }
$App = (Resolve-Path $App).Path
if (-not $Tree) {
  $sep = [IO.Path]::DirectorySeparatorChar
  $marker = "${sep}src${sep}"
  $i = $App.ToLower().IndexOf($marker.ToLower())
  $Tree = if ($i -ge 0) { $App.Substring(0, $i) } else { Split-Path $App -Parent }
}
$profileDir = Join-Path 'profiles' $Profile
$outDir = Join-Path 'runs' $Name
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

Write-Host "== turnkey run =="
Write-Host "   profile : $Profile ($profileDir)"
Write-Host "   app     : $App"
Write-Host "   tree    : $Tree"
Write-Host "   out     : $outDir"

if (-not $SkipBuild) {
  Write-Host "`n• building the kit (Harness.slnx) …"
  dotnet build Harness.slnx -v quiet -clp:ErrorsOnly
  if ($LASTEXITCODE -ne 0) { throw "kit build failed" }
}

function Invoke-Gate([string]$mode, [string]$file) {
  Write-Host "`n• gate ($mode) …"
  dotnet run --project Harness.Gate --no-build -- --profile $profileDir --app-project $App --mode $mode 2>&1 |
    Tee-Object -FilePath $file
  $line = (Select-String -Path $file -Pattern 'checks passed' | Select-Object -Last 1).Line
  if ($line) { return $line.Trim() } else { return 'no result line' }
}

$publicResult = Invoke-Gate 'public' (Join-Path $outDir 'gate-public.txt')

$holdoutResult = 'skipped (-SkipHoldout)'
if (-not $SkipHoldout) {
  $holdoutResult = Invoke-Gate 'holdout' (Join-Path $outDir 'gate-holdout.txt')
}

Write-Host "`n• quality (D1-D4) …"
$scorecard = Join-Path $outDir 'scorecard.json'
dotnet run --project Harness.Quality --no-build -- --profile $profileDir --tree $Tree --app-project $App `
  --mode all --out $scorecard 2>&1 | Tee-Object -FilePath (Join-Path $outDir 'quality.txt')

# ---- compact scorecard summary for the RUN_LOG skeleton ------------------------------------------
$sc = $null
if (Test-Path $scorecard) { $sc = Get-Content $scorecard -Raw | ConvertFrom-Json }
function Pct($x) { if ($null -eq $x) { '—' } else { '{0:P0}' -f $x } }
$d1 = if ($sc.d1) { "$($sc.d1.pass)/$($sc.d1.total) ($(Pct $sc.d1.rate))" } else { '—' }
$d2 = if ($sc.d2) { "resilience $(Pct $sc.d2.resilience) · safety $(Pct $sc.d2.safety) · C$($sc.d2.correct)/G$($sc.d2.graceful)/B$($sc.d2.broken)/SW$($sc.d2.silentWrong)" } else { '—' }
$d3 = if ($sc.d3) { "wire-coupling $($sc.d3.wireCouplingCount) · avgCC $($sc.d3.avgCyclomatic) · maxNest $($sc.d3.maxNesting) · LOC $($sc.d3.ownedLoc) · files $($sc.d3.files)" } else { '—' }
$d4 = if ($sc.d4) { "source $($sc.d4.sourceFindings) · deps $($sc.d4.transitiveDeps) · vuln $($sc.d4.vulnerablePackages)" } else { '—' }
$scope = if ($sc.scope) { $sc.scope } else { '?' }

$stamp = (Get-Date).ToString('yyyy-MM-dd HH:mm')
$runLog = Join-Path $outDir 'RUN_LOG.md'
@"
# Turnkey Benchmark — Run Log (skeleton)

**Profile:** ``$profileDir`` · **App:** ``$App`` · **Tree:** ``$Tree``
**Generated:** $stamp by ``run.ps1`` · detected scope = $scope

## Part A — readiness gate
- **public:**  $publicResult
- **holdout:** $holdoutResult

(``public`` all-green ⇒ DONE; ``public`` **and** ``holdout`` all-green ⇒ ROBUST. Per-check detail in
``gate-public.txt`` / ``gate-holdout.txt``.)

## Part B — quality scorecard (``scorecard.json``)
| Dimension | Result |
|---|---|
| **D1** correctness-depth | $d1 |
| **D2** drift | $d2 |
| **D3** maintainability | $d3 |
| **D4** security | $d4 |

## Diagnosis
_TODO — read each red gate check and low dimension against ``API_INTEGRATION_BENCHMARK.md`` §7 and turn it
into a concrete fix. This file is a generated skeleton; fill in the narrative._
"@ | Set-Content -Path $runLog -Encoding utf8

Write-Host "`n== done =="
Write-Host "   public  : $publicResult"
Write-Host "   holdout : $holdoutResult"
Write-Host "   D1 $d1 | D2 $d2"
Write-Host "   D3 $d3"
Write-Host "   D4 $d4"
Write-Host "   artifacts: $outDir/  (scorecard.json, gate-*.txt, quality.txt, RUN_LOG.md)"
