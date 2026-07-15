#requires -Version 7
<#
  Aggregates the per-tree quality.json reports into the Stage-2Q scorecard: per-arm median + IQR +
  range, Cliff's delta with magnitude bands, an exact-where-possible Mann-Whitney U, and a bootstrap
  BCa CI (emitted only when n supports one).

  Why this exists: the published scorecard was hand-aggregated from console output. This derives it
  from artifacts instead, so it is reproducible, auditable, and ready for the Phase-4 N-run campaign
  without a rewrite.

  Deterministic: the bootstrap is seeded (-Seed). Same inputs => same output, always.

  Inputs : benchmark/runs/<tree>/quality.json   (produce them with score-all.ps1)
  Outputs: benchmark/runs/scorecard.json        (machine-readable, full per-tree values)
           benchmark/runs/scorecard.md          (the table, shaped like QUALITY_FINDINGS.md §2)

  Usage:
    pwsh benchmark/harness/aggregate-scorecard.ps1
    pwsh benchmark/harness/aggregate-scorecard.ps1 -Verify     # diff against the published scorecard
    pwsh benchmark/harness/aggregate-scorecard.ps1 -MatchedPairOnly
#>
[CmdletBinding()]
param(
    [string]$RunsDir,
    [int]$Seed = 20260715,          # bootstrap seed — pinned so results are reproducible
    [int]$Bootstrap = 10000,
    [int]$MinBootstrapN = 8,        # below this a resample of a resample is theatre, not inference
    [switch]$Verify,                # diff computed values against the published QUALITY_FINDINGS §2 table
    [switch]$MatchedPairOnly,       # scope22-armA vs scope22-armB only (the independent, uncorrelated pair)
    [switch]$Scope22Only,           # 22-op trees only — makes the raw-count rows comparable
    [switch]$Quiet
)
$ErrorActionPreference = 'Stop'
if (-not $RunsDir) { $RunsDir = Join-Path (Split-Path -Parent $PSScriptRoot) 'runs' }

# ─────────────────────────────────────────────────────────────────────────────────────────────
# Pre-registered configuration. Exclusions and metric directions are LOCKED (PROTOCOL.md §7.1,
# QUALITY_PROTOCOL.md §5) — they are not tuned to the data.
# ─────────────────────────────────────────────────────────────────────────────────────────────

# Excluded from the aggregate per the pre-registered infra-failure rule. Still SCORED, and still
# printed below as an instrument sanity anchor: a genuinely incomplete tree must score near-zero on
# D1, and these do (~17%). Exclusion from a result is not deletion from the record.
$EXCLUDED = [ordered]@{
    'pilot2-armA'  = 'infra stall (apiError=true) at turn 68, before DONE — PROTOCOL.md §7.1'
    'pilot2r-armA' = 'infra stall (apiError=true) at turn 4, before DONE — PROTOCOL.md §7.1'
}

# Correlated trees: same SDK approach, different reference delivery. They are NOT independent draws,
# which is why -MatchedPairOnly exists and why the p-values below carry a health warning.
$CORRELATED = @('scope22lean-armA', 'scope22split-armA')

# ScopeSensitive = the metric is a raw COUNT whose ceiling depends on task size, so it is only
# comparable between trees of the SAME scope. The 11-op pilot trees run 13 drift cells and carry less
# code; the 22-op trees run 22 cells. Rates (Resilience/Safety/D1) normalise by N and are scope-robust;
# per-project figures (deps) don't move with op count. Mixing scopes on a ScopeSensitive row produces an
# artifact — see the pool-composition warning below and use -Scope22Only for a like-for-like read.
$METRICS = @(
    @{ Id='D1.rate';            Path='d1.Rate';               Dir='higher'; Fmt='pct'; Label='D1 correctness-depth rate' }
    @{ Id='D2.resilience';      Path='d2.Resilience';         Dir='higher'; Fmt='pct'; Label='D2 drift resilience' }
    @{ Id='D2.safety';          Path='d2.Safety';             Dir='higher'; Fmt='pct'; Label='D2 failure-safety' }
    @{ Id='D2.silentWrongRate'; Path='__swRate';              Dir='lower';  Fmt='pct'; Label='D2 silent-wrong rate' }
    @{ Id='D2.silentWrong';     Path='d2.SilentWrong';        Dir='lower';  Fmt='int'; Label='D2 silent-wrong count'; ScopeSensitive=$true }
    @{ Id='D3.wireCoupling';    Path='d3.WireCouplingCount';  Dir='lower';  Fmt='int'; Label='D3 wire-coupling'; ScopeSensitive=$true }
    @{ Id='D3.avgCC';           Path='d3.AvgCyclomatic';      Dir='lower';  Fmt='f2';  Label='D3 avg cyclomatic' }
    @{ Id='D3.maxNesting';      Path='d3.MaxNesting';         Dir='lower';  Fmt='int'; Label='D3 max nesting' }
    @{ Id='D3.ownedLoc';        Path='d3.OwnedLoc';           Dir='lower';  Fmt='int'; Label='D3 owned integration LOC'; ScopeSensitive=$true }
    @{ Id='D4.deps';            Path='d4.TransitiveDeps';     Dir='lower';  Fmt='int'; Label='D4 transitive deps' }
    @{ Id='D4.vulnerable';      Path='d4.VulnerablePackages'; Dir='lower';  Fmt='int'; Label='D4 vulnerable pkgs' }
    @{ Id='D4.sourceFindings';  Path='d4.SourceFindings';     Dir='lower';  Fmt='int'; Label='D4 source-security findings' }
)

# The published QUALITY_FINDINGS.md §2 medians — the hand-aggregated values this script is meant to
# either confirm or contradict. -Verify diffs against these.
$PUBLISHED = @{
    'D1.rate'           = @{ A=1.00; B=1.00; Band='small'      }
    'D2.resilience'     = @{ A=0.41; B=0.54; Band='large'      }
    'D2.safety'         = @{ A=0.64; B=0.62; Band='small'      }
    'D2.silentWrong'    = @{ A=8;    B=5;    Band='negligible' }
    'D3.wireCoupling'   = @{ A=0;    B=19;   Band='large'      }
    'D3.avgCC'          = @{ A=2.33; B=2.16; Band='negligible' }
    'D3.maxNesting'     = @{ A=4;    B=6;    Band='large'      }
    'D3.ownedLoc'       = @{ A=776;  B=968                     }
    'D4.deps'           = @{ A=96;   B=90;   Band='large'      }
    'D4.vulnerable'     = @{ A=4;    B=5                       }
    'D4.sourceFindings' = @{ A=0;    B=0                       }
}

# ─────────────────────────────────────────────────────────────────────────────────────────────
# Statistics
# ─────────────────────────────────────────────────────────────────────────────────────────────

# Quantile, type 7 (linear interpolation between order statistics) — R's and numpy's default.
# Stated explicitly because at n=3 the quartile convention visibly moves the IQR.
function Get-Quantile([double[]]$x, [double]$q) {
    $s = [double[]]($x | Sort-Object); $n = $s.Count
    if ($n -eq 0) { return [double]::NaN }
    if ($n -eq 1) { return $s[0] }
    $h  = ($n - 1) * $q
    $lo = [int][Math]::Floor($h); $hi = [int][Math]::Ceiling($h)
    return $s[$lo] + ($h - $lo) * ($s[$hi] - $s[$lo])
}

function Get-NormalCdf([double]$x) {
    # Abramowitz & Stegun 26.2.17, |error| < 7.5e-8
    $b = @(0.319381530, -0.356563782, 1.781477937, -1.821255978, 1.330274429); $p = 0.2316419
    $neg = $x -lt 0; if ($neg) { $x = -$x }
    $t = 1.0 / (1.0 + $p * $x)
    $poly = ((((($b[4]*$t + $b[3])*$t) + $b[2])*$t + $b[1])*$t + $b[0])*$t
    $cdf = 1.0 - (1.0/[Math]::Sqrt(2*[Math]::PI)) * [Math]::Exp(-0.5*$x*$x) * $poly
    if ($neg) { return 1.0 - $cdf } else { return $cdf }
}

function Get-NormalInv([double]$p) {
    # Acklam's inverse normal CDF
    if ($p -le 0) { return [double]::NegativeInfinity }
    if ($p -ge 1) { return [double]::PositiveInfinity }
    $a = @(-3.969683028665376e+01, 2.209460984245205e+02, -2.759285104469687e+02, 1.383577518672690e+02, -3.066479806614716e+01, 2.506628277459239e+00)
    $b = @(-5.447609879822406e+01, 1.615858368580409e+02, -1.556989798598866e+02, 6.680131188771972e+01, -1.328068155288572e+01)
    $c = @(-7.784894002430293e-03, -3.223964580411365e-01, -2.400758277161838e+00, -2.549732539343734e+00, 4.374664141464968e+00, 2.938163982698783e+00)
    $d = @(7.784695709041462e-03, 3.224671290700398e-01, 2.445134137142996e+00, 3.754408661907416e+00)
    $pl = 0.02425
    if ($p -lt $pl) {
        $q = [Math]::Sqrt(-2*[Math]::Log($p))
        return ((((($c[0]*$q+$c[1])*$q+$c[2])*$q+$c[3])*$q+$c[4])*$q+$c[5]) / (((($d[0]*$q+$d[1])*$q+$d[2])*$q+$d[3])*$q+1)
    }
    if ($p -gt 1-$pl) {
        $q = [Math]::Sqrt(-2*[Math]::Log(1-$p))
        return -((((($c[0]*$q+$c[1])*$q+$c[2])*$q+$c[3])*$q+$c[4])*$q+$c[5]) / (((($d[0]*$q+$d[1])*$q+$d[2])*$q+$d[3])*$q+1)
    }
    $q = $p - 0.5; $r = $q*$q
    return ((((($a[0]*$r+$a[1])*$r+$a[2])*$r+$a[3])*$r+$a[4])*$r+$a[5])*$q / ((((($b[0]*$r+$b[1])*$r+$b[2])*$r+$b[3])*$r+$b[4])*$r+1)
}

# Cliff's delta: P(x>y) - P(x<y). Sign is on the RAW metric; direction is applied later.
function Get-CliffsDelta([double[]]$x, [double[]]$y) {
    $gt = 0; $lt = 0
    foreach ($a in $x) { foreach ($b in $y) { if ($a -gt $b) { $gt++ } elseif ($a -lt $b) { $lt++ } } }
    return ($gt - $lt) / [double]($x.Count * $y.Count)
}

function Get-CliffBand([double]$d) {
    $a = [Math]::Abs($d)
    if     ($a -lt 0.147) { 'negligible' }
    elseif ($a -lt 0.33)  { 'small' }
    elseif ($a -lt 0.474) { 'medium' }
    else                  { 'large' }
}

function Get-U([double[]]$x, [double[]]$y) {
    $u = 0.0
    foreach ($a in $x) { foreach ($b in $y) { if ($a -gt $b) { $u += 1.0 } elseif ($a -eq $b) { $u += 0.5 } } }
    return $u
}

function Get-Binomial([int]$n, [int]$k) {
    if ($k -lt 0 -or $k -gt $n) { return 0.0 }
    $k = [Math]::Min($k, $n - $k); $r = 1.0
    for ($i = 1; $i -le $k; $i++) { $r = $r * ($n - $k + $i) / $i }
    return $r
}

function Get-Combinations([int]$n, [int]$k) {
    $out = [Collections.Generic.List[int[]]]::new()
    if ($k -lt 0 -or $k -gt $n) { return $out }
    $c = [int[]](0..($k-1))
    while ($true) {
        $out.Add([int[]]$c.Clone())
        $i = $k - 1
        while ($i -ge 0 -and $c[$i] -eq $i + $n - $k) { $i-- }
        if ($i -lt 0) { break }
        $c[$i]++
        for ($j = $i + 1; $j -lt $k; $j++) { $c[$j] = $c[$j-1] + 1 }
    }
    return $out
}

# Mann-Whitney U. Exact permutation while the split count is tractable (which covers this study's
# n=5/3 at 56 splits); tie-corrected normal approximation once N grows past that (Phase 4).
function Get-MannWhitney([double[]]$x, [double[]]$y, [double]$MaxExact = 200000) {
    $n1 = $x.Count; $n2 = $y.Count; $n = $n1 + $n2
    if ($n1 -eq 0 -or $n2 -eq 0) { return @{ U=[double]::NaN; P=[double]::NaN; Method='n/a' } }
    $all  = [double[]](@($x) + @($y))
    $uObs = Get-U $x $y
    $mean = $n1 * $n2 / 2.0
    $dev  = [Math]::Abs($uObs - $mean)

    if ((Get-Binomial $n $n1) -le $MaxExact) {
        $combos = Get-Combinations $n $n1
        $extreme = 0
        foreach ($c in $combos) {
            $mask = [bool[]]::new($n); foreach ($i in $c) { $mask[$i] = $true }
            $xs = [Collections.Generic.List[double]]::new(); $ys = [Collections.Generic.List[double]]::new()
            for ($i = 0; $i -lt $n; $i++) { if ($mask[$i]) { $xs.Add($all[$i]) } else { $ys.Add($all[$i]) } }
            $u = Get-U ([double[]]$xs.ToArray()) ([double[]]$ys.ToArray())
            if ([Math]::Abs($u - $mean) -ge $dev - 1e-9) { $extreme++ }
        }
        return @{ U=$uObs; P=($extreme / [double]$combos.Count); Method="exact permutation ($($combos.Count) splits)" }
    }

    # normal approximation, tie-corrected, with continuity correction
    $ties = ($all | Group-Object | ForEach-Object { [double]$_.Count })
    $tieSum = 0.0; foreach ($t in $ties) { $tieSum += ($t*$t*$t - $t) }
    $var = ($n1 * $n2 / 12.0) * (($n + 1) - $tieSum / [double]($n * ($n - 1)))
    if ($var -le 0) { return @{ U=$uObs; P=1.0; Method='normal approx (zero variance)' } }
    $z = ($dev - 0.5) / [Math]::Sqrt($var)
    return @{ U=$uObs; P=(2.0 * (1.0 - (Get-NormalCdf $z))); Method='normal approximation (tie-corrected)' }
}

# Bootstrap BCa CI for the median. Refuses below MinBootstrapN rather than emitting a fake interval.
function Get-BcaCi([double[]]$x, [int]$B, [double]$Alpha, [int]$Seed, [int]$MinN) {
    $n = $x.Count
    if ($n -lt $MinN) { return @{ Ok=$false; Reason="n=$n < $MinN — a bootstrap CI on this few points is not meaningful" } }
    $theta = Get-Quantile $x 0.5
    if (($x | Select-Object -Unique).Count -eq 1) { return @{ Ok=$true; Lo=$theta; Hi=$theta; Note='degenerate: every value identical' } }

    $rng = [Random]::new($Seed)
    $boot = [double[]]::new($B)
    for ($b = 0; $b -lt $B; $b++) {
        $s = [double[]]::new($n)
        for ($i = 0; $i -lt $n; $i++) { $s[$i] = $x[$rng.Next($n)] }
        $boot[$b] = Get-Quantile $s 0.5
    }
    $sorted = [double[]]($boot | Sort-Object)

    $less = ($boot | Where-Object { $_ -lt $theta }).Count
    $prop = $less / [double]$B
    if ($prop -le 0 -or $prop -ge 1) { return @{ Ok=$true; Lo=$sorted[0]; Hi=$sorted[$B-1]; Note='z0 undefined (bootstrap mass on one side); percentile CI reported' } }
    $z0 = Get-NormalInv $prop

    # jackknife acceleration
    $jack = [double[]]::new($n)
    for ($i = 0; $i -lt $n; $i++) {
        $sub = [Collections.Generic.List[double]]::new()
        for ($j = 0; $j -lt $n; $j++) { if ($j -ne $i) { $sub.Add($x[$j]) } }
        $jack[$i] = Get-Quantile ([double[]]$sub.ToArray()) 0.5
    }
    $jbar = ($jack | Measure-Object -Average).Average
    $num = 0.0; $den = 0.0
    foreach ($j in $jack) { $d = $jbar - $j; $num += $d*$d*$d; $den += $d*$d }
    $a = if ($den -le 0) { 0.0 } else { $num / (6.0 * [Math]::Pow($den, 1.5)) }

    $zA = Get-NormalInv ($Alpha/2.0); $zB = Get-NormalInv (1.0 - $Alpha/2.0)
    $a1 = Get-NormalCdf ($z0 + ($z0 + $zA) / (1.0 - $a*($z0 + $zA)))
    $a2 = Get-NormalCdf ($z0 + ($z0 + $zB) / (1.0 - $a*($z0 + $zB)))
    return @{ Ok=$true; Lo=(Get-Quantile $sorted $a1); Hi=(Get-Quantile $sorted $a2); Z0=$z0; A=$a }
}

# ─────────────────────────────────────────────────────────────────────────────────────────────
# Load
# ─────────────────────────────────────────────────────────────────────────────────────────────

function Get-Path($obj, [string]$path) {
    # __swRate is derived, not stored: silent-wrong normalised by the tree's own cell count, which is
    # what makes it comparable between an 11-op tree (13 cells) and a 22-op tree (22 cells).
    if ($path -eq '__swRate') {
        $n = $obj.d2.cells.Count
        if (-not $n) { return $null }
        return $obj.d2.SilentWrong / [double]$n
    }
    $cur = $obj
    foreach ($seg in $path.Split('.')) {
        if ($null -eq $cur) { return $null }
        $cur = $cur.PSObject.Properties[$seg]?.Value
    }
    return $cur
}

$reports = @()
foreach ($f in Get-ChildItem $RunsDir -Directory | Sort-Object Name) {
    $qp = Join-Path $f.FullName 'quality.json'
    if (-not (Test-Path $qp)) { continue }
    if ($f.Name -like 'extend-*') { continue }                 # D5 artifacts, not arm builds
    if ($f.Name -notmatch '-arm([AB])$') { continue }
    $arm  = $Matches[1]
    $data = Get-Content $qp -Raw | ConvertFrom-Json -Depth 24
    # Task scope, read off the tree's own drift-cell count (13 => the 11-op pilot surface, 22 => the
    # full 22-op surface). Needed because raw-count metrics are only comparable within a scope.
    $reports += [pscustomobject]@{
        Tree  = $f.Name
        Arm   = $arm
        Cells = $data.d2.cells.Count
        Scope = if ($data.d2.cells.Count -ge 20) { 22 } else { 11 }
        Data  = $data
    }
}

if (-not $reports) {
    Write-Host 'No quality.json found. Generate the inputs first:' -ForegroundColor Red
    Write-Host '  pwsh benchmark/harness/score-all.ps1' -ForegroundColor Yellow
    exit 1
}

$included = $reports | Where-Object { -not $EXCLUDED.Contains($_.Tree) }
$dropped  = $reports | Where-Object {     $EXCLUDED.Contains($_.Tree) }
if ($MatchedPairOnly) { $included = $included | Where-Object { $_.Tree -in @('scope22-armA','scope22-armB') } }
if ($Scope22Only)     { $included = $included | Where-Object { $_.Scope -eq 22 } }

$armA = @($included | Where-Object Arm -eq 'A')
$armB = @($included | Where-Object Arm -eq 'B')

# Pool composition. If the two arms' scope mixes differ, every ScopeSensitive row is confounded: a
# median can land on a 22-cell tree for one arm and a 13-cell tree for the other, and the row then
# measures task size as much as it measures the arm.
$scopeMixA = ($armA | Group-Object Scope | Sort-Object Name | ForEach-Object { "$($_.Count)x$($_.Name)-op" }) -join ' + '
$scopeMixB = ($armB | Group-Object Scope | Sort-Object Name | ForEach-Object { "$($_.Count)x$($_.Name)-op" }) -join ' + '
# Confounded iff the pool mixes task sizes at all. (Comparing the mix STRINGS would fire on a mere
# difference in n, which is not a confound — unequal group sizes are fine, unequal task sizes are not.)
$scopeConfounded = (@($included.Scope | Select-Object -Unique).Count -gt 1)

if (-not $Quiet) {
    Write-Host "== scorecard ==" -ForegroundColor Cyan
    Write-Host ("  Arm A (SDK) : n={0}  [{1}]  scope: {2}" -f $armA.Count, ($armA.Tree -join ', '), $scopeMixA)
    Write-Host ("  Arm B (spec): n={0}  [{1}]  scope: {2}" -f $armB.Count, ($armB.Tree -join ', '), $scopeMixB)
    foreach ($d in $dropped) { Write-Host ("  excluded    : {0} — {1}" -f $d.Tree, $EXCLUDED[$d.Tree]) -ForegroundColor DarkGray }
    if ($scopeConfounded) {
        Write-Host ''
        Write-Host '  ! SCOPE-MIX WARNING: the arms have different scope compositions, so every row marked' -ForegroundColor Yellow
        Write-Host '    [scope-sensitive] below is confounded by task size. Use -Scope22Only or -MatchedPairOnly.' -ForegroundColor Yellow
    }
}

# ─────────────────────────────────────────────────────────────────────────────────────────────
# Compute
# ─────────────────────────────────────────────────────────────────────────────────────────────

function Format-Val($v, [string]$fmt) {
    if ($null -eq $v -or [double]::IsNaN($v)) { return 'n/a' }
    switch ($fmt) {
        'pct' { '{0:P0}' -f $v }
        'f2'  { '{0:N2}' -f $v }
        default { '{0:N0}' -f $v }
    }
}

$rows = @()
foreach ($m in $METRICS) {
    $xa = [double[]]($armA | ForEach-Object { Get-Path $_.Data $m.Path } | Where-Object { $null -ne $_ })
    $xb = [double[]]($armB | ForEach-Object { Get-Path $_.Data $m.Path } | Where-Object { $null -ne $_ })
    if ($xa.Count -eq 0 -or $xb.Count -eq 0) { continue }

    $delta = Get-CliffsDelta $xa $xb
    $band  = Get-CliffBand $delta
    $mw    = Get-MannWhitney $xa $xb

    # Direction: positive delta means Arm A's RAW values are larger; translate that into who it favours.
    # A median TIE is parity regardless of delta — reporting "favours B" when both arms sit at 100%
    # (which a small negative delta will happily do) misreads a tie as a result.
    $medA = Get-Quantile $xa 0.5; $medB = Get-Quantile $xb 0.5
    $favors = if ($band -eq 'negligible' -or $delta -eq 0 -or $medA -eq $medB) { '~parity' }
              elseif ($m.Dir -eq 'higher') { if ($delta -gt 0) { 'A' } else { 'B' } }
              else                         { if ($delta -gt 0) { 'B' } else { 'A' } }

    $rows += [pscustomobject]@{
        Id      = $m.Id
        Label   = $m.Label
        Dir     = $m.Dir
        ScopeSensitive = [bool]$m.ScopeSensitive
        ScopeConfounded = ([bool]$m.ScopeSensitive -and $scopeConfounded)
        A = [ordered]@{
            n      = $xa.Count
            median = Get-Quantile $xa 0.5
            q1     = Get-Quantile $xa 0.25
            q3     = Get-Quantile $xa 0.75
            min    = ($xa | Measure-Object -Minimum).Minimum
            max    = ($xa | Measure-Object -Maximum).Maximum
            values = $xa
            ci     = Get-BcaCi $xa $Bootstrap 0.05 $Seed $MinBootstrapN
        }
        B = [ordered]@{
            n      = $xb.Count
            median = Get-Quantile $xb 0.5
            q1     = Get-Quantile $xb 0.25
            q3     = Get-Quantile $xb 0.75
            min    = ($xb | Measure-Object -Minimum).Minimum
            max    = ($xb | Measure-Object -Maximum).Maximum
            values = $xb
            ci     = Get-BcaCi $xb $Bootstrap 0.05 ($Seed+1) $MinBootstrapN
        }
        CliffsDelta = [Math]::Round($delta, 4)
        Band        = $band
        MannWhitneyU = $mw.U
        P            = if ([double]::IsNaN($mw.P)) { $null } else { [Math]::Round($mw.P, 5) }
        PMethod      = $mw.Method
        Favors       = $favors
        Overlap      = -not (($xa | Measure-Object -Maximum).Maximum -lt ($xb | Measure-Object -Minimum).Minimum -or
                             ($xb | Measure-Object -Maximum).Maximum -lt ($xa | Measure-Object -Minimum).Minimum)
    }
}

# ─────────────────────────────────────────────────────────────────────────────────────────────
# Emit
# ─────────────────────────────────────────────────────────────────────────────────────────────

$md = [Collections.Generic.List[string]]::new()
$md.Add('# Scorecard (generated)')
$md.Add('')
$md.Add("> Generated by ``benchmark/harness/aggregate-scorecard.ps1`` from ``runs/*/quality.json``.")
$md.Add("> Arm A (SDK) n=$($armA.Count) · Arm B (spec) n=$($armB.Count) · bootstrap seed $Seed.")
$md.Add('> Medians across included trees. Cliff''s delta sign is on the raw metric; **Favors** applies direction.')
$md.Add('')
$md.Add('| Dimension | Arm A (SDK) | Arm B (spec) | Cliff''s δ | Effect | Mann–Whitney p | Ranges overlap? | Favors |')
$md.Add('|---|---:|---:|---:|:--:|---:|:--:|:--:|')
foreach ($r in $rows) {
    $arrow = if ($r.Dir -eq 'lower') { ' (↓)' } else { '' }
    if ($r.ScopeConfounded) { $arrow += ' **[scope-sensitive]**' }
    $p = if ($null -eq $r.P) { '—' } else { '{0:N4}' -f $r.P }
    $md.Add(('| {0}{1} | {2} | {3} | {4:N2} | {5} | {6} | {7} | {8} |' -f
        $r.Label, $arrow,
        (Format-Val $r.A.median ($METRICS | Where-Object Id -eq $r.Id).Fmt),
        (Format-Val $r.B.median ($METRICS | Where-Object Id -eq $r.Id).Fmt),
        $r.CliffsDelta, $r.Band, $p,
        $(if ($r.Overlap) { 'yes' } else { '**no**' }),
        $r.Favors))
}
$md.Add('')
$md.Add('## How to read this')
$md.Add('')
$md.Add('- **Ranges overlap = no** is the load-bearing column at this sample size. A non-overlapping range')
$md.Add('  means every Arm A tree beats every Arm B tree (or vice versa) — that survives small n in a way a')
$md.Add('  p-value does not.')
$md.Add('- **The p-values are anti-conservative and should not be quoted as evidence.** Mann–Whitney assumes')
$md.Add('  independent draws; the Arm A pool contains correlated delivery variants (' + ($CORRELATED -join ', ') + ').')
$md.Add('  Re-run with `-MatchedPairOnly` for the clean, uncorrelated comparison.')
$md.Add('- **No bootstrap CI is emitted below n=' + $MinBootstrapN + '** by design. Resampling 3 points does not')
$md.Add('  manufacture the information a CI implies.')
if ($scopeConfounded) {
    $md.Add('- **Rows marked `[scope-sensitive]` are confounded and should not be quoted.** They are raw counts')
    $md.Add('  whose ceiling scales with task size, and the arms have different scope mixes:')
    $md.Add('  Arm A = ' + $scopeMixA + ', Arm B = ' + $scopeMixB + '. An 11-op tree runs 13 drift cells and')
    $md.Add('  carries less code than a 22-op tree''s 22 cells, so a median can land on a 22-cell tree for one')
    $md.Add('  arm and a 13-cell tree for the other — measuring task size, not the arm. Use `-Scope22Only`.')
    $md.Add('  `D2 silent-wrong rate` is the scope-invariant form of the silent-wrong count; prefer it.')
}
$md.Add('')
$md.Add('## Per-tree values')
$md.Add('')
$md.Add('| Dimension | ' + (($included | Sort-Object Arm, Tree | ForEach-Object { $_.Tree }) -join ' | ') + ' |')
$md.Add('|---|' + ((1..$included.Count | ForEach-Object { '---:' }) -join '|') + '|')
foreach ($m in $METRICS) {
    $cells = foreach ($t in ($included | Sort-Object Arm, Tree)) { Format-Val (Get-Path $t.Data $m.Path) $m.Fmt }
    $md.Add('| ' + $m.Label + ' | ' + ($cells -join ' | ') + ' |')
}
if ($dropped) {
    $md.Add('')
    $md.Add('## Excluded trees (instrument sanity anchors)')
    $md.Add('')
    $md.Add('Excluded from the aggregate per the pre-registered infra-failure rule, but scored and shown here:')
    $md.Add('a genuinely incomplete integration **must** score near-zero on D1, which is what validates that the')
    $md.Add('instrument is not blind.')
    $md.Add('')
    $md.Add('| Tree | Reason | D1 rate |')
    $md.Add('|---|---|---:|')
    foreach ($d in $dropped) { $md.Add('| ' + $d.Tree + ' | ' + $EXCLUDED[$d.Tree] + ' | ' + (Format-Val (Get-Path $d.Data 'd1.Rate') 'pct') + ' |') }
}

$mdPath = Join-Path $RunsDir 'scorecard.md'
$md -join "`n" | Set-Content $mdPath
[ordered]@{
    generatedBy = 'aggregate-scorecard.ps1'
    seed        = $Seed
    bootstrap   = $Bootstrap
    matchedPairOnly = [bool]$MatchedPairOnly
    armA = @($armA.Tree); armB = @($armB.Tree)
    excluded = $EXCLUDED
    correlatedInArmA = $CORRELATED
    rows = $rows
} | ConvertTo-Json -Depth 12 | Set-Content (Join-Path $RunsDir 'scorecard.json')

if (-not $Quiet) {
    Write-Host ''
    ($md -join "`n")
    Write-Host ''
    Write-Host "wrote $mdPath" -ForegroundColor Green
    Write-Host "wrote $(Join-Path $RunsDir 'scorecard.json')" -ForegroundColor Green
}

# ─────────────────────────────────────────────────────────────────────────────────────────────
# Verify against the published (hand-aggregated) scorecard
# ─────────────────────────────────────────────────────────────────────────────────────────────
if ($Verify) {
    Write-Host ''
    Write-Host '== verify vs published QUALITY_FINDINGS.md §2 ==' -ForegroundColor Cyan
    $bad = 0
    foreach ($r in $rows) {
        $exp = $PUBLISHED[$r.Id]; if (-not $exp) { continue }
        $tol = if ((($METRICS | Where-Object Id -eq $r.Id).Fmt) -eq 'int') { 0.001 } else { 0.006 }
        $dA = [Math]::Abs($r.A.median - $exp.A); $dB = [Math]::Abs($r.B.median - $exp.B)
        $okA = $dA -le $tol; $okB = $dB -le $tol
        $okBand = (-not $exp.Band) -or ($exp.Band -eq $r.Band)
        if ($okA -and $okB -and $okBand) {
            Write-Host ("  ok   {0,-22} A={1} B={2} {3}" -f $r.Id, $r.A.median, $r.B.median, $r.Band) -ForegroundColor Green
        } else {
            $bad++
            Write-Host ("  DIFF {0,-22} computed A={1} B={2} band={3} | published A={4} B={5} band={6}" -f
                $r.Id, $r.A.median, $r.B.median, $r.Band, $exp.A, $exp.B, $exp.Band) -ForegroundColor Red
        }
    }
    Write-Host ''
    if ($bad -eq 0) { Write-Host 'PUBLISHED SCORECARD REPRODUCED — hand-aggregation was correct.' -ForegroundColor Green }
    else { Write-Host "$bad row(s) DIVERGE from the published scorecard — investigate before trusting either." -ForegroundColor Red }
    exit ($bad -eq 0 ? 0 : 1)
}
