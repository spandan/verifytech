#!/usr/bin/env bash
# Reset VerifyTech database: optional dump → teardown → fresh schema.
#
# Requires: psql, pg_dump (for dumps)
# Requires: DATABASE_URL — Supabase direct Postgres connection string
#   Supabase → Project Settings → Database → Connection string → URI
#   Example: postgresql://postgres.[ref]:[PASSWORD]@aws-0-us-east-1.pooler.supabase.com:5432/postgres
#
# Usage:
#   export DATABASE_URL='postgresql://...'
#   ./scripts/reset-database.sh              # dump + empty storage + reset + schema
#   ./scripts/reset-database.sh --no-dump    # empty storage + reset + schema
#   ./scripts/reset-database.sh --dump-only  # dump only, no changes
#   ./scripts/reset-database.sh --yes        # skip confirmation prompt
#   ./scripts/reset-database.sh --skip-storage  # SQL only (no Storage API wipe)
#
# Storage buckets are emptied via scripts/empty_storage_buckets.py (requires
# SUPABASE_URL + SUPABASE_SERVICE_ROLE_KEY in backend-api/.env).

set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
RESET_SQL="$ROOT/database/reset.sql"
SCHEMA_SQL="$ROOT/database/schema.sql"
BACKUP_DIR="$ROOT/database/backups"
ENV_FILE="$ROOT/backend-api/.env"

DUMP=1
SKIP_CONFIRM=0
DUMP_ONLY=0
SKIP_STORAGE=0

for arg in "$@"; do
  case "$arg" in
    --no-dump) DUMP=0 ;;
    --yes|-y) SKIP_CONFIRM=1 ;;
    --dump-only) DUMP_ONLY=1; DUMP=1 ;;
    --skip-storage) SKIP_STORAGE=1 ;;
    -h|--help)
      sed -n '2,14p' "$0"
      exit 0
      ;;
    *)
      echo "Unknown option: $arg (use --help)" >&2
      exit 1
      ;;
  esac
done

# Load DATABASE_URL from backend-api/.env if not already set
if [[ -z "${DATABASE_URL:-}" && -f "$ENV_FILE" ]]; then
  # shellcheck disable=SC1090
  set -a
  source "$ENV_FILE"
  set +a
fi

if [[ -z "${DATABASE_URL:-}" ]]; then
  echo "ERROR: DATABASE_URL is not set." >&2
  echo "" >&2
  echo "Add to backend-api/.env or export before running:" >&2
  echo "  DATABASE_URL=postgresql://postgres.[PROJECT_REF]:[PASSWORD]@...supabase.com:5432/postgres" >&2
  echo "" >&2
  echo "Find it in Supabase → Project Settings → Database → Connection string (URI)." >&2
  exit 1
fi

if ! command -v psql >/dev/null 2>&1; then
  echo "ERROR: psql not found. Install PostgreSQL client tools." >&2
  exit 1
fi

mkdir -p "$BACKUP_DIR"
STAMP="$(date +%Y%m%d-%H%M%S)"
DUMP_FILE="$BACKUP_DIR/verifytech-${STAMP}.sql"

if [[ "$DUMP" -eq 1 ]]; then
  if ! command -v pg_dump >/dev/null 2>&1; then
    echo "ERROR: pg_dump not found (required for dumps). Use --no-dump to skip." >&2
    exit 1
  fi
  echo "→ Dumping database to $DUMP_FILE"
  pg_dump "$DATABASE_URL" \
    --no-owner \
    --no-privileges \
    --schema=public \
    --file="$DUMP_FILE"
  echo "  Dump saved ($(du -h "$DUMP_FILE" | cut -f1))"
fi

if [[ "$DUMP_ONLY" -eq 1 ]]; then
  echo "Done (--dump-only; no reset applied)."
  exit 0
fi

if [[ "$SKIP_CONFIRM" -ne 1 ]]; then
  echo ""
  echo "WARNING: This will DELETE all application data:"
  echo "  • certificates, devices, reports, scan sessions"
  echo "  • evidence and agent files in storage buckets"
  echo "  • tenants, audit logs, agent_versions seed row"
  echo ""
  echo "Auth users (Supabase login accounts) are NOT removed."
  echo ""
  read -r -p "Type RESET to continue: " confirm
  if [[ "$confirm" != "RESET" ]]; then
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
    echo "ERROR: python3 required to empty storage buckets. Use --skip-storage or install Python." >&2
    exit 1
  fi
  if [[ -z "${SUPABASE_URL:-}" || -z "${SUPABASE_SERVICE_ROLE_KEY:-}" ]]; then
    echo "ERROR: SUPABASE_URL and SUPABASE_SERVICE_ROLE_KEY required in backend-api/.env" >&2
    echo "       (to empty storage via API). Or run with --skip-storage." >&2
    exit 1
  fi
  echo "→ Emptying storage buckets (Storage API)"
  "$PYTHON" "$ROOT/scripts/empty_storage_buckets.py"
else
  echo "→ Skipping storage wipe (--skip-storage)"
fi

echo "→ Running teardown (database/reset.sql)"
psql "$DATABASE_URL" -v ON_ERROR_STOP=1 -f "$RESET_SQL"

echo "→ Applying fresh schema (database/schema.sql)"
psql "$DATABASE_URL" -v ON_ERROR_STOP=1 -f "$SCHEMA_SQL"

echo ""
echo "Database reset complete."
echo "  Schema: database/schema.sql"
if [[ "$DUMP" -eq 1 ]]; then
  echo "  Backup: $DUMP_FILE"
fi
echo ""
echo "Next steps:"
echo "  • Re-upload agent binary if needed: python scripts/upload_agent.py"
echo "  • Update agent_versions row with real checksum after release"
