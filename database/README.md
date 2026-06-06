# Certronx database

PostgreSQL schema for Supabase. **`schema.sql` is the canonical full definition.**

## Fresh project (first time)

1. Create a Supabase project.
2. Run **`schema.sql`** in **Supabase → SQL Editor**.

## Production data reset (keep schema + agent releases)

Use when promoting an environment to production — wipe staging/test **data** without rebuilding schema or re-uploading the Windows agent.

**SQL only** (paste in Supabase SQL Editor):

```bash
database/data_reset.sql
```

**Recommended** (also clears `certification-evidence` storage; preserves `agent-releases`):

```bash
chmod +x scripts/data-reset-prod.sh
./scripts/data-reset-prod.sh
```

| Cleared | Preserved |
|--------|-----------|
| Certificates, devices, scans, tenants, profiles, audit logs | Schema, RLS, triggers |
| `certification-evidence` storage files | `agent-releases` bucket + `agent_versions` table |
| | `auth.users` (profiles recreated on next sign-in) |

Supabase blocks `DELETE FROM storage.objects` in SQL — evidence files are removed via `scripts/empty_storage_buckets.py --bucket certification-evidence`.

## Full reset (wipe app data + recreate schema)

Drops all app tables and enums, empties **both** storage buckets, and reapplies `schema.sql`. **Does not** delete Supabase Auth users.

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

Or use only SQL (tables reset but orphaned storage files may remain):

```bash
./scripts/reset-database.sh --skip-storage --yes
```

## What gets removed vs kept (full reset)

| Removed | Kept |
|--------|------|
| All app tables & enums | `auth.users` (login accounts) |
| Files in `certification-evidence` and `agent-releases` buckets | Storage bucket definitions (re-seeded) |
| Certificates, scan sessions, audit logs | Extensions (`uuid-ossp`, `pgcrypto`) |

## Files

| File | Purpose |
|------|---------|
| `schema.sql` | **Canonical** full schema (fresh install or after `reset.sql`) |
| `migrations/` | Incremental ALTER scripts for existing databases |
| `reset.sql` | Teardown only — run before `schema.sql` |
| `data_reset.sql` | Clear application data only (prod promotion; keeps schema + agent releases) |
| `backups/` | Auto-created SQL dumps from reset script |
