-- Certronx / VerifyTech — clear application DATA for production promotion
-- Keeps schema, RLS, triggers, agent_versions rows, and auth.users intact.
-- Does NOT empty the agent-releases storage bucket.
--
-- Run in Supabase SQL Editor (or via psql) when staging/test data should be wiped
-- before treating the environment as production.
--
-- Storage: Supabase blocks DELETE on storage.objects in SQL. After this script,
-- empty certification evidence only (agent-releases is preserved):
--
--   python scripts/empty_storage_buckets.py --bucket certification-evidence
--
-- Optional backup first:
--   pg_dump "$DATABASE_URL" --schema=public --no-owner -f database/backups/pre-prod-$(date +%Y%m%d).sql

BEGIN;

-- Detach profile → tenant links before tenant wipe
UPDATE profiles SET default_tenant_id = NULL WHERE default_tenant_id IS NOT NULL;

TRUNCATE TABLE
    report_claims,
    scan_reports,
    account_scan_link_tokens,
    scan_sessions,
    scan_pairing_sessions,
    certificate_events,
    verification_attempts,
    certificates,
    device_reports,
    intake_responses,
    audit_logs,
    devices,
    tenant_locations,
    tenant_users,
    tenants,
    profiles
RESTART IDENTITY CASCADE;

COMMIT;

-- Preserved intentionally:
--   • agent_versions (Windows agent release metadata)
--   • storage bucket agent-releases (binaries — wipe separately if ever needed)
--   • auth.users (Supabase login accounts; profiles recreated on next sign-in)
