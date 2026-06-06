#!/usr/bin/env bash
# Wipe staging/test application data before production promotion.
# Keeps schema, agent_versions, agent-releases bucket, and auth.users.
#
# Usage:
#   export DATABASE_URL='postgresql://...'
#   ./scripts/data-reset-prod.sh
#   ./scripts/data-reset-prod.sh --yes          # skip confirmation
#   ./scripts/data-reset-prod.sh --skip-storage # SQL only (orphan evidence files may remain)

set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
DATA_RESET_SQL="$ROOT/database/data_reset.sql"
ENV_FILE="$ROOT/backend-api/.env"

SKIP_CONFIRM=0
SKIP_STORAGE=0

for arg in "$@"; do
  case "$arg" in
    --yes|-y) SKIP_CONFIRM=1 ;;
    --skip-storage) SKIP_STORAGE=1 ;;
    -h|--help)
      sed -n '2,9p' "$0"
      exit 0
      ;;
    *)
      echo "Unknown option: $arg (use --help)" >&2
      exit 1
      ;;
  esac
done

if [[ -z "${DATABASE_URL:-}" && -f "$ENV_FILE" ]]; then
  set -a
  # shellcheck disable=SC1090
  source "$ENV_FILE"
  set +a
fi

if [[ -z "${DATABASE_URL:-}" ]]; then
  echo "ERROR: DATABASE_URL is not set (backend-api/.env or environment)." >&2
  exit 1
fi

if ! command -v psql >/dev/null 2>&1; then
  echo "ERROR: psql not found." >&2
  exit 1
fi

if [[ "$SKIP_CONFIRM" -ne 1 ]]; then
  echo ""
  echo "WARNING: This will DELETE all application data:"
  echo "  • certificates, devices, scans, tenants, profiles, audit logs"
  echo "  • certification-evidence storage files"
  echo ""
  echo "Preserved:"
  echo "  • database schema, RLS, triggers"
  echo "  • agent_versions table"
  echo "  • agent-releases storage bucket"
  echo "  • auth.users (login accounts)"
  echo ""
  read -r -p "Type DATA-RESET to continue: " confirm
  if [[ "$confirm" != "DATA-RESET" ]]; then
    echo "Aborted."
    exit 1
  fi
fi

if [[ "$SKIP_STORAGE" -eq 0 ]]; then
  PYTHON="${ROOT}/.venv/bin/python"
  if [[ ! -x "$PYTHON" ]]; then
    PYTHON="$(command -v python3 || true)"
  fi
  if [[ -z "$PYTHON" ]]; then
    echo "ERROR: python3 required to empty certification-evidence bucket." >&2
    echo "       Use --skip-storage or install Python." >&2
    exit 1
  fi
  if [[ -z "${SUPABASE_URL:-}" || -z "${SUPABASE_SERVICE_ROLE_KEY:-}" ]]; then
    echo "ERROR: SUPABASE_URL and SUPABASE_SERVICE_ROLE_KEY required in backend-api/.env" >&2
    exit 1
  fi
  echo "→ Emptying certification-evidence bucket (agent-releases preserved)"
  "$PYTHON" "$ROOT/scripts/empty_storage_buckets.py" --bucket certification-evidence
else
  echo "→ Skipping storage wipe (--skip-storage)"
fi

echo "→ Running database/data_reset.sql"
psql "$DATABASE_URL" -v ON_ERROR_STOP=1 -f "$DATA_RESET_SQL"

echo ""
echo "Data reset complete. Schema and agent releases unchanged."
