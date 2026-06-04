#!/usr/bin/env bash
# Stop anything listening on dev ports 8000, 3000, 3001.
set -euo pipefail

PORTS=(8000 3000 3001)
found=false

for port in "${PORTS[@]}"; do
  pids=$(lsof -ti :"$port" 2>/dev/null || true)
  if [[ -n "$pids" ]]; then
    found=true
    echo "Stopping port $port (PIDs: $(echo "$pids" | tr '\n' ' '))"
    echo "$pids" | xargs kill -9 2>/dev/null || true
  fi
done

pkill -f "uvicorn app.main:app" 2>/dev/null || true

if $found; then
  echo "Ports 8000, 3000, 3001 cleared."
else
  echo "No listeners on 8000, 3000, or 3001."
fi
