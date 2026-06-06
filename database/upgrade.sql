-- VerifyTech — incremental upgrade for existing Supabase projects
-- Safe to re-run (idempotent). Does NOT replace schema.sql on fresh projects.
--
-- Use when you already ran an older schema.sql and need account / scan_reports tables.
-- Fresh project? Run schema.sql only. Full wipe? reset.sql then schema.sql.

-- ─── Device account fields ───────────────────────────────────────────────────
ALTER TABLE devices ADD COLUMN IF NOT EXISTS nickname TEXT;
ALTER TABLE devices ADD COLUMN IF NOT EXISTS serial_hash TEXT;
ALTER TABLE devices ADD COLUMN IF NOT EXISTS serial_last4 TEXT;

CREATE INDEX IF NOT EXISTS idx_devices_owner_user_id ON devices(owner_user_id);

-- ─── Scan reports (account layer on top of certificates) ───────────────────
CREATE TABLE IF NOT EXISTS scan_reports (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    device_id UUID REFERENCES devices(id) ON DELETE SET NULL,
    user_id UUID REFERENCES profiles(id) ON DELETE SET NULL,
    certificate_id UUID REFERENCES certificates(id) ON DELETE SET NULL,
    device_report_id UUID REFERENCES device_reports(id) ON DELETE SET NULL,
    verification_code TEXT NOT NULL UNIQUE,
    public_report_token TEXT NOT NULL UNIQUE,
    scan_payload JSONB NOT NULL DEFAULT '{}',
    report_summary JSONB,
    status TEXT NOT NULL DEFAULT 'completed',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_scan_reports_user_id ON scan_reports(user_id);
CREATE INDEX IF NOT EXISTS idx_scan_reports_device_id ON scan_reports(device_id);
CREATE INDEX IF NOT EXISTS idx_scan_reports_public_token ON scan_reports(public_report_token);

-- ─── Report claims (anonymous → account) ─────────────────────────────────────
CREATE TABLE IF NOT EXISTS report_claims (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    scan_report_id UUID NOT NULL REFERENCES scan_reports(id) ON DELETE CASCADE,
    user_id UUID NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
    claimed_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (scan_report_id, user_id)
);

CREATE INDEX IF NOT EXISTS idx_report_claims_user_id ON report_claims(user_id);

-- ─── Scan session account linking ────────────────────────────────────────────
ALTER TABLE scan_sessions ADD COLUMN IF NOT EXISTS user_id UUID REFERENCES profiles(id) ON DELETE SET NULL;
ALTER TABLE scan_sessions ADD COLUMN IF NOT EXISTS notification_email TEXT;

CREATE TABLE IF NOT EXISTS account_scan_link_tokens (
    token TEXT PRIMARY KEY,
    user_id UUID NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
    expires_at TIMESTAMPTZ NOT NULL,
    used_at TIMESTAMPTZ,
    scan_session_id TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_account_scan_link_tokens_user_id ON account_scan_link_tokens(user_id);

-- ─── Auto-create profile on Supabase Auth signup ─────────────────────────────
CREATE OR REPLACE FUNCTION public.handle_new_user()
RETURNS TRIGGER
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = public
AS $$
BEGIN
    INSERT INTO public.profiles (id, email, created_at, updated_at)
    VALUES (NEW.id, NEW.email, NOW(), NOW())
    ON CONFLICT (id) DO UPDATE
    SET email = COALESCE(EXCLUDED.email, profiles.email),
        updated_at = NOW();
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS on_auth_user_created ON auth.users;
CREATE TRIGGER on_auth_user_created
    AFTER INSERT ON auth.users
    FOR EACH ROW EXECUTE FUNCTION public.handle_new_user();

-- ─── Row Level Security ──────────────────────────────────────────────────────
ALTER TABLE scan_reports ENABLE ROW LEVEL SECURITY;
ALTER TABLE report_claims ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "Users read own devices" ON devices;
CREATE POLICY "Users read own devices"
    ON devices FOR SELECT TO authenticated
    USING (owner_user_id = auth.uid());

DROP POLICY IF EXISTS "Users update own devices" ON devices;
CREATE POLICY "Users update own devices"
    ON devices FOR UPDATE TO authenticated
    USING (owner_user_id = auth.uid())
    WITH CHECK (owner_user_id = auth.uid());

DROP POLICY IF EXISTS "Users read own scan reports" ON scan_reports;
CREATE POLICY "Users read own scan reports"
    ON scan_reports FOR SELECT TO authenticated
    USING (user_id = auth.uid());

DROP POLICY IF EXISTS "Users read own report claims" ON report_claims;
CREATE POLICY "Users read own report claims"
    ON report_claims FOR SELECT TO authenticated
    USING (user_id = auth.uid());
