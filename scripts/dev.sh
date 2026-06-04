#!/usr/bin/env bash
# Start API (8000) and public frontend (3000). Dashboard (3001) is optional.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PORTS=(8000 3000)
PIDS=()
WITH_DASHBOARD=false

log() { printf '\033[1;36m[dev]\033[0m %s\n' "$*"; }
err() { printf '\033[1;31m[dev]\033[0m %s\n' "$*" >&2; }

port_in_use() {
  lsof -nP -iTCP:"$1" -sTCP:LISTEN >/dev/null 2>&1
}

kill_ports() {
  for port in "${PORTS[@]}"; do
    local pids
    pids=$(lsof -ti :"$port" 2>/dev/null || true)
    if [[ -n "$pids" ]]; then
      log "Stopping process(es) on port $port ..."
      echo "$pids" | xargs kill -9 2>/dev/null || true
    fi
  done
}

cleanup() {
  log "Shutting down ..."
  for pid in "${PIDS[@]}"; do
    kill "$pid" 2>/dev/null || true
  done
  for port in "${PORTS[@]}"; do
    lsof -ti :"$port" 2>/dev/null | xargs kill -9 2>/dev/null || true
  done
  exit 0
}

trap cleanup EXIT INT TERM

usage() {
  cat <<EOF
Usage: $(basename "$0") [options]

Options:
  --kill-ports     Free 8000 and 3000 before starting (default)
  --no-kill        Fail if any port is already in use
  --with-dashboard Also start frontend-dashboard on http://localhost:3001
  -h, --help       Show this help

Services (default):
  API             http://localhost:8000  (docs: /docs)
  Public site     http://localhost:3000

Press Ctrl+C to stop all services.
EOF
}

KILL_PORTS=true
while [[ $# -gt 0 ]]; do
  case "$1" in
    --kill-ports) KILL_PORTS=true ;;
    --no-kill) KILL_PORTS=false ;;
    --with-dashboard) WITH_DASHBOARD=true ;;
    -h|--help) usage; exit 0 ;;
    *) err "Unknown option: $1"; usage; exit 1 ;;
  esac
  shift
done

if $WITH_DASHBOARD; then
  PORTS+=(3001)
fi

if $KILL_PORTS; then
  kill_ports
else
  for port in "${PORTS[@]}"; do
    if port_in_use "$port"; then
      err "Port $port is already in use. Run with --kill-ports or stop the process manually."
      exit 1
    fi
  done
fi

# --- Preflight ---
if [[ ! -d "$ROOT/.venv" ]]; then
  err "Python venv not found. Run from repo root:"
  err "  python3 -m venv .venv && source .venv/bin/activate"
  err "  pip install -e packages/shared -e packages/schema-engine -e packages/certificate-engine -e packages/verification-engine -r backend-api/requirements.txt"
  exit 1
fi

if [[ ! -d "$ROOT/frontend-public/node_modules" ]]; then
  err "frontend-public dependencies missing. Run: cd frontend-public && npm install"
  exit 1
fi

if $WITH_DASHBOARD && [[ ! -d "$ROOT/frontend-dashboard/node_modules" ]]; then
  err "frontend-dashboard dependencies missing. Run: cd frontend-dashboard && npm install"
  exit 1
fi

# shellcheck source=/dev/null
source "$ROOT/.venv/bin/activate"

log "Starting DevicePassport dev stack ..."
echo ""
log "  API .............. http://localhost:8000"
log "  Public site ...... http://localhost:3000"
if $WITH_DASHBOARD; then
  log "  Dashboard ........ http://localhost:3001"
fi
echo ""

# --- API ---
(
  cd "$ROOT/backend-api"
  exec uvicorn app.main:app --reload --host 127.0.0.1 --port 8000
) 2>&1 | sed 's/^/[api] /' &
PIDS+=($!)

# --- Public frontend ---
(
  cd "$ROOT/frontend-public"
  exec npm run dev -- --port 3000
) 2>&1 | sed 's/^/[public] /' &
PIDS+=($!)

if $WITH_DASHBOARD; then
  (
    cd "$ROOT/frontend-dashboard"
    exec npm run dev -- --port 3001
  ) 2>&1 | sed 's/^/[dashboard] /' &
  PIDS+=($!)
fi

log "All services started. Press Ctrl+C to stop."
echo ""

wait
