#!/usr/bin/env bash
# run.sh — one-shot turnkey benchmark: build the kit, then Part A (public + holdout) and Part B (D1-D4),
# capturing every output under runs/<name>/ and emitting a RUN_LOG skeleton. POSIX sibling of run.ps1.
#
#   ./run.sh --profile <name> --app <path>/YourApi.csproj [--tree <treeRoot>] [--name <run>] \
#            [--skip-holdout] [--skip-build]
#
# --tree defaults to the app's tree (derived from a /src/ segment in --app); --name defaults to --profile.
# A failed gate is a valid RESULT (recorded, not fatal) — the script only aborts on a genuine error.
set -euo pipefail

PROFILE="" APP="" TREE="" NAME="" SKIP_HOLDOUT=0 SKIP_BUILD=0
while [[ $# -gt 0 ]]; do
  case "$1" in
    --profile)      PROFILE="$2"; shift 2 ;;
    --app)          APP="$2"; shift 2 ;;
    --tree)         TREE="$2"; shift 2 ;;
    --name)         NAME="$2"; shift 2 ;;
    --skip-holdout) SKIP_HOLDOUT=1; shift ;;
    --skip-build)   SKIP_BUILD=1; shift ;;
    *) echo "unknown arg: $1" >&2; exit 2 ;;
  esac
done
[[ -n "$PROFILE" && -n "$APP" ]] || { echo "usage: run.sh --profile <name> --app <csproj> [--tree <dir>] [--name <run>] [--skip-holdout] [--skip-build]" >&2; exit 2; }

cd "$(dirname "$0")"   # the harness resolves profiles/ relative to cwd; pin it to the kit root
APP="$(cd "$(dirname "$APP")" && pwd)/$(basename "$APP")"   # absolutize
[[ -n "$NAME" ]] || NAME="$PROFILE"
if [[ -z "$TREE" ]]; then
  case "$APP" in
    */src/*) TREE="${APP%%/src/*}" ;;
    *)       TREE="$(dirname "$APP")" ;;
  esac
fi
PROFILE_DIR="profiles/$PROFILE"
OUT_DIR="runs/$NAME"
mkdir -p "$OUT_DIR"

echo "== turnkey run =="
echo "   profile : $PROFILE ($PROFILE_DIR)"
echo "   app     : $APP"
echo "   tree    : $TREE"
echo "   out     : $OUT_DIR"

if [[ "$SKIP_BUILD" -eq 0 ]]; then
  echo; echo "• building the kit (Harness.slnx) …"
  dotnet build Harness.slnx -v quiet -clp:ErrorsOnly
fi

run_gate() {  # $1=mode  $2=outfile → echoes ONLY the "N/M checks passed" line on stdout (progress → stderr)
  local mode="$1" file="$2"
  { echo; echo "• gate ($mode) …"; } >&2
  dotnet run --project Harness.Gate --no-build -- --profile "$PROFILE_DIR" --app-project "$APP" --mode "$mode" 2>&1 | tee "$file" >&2 || true
  grep 'checks passed' "$file" | tail -1 | sed 's/^ *//' || echo 'no result line'
}

PUBLIC_RESULT="$(run_gate public "$OUT_DIR/gate-public.txt")"

HOLDOUT_RESULT="skipped (--skip-holdout)"
if [[ "$SKIP_HOLDOUT" -eq 0 ]]; then
  HOLDOUT_RESULT="$(run_gate holdout "$OUT_DIR/gate-holdout.txt")"
fi

echo; echo "• quality (D1-D4) …"
dotnet run --project Harness.Quality --no-build -- --profile "$PROFILE_DIR" --tree "$TREE" --app-project "$APP" \
  --mode all --out "$OUT_DIR/scorecard.json" 2>&1 | tee "$OUT_DIR/quality.txt" || true

# ---- compact scorecard summary (jq if present, else a python fallback) ---------------------------
SUMMARY_D1="—"; SUMMARY_D2="—"; SUMMARY_D3="—"; SUMMARY_D4="—"; SCOPE="?"
SC="$OUT_DIR/scorecard.json"
if [[ -f "$SC" ]] && command -v python >/dev/null 2>&1; then
  read -r SCOPE SUMMARY_D1 SUMMARY_D2 SUMMARY_D3 SUMMARY_D4 < <(python - "$SC" <<'PY'
import json,sys
d=json.load(open(sys.argv[1]))
def pct(x): return "—" if x is None else f"{round(x*100)}%"
d1=d.get("d1"); d2=d.get("d2"); d3=d.get("d3"); d4=d.get("d4")
s_d1=f"{d1['pass']}/{d1['total']}({pct(d1['rate'])})" if d1 else "—"
s_d2=f"res-{pct(d2['resilience'])}/safe-{pct(d2['safety'])}/C{d2['correct']}G{d2['graceful']}B{d2['broken']}SW{d2['silentWrong']}" if d2 else "—"
s_d3=f"wire-{d3['wireCouplingCount']}/avgCC-{d3['avgCyclomatic']}/maxNest-{d3['maxNesting']}/LOC-{d3['ownedLoc']}/files-{d3['files']}" if d3 else "—"
s_d4=f"src-{d4['sourceFindings']}/deps-{d4['transitiveDeps']}/vuln-{d4['vulnerablePackages']}" if d4 else "—"
print(d.get("scope","?"),s_d1,s_d2,s_d3,s_d4)
PY
)
fi

cat > "$OUT_DIR/RUN_LOG.md" <<EOF
# Turnkey Benchmark — Run Log (skeleton)

**Profile:** \`$PROFILE_DIR\` · **App:** \`$APP\` · **Tree:** \`$TREE\`
**Generated:** $(date '+%Y-%m-%d %H:%M') by \`run.sh\` · detected scope = $SCOPE

## Part A — readiness gate
- **public:**  $PUBLIC_RESULT
- **holdout:** $HOLDOUT_RESULT

(\`public\` all-green ⇒ DONE; \`public\` **and** \`holdout\` all-green ⇒ ROBUST. Per-check detail in
\`gate-public.txt\` / \`gate-holdout.txt\`.)

## Part B — quality scorecard (\`scorecard.json\`)
- **D1** correctness-depth : $SUMMARY_D1
- **D2** drift             : $SUMMARY_D2
- **D3** maintainability   : $SUMMARY_D3
- **D4** security          : $SUMMARY_D4

## Diagnosis
_TODO — read each red gate check and low dimension against \`API_INTEGRATION_BENCHMARK.md\` §7 and turn it
into a concrete fix. This file is a generated skeleton; fill in the narrative._
EOF

echo; echo "== done =="
echo "   public  : $PUBLIC_RESULT"
echo "   holdout : $HOLDOUT_RESULT"
echo "   D1 $SUMMARY_D1 | D2 $SUMMARY_D2"
echo "   artifacts: $OUT_DIR/  (scorecard.json, gate-*.txt, quality.txt, RUN_LOG.md)"
