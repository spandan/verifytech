-- VerifyTech / DevicePassport — full teardown of application schema
-- WARNING: Deletes all certificates, devices, scan sessions, audit logs, and storage objects
-- in app buckets. Does NOT delete auth.users (Supabase Auth accounts remain).
--
-- Run via: scripts/reset-database.sh
-- Or paste into Supabase SQL Editor before re-running schema.sql

-- ─── Auth trigger (recreated by schema.sql) ────────────────────────────────
DROP TRIGGER IF EXISTS on_auth_user_created ON auth.users;
DROP FUNCTION IF EXISTS public.handle_new_user();

-- ─── Row Level Security policies (recreated by schema.sql) ────────────────
DROP POLICY IF EXISTS "Users read own report claims" ON report_claims;
DROP POLICY IF EXISTS "Users read own scan reports" ON scan_reports;
DROP POLICY IF EXISTS "Users update own devices" ON devices;
DROP POLICY IF EXISTS "Users read own devices" ON devices;

-- ─── Storage policies (recreated by schema.sql) ───────────────────────────
DROP POLICY IF EXISTS "Authenticated upload certification evidence" ON storage.objects;
DROP POLICY IF EXISTS "Authenticated upload agent releases" ON storage.objects;
DROP POLICY IF EXISTS "Authenticated update agent releases" ON storage.objects;
DROP POLICY IF EXISTS "Public read agent releases" ON storage.objects;

-- Storage files cannot be deleted via SQL on Supabase (storage.protect_delete).
-- Empty buckets first with: python scripts/empty_storage_buckets.py
-- (reset-database.sh runs this automatically)

-- ─── Application tables (dependency order) ────────────────────────────────
DROP TABLE IF EXISTS report_claims CASCADE;
DROP TABLE IF EXISTS scan_reports CASCADE;
DROP TABLE IF EXISTS account_scan_link_tokens CASCADE;
DROP TABLE IF EXISTS scan_sessions CASCADE;
DROP TABLE IF EXISTS scan_pairing_sessions CASCADE;
DROP TABLE IF EXISTS certificate_events CASCADE;
DROP TABLE IF EXISTS verification_attempts CASCADE;
DROP TABLE IF EXISTS certificates CASCADE;
DROP TABLE IF EXISTS device_reports CASCADE;
DROP TABLE IF EXISTS intake_responses CASCADE;
DROP TABLE IF EXISTS audit_logs CASCADE;
DROP TABLE IF EXISTS agent_versions CASCADE;
DROP TABLE IF EXISTS devices CASCADE;
DROP TABLE IF EXISTS tenant_locations CASCADE;
DROP TABLE IF EXISTS tenant_users CASCADE;
DROP TABLE IF EXISTS tenants CASCADE;
DROP TABLE IF EXISTS profiles CASCADE;

-- ─── Custom enums ─────────────────────────────────────────────────────────
DROP TYPE IF EXISTS scan_pairing_status CASCADE;
DROP TYPE IF EXISTS scan_session_status CASCADE;
DROP TYPE IF EXISTS charger_included CASCADE;
DROP TYPE IF EXISTS device_category CASCADE;
DROP TYPE IF EXISTS verification_result CASCADE;
DROP TYPE IF EXISTS certificate_status CASCADE;
DROP TYPE IF EXISTS certificate_level CASCADE;
DROP TYPE IF EXISTS report_type CASCADE;
DROP TYPE IF EXISTS tenant_role CASCADE;
