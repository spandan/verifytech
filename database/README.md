# VerifyTech database

PostgreSQL schema for Supabase. Three SQL files — no separate migrations folder.

## Fresh project (first time)

1. Create a Supabase project.
2. Run **`schema.sql`** in **Supabase → SQL Editor**.

## Upgrade existing project (keep data)

If you already ran an older `schema.sql` and need account tables (`scan_reports`, magic-link profiles, etc.):

1. Run **`upgrade.sql`** in the SQL Editor (idempotent — safe to re-run).

Do **not** run full `schema.sql` on a populated database (it will fail on existing tables).

## Full reset (wipe app data + recreate schema)

Deletes all certificates, devices, scan sessions, storage files in app buckets, and reapplies `schema.sql`. **Does not** delete Supabase Auth users.

**Note:** Supabase does not allow `DELETE FROM storage.objects` in SQL. Bucket files are removed via the Storage API (`scripts/empty_storage_buckets.py`), which `reset-database.sh` runs automatically.

### Prerequisites

- [PostgreSQL client](https://www.postgresql.org/download/) (`psql`, `pg_dump`)
- **Direct** database connection string (not the Supabase JS API URL)

From **Supabase → Project Settings → Database → Connection string**, copy the **URI** (Session or Direct mode). Add to `backend-api/.env`:

```env
DATABASE_URL=postgresql://postgres.[PROJECT_REF]:[YOUR_PASSWORD]@db.[PROJECT_REF].supabase.co:5432/postgres
```

### Run reset script

```bash
chmod +x scripts/reset-database.sh

# Dump backup, then reset + apply schema (prompts for confirmation)
./scripts/reset-database.sh

# Skip backup
./scripts/reset-database.sh --no-dump

# Backup only
./scripts/reset-database.sh --dump-only

# Non-interactive (CI / automation)
./scripts/reset-database.sh --yes
```

Backups are written to `database/backups/verifytech-YYYYMMDD-HHMMSS.sql`.

### Manual reset (SQL Editor)

1. Empty storage first (from repo root, with venv active):

   ```bash
   python scripts/empty_storage_buckets.py
   ```

2. Run `database/reset.sql` in the SQL Editor.
3. Run `database/schema.sql` in the SQL Editor.

To upgrade without wiping data, run `database/upgrade.sql` instead of steps 2–3.

Or use only SQL (tables reset but orphaned storage files may remain):

```bash
./scripts/reset-database.sh --skip-storage --yes
```

## What gets removed vs kept

| Removed | Kept |
|--------|------|
| All app tables & enums | `auth.users` (login accounts) |
| Files in `certification-evidence` and `agent-releases` buckets | Storage bucket definitions (re-seeded) |
| Certificates, scan sessions, audit logs | Extensions (`uuid-ossp`, `pgcrypto`) |

## Files

| File | Purpose |
|------|---------|
| `schema.sql` | Full schema (fresh install or after `reset.sql`) |
| `reset.sql` | Teardown only — run before `schema.sql` |
| `upgrade.sql` | Idempotent incremental upgrade for existing databases |
| `backups/` | Auto-created SQL dumps from reset script |
