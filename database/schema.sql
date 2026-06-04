-- DevicePassport / VerifyTech — complete Supabase schema
-- Run once in the Supabase SQL editor on a fresh project.
-- Tenant-aware: tenant_id nullable for consumer/anonymous flows

-- ─── Extensions ─────────────────────────────────────────────────────────────
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- ─── Profiles (extends Supabase auth.users) ───────────────────────────────
CREATE TABLE profiles (
    id UUID PRIMARY KEY REFERENCES auth.users(id) ON DELETE CASCADE,
    email TEXT,
    full_name TEXT,
    avatar_url TEXT,
    default_tenant_id UUID,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ─── Tenants ──────────────────────────────────────────────────────────────
CREATE TABLE tenants (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name TEXT NOT NULL,
    slug TEXT NOT NULL UNIQUE,
    branding_json JSONB DEFAULT '{}',
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

ALTER TABLE profiles
    ADD CONSTRAINT fk_profiles_default_tenant
    FOREIGN KEY (default_tenant_id) REFERENCES tenants(id) ON DELETE SET NULL;

-- ─── Tenant Users ─────────────────────────────────────────────────────────
CREATE TYPE tenant_role AS ENUM ('owner', 'admin', 'technician', 'viewer');

CREATE TABLE tenant_users (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    user_id UUID NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
    role tenant_role NOT NULL DEFAULT 'viewer',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (tenant_id, user_id)
);

-- ─── Tenant Locations ─────────────────────────────────────────────────────
CREATE TABLE tenant_locations (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    name TEXT NOT NULL,
    address_json JSONB DEFAULT '{}',
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ─── Devices ──────────────────────────────────────────────────────────────
CREATE TABLE devices (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID REFERENCES tenants(id) ON DELETE SET NULL,
    owner_user_id UUID REFERENCES profiles(id) ON DELETE SET NULL,
    identity_hash TEXT NOT NULL,
    manufacturer TEXT,
    model TEXT,
    device_type TEXT,
    platform TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_devices_identity_hash ON devices(identity_hash);
CREATE INDEX idx_devices_tenant_id ON devices(tenant_id);

-- ─── Device Reports ───────────────────────────────────────────────────────
CREATE TYPE report_type AS ENUM (
    'initial_certification',
    'buyer_verification',
    'recertification'
);

CREATE TABLE device_reports (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    device_id UUID NOT NULL REFERENCES devices(id) ON DELETE CASCADE,
    tenant_id UUID REFERENCES tenants(id) ON DELETE SET NULL,
    report_type report_type NOT NULL,
    schema_version TEXT NOT NULL DEFAULT '1.0.0',
    platform TEXT NOT NULL,
    tier1_json JSONB NOT NULL DEFAULT '{}',
    tier2_json JSONB NOT NULL DEFAULT '{}',
    tier3_json JSONB NOT NULL DEFAULT '{}',
    raw_report_json JSONB NOT NULL DEFAULT '{}',
    identity_hash TEXT NOT NULL,
    value_hash TEXT,
    report_hash TEXT NOT NULL,
    collector_version TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_device_reports_device_id ON device_reports(device_id);
CREATE INDEX idx_device_reports_identity_hash ON device_reports(identity_hash);

-- ─── Certificates ─────────────────────────────────────────────────────────
CREATE TYPE certificate_level AS ENUM (
    'identity_verified',
    'condition_certified',
    'enhanced_certified'
);

CREATE TYPE certificate_status AS ENUM (
    'active',
    'expired',
    'revoked',
    'superseded'
);

CREATE TABLE certificates (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    certificate_code TEXT NOT NULL UNIQUE,
    device_id UUID NOT NULL REFERENCES devices(id) ON DELETE CASCADE,
    tenant_id UUID REFERENCES tenants(id) ON DELETE SET NULL,
    initial_report_id UUID NOT NULL REFERENCES device_reports(id),
    certificate_level certificate_level NOT NULL,
    status certificate_status NOT NULL DEFAULT 'active',
    condition_grade TEXT,
    value_score NUMERIC(5, 2),
    issued_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    expires_at TIMESTAMPTZ NOT NULL,
    public_url TEXT,
    qr_code_payload TEXT,
    public_payload_json JSONB DEFAULT '{}',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_certificates_code ON certificates(certificate_code);
CREATE INDEX idx_certificates_device_id ON certificates(device_id);
CREATE INDEX idx_certificates_tenant_id ON certificates(tenant_id);

-- ─── Verification Attempts ────────────────────────────────────────────────
CREATE TYPE verification_result AS ENUM (
    'CERTIFIED_MATCH',
    'CERTIFIED_WITH_CHANGES',
    'DEVICE_MISMATCH',
    'CERTIFICATE_NOT_FOUND',
    'CERTIFICATE_EXPIRED',
    'CERTIFICATE_REVOKED'
);

CREATE TABLE verification_attempts (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    certificate_id UUID REFERENCES certificates(id) ON DELETE SET NULL,
    verification_report_id UUID REFERENCES device_reports(id) ON DELETE SET NULL,
    tenant_id UUID REFERENCES tenants(id) ON DELETE SET NULL,
    result verification_result NOT NULL,
    identity_match_score NUMERIC(5, 4),
    value_match_score NUMERIC(5, 4),
    changes_json JSONB DEFAULT '{}',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_verification_attempts_certificate_id ON verification_attempts(certificate_id);

-- ─── Agent Versions ───────────────────────────────────────────────────────
CREATE TABLE agent_versions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    platform TEXT NOT NULL,
    version TEXT NOT NULL,
    download_url TEXT NOT NULL,
    checksum TEXT NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    release_notes TEXT,
    minimum_supported_schema_version TEXT NOT NULL DEFAULT '1.0.0',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (platform, version)
);

-- agent_versions.download_url formats:
--   windows/0.1.0/DeviceCertAgent.exe  → signed URL from GET /api/agents/{platform}
--   /agents/DeviceCertAgent.exe        → API fallback (local dev)
--   https://...                        → external CDN URL

-- ─── Intake Responses ─────────────────────────────────────────────────────
CREATE TYPE device_category AS ENUM (
    'personal',
    'business',
    'school',
    'refurbished',
    'unknown'
);

CREATE TYPE charger_included AS ENUM ('yes', 'no', 'not_sure');

CREATE TABLE intake_responses (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    user_id UUID REFERENCES profiles(id) ON DELETE SET NULL,
    tenant_id UUID REFERENCES tenants(id) ON DELETE SET NULL,
    device_category device_category NOT NULL,
    approximate_purchase_year INTEGER NOT NULL,
    zip_code_or_region TEXT NOT NULL,
    charger_included charger_included,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ─── Certificate Events ───────────────────────────────────────────────────
CREATE TABLE certificate_events (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    certificate_id UUID NOT NULL REFERENCES certificates(id) ON DELETE CASCADE,
    tenant_id UUID REFERENCES tenants(id) ON DELETE SET NULL,
    event_type TEXT NOT NULL,
    event_data JSONB DEFAULT '{}',
    actor_user_id UUID REFERENCES profiles(id) ON DELETE SET NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- ─── Audit Logs ───────────────────────────────────────────────────────────
CREATE TABLE audit_logs (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID REFERENCES tenants(id) ON DELETE SET NULL,
    actor_user_id UUID REFERENCES profiles(id) ON DELETE SET NULL,
    action TEXT NOT NULL,
    resource_type TEXT NOT NULL,
    resource_id UUID,
    metadata_json JSONB DEFAULT '{}',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_audit_logs_tenant_id ON audit_logs(tenant_id);
CREATE INDEX idx_audit_logs_resource ON audit_logs(resource_type, resource_id);

-- ─── Agent scan sessions (short-lived, nonce-bound) ───────────────────────
CREATE TYPE scan_session_status AS ENUM ('started', 'submitted', 'expired', 'rejected');

CREATE TABLE scan_sessions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    session_id TEXT NOT NULL UNIQUE,
    nonce TEXT NOT NULL,
    platform TEXT NOT NULL DEFAULT 'windows',
    agent_version TEXT NOT NULL,
    build_channel TEXT NOT NULL DEFAULT 'production',
    status scan_session_status NOT NULL DEFAULT 'started',
    expires_at TIMESTAMPTZ NOT NULL,
    submitted_at TIMESTAMPTZ,
    admin_mode BOOLEAN,
    scan_started_at TIMESTAMPTZ,
    scan_completed_at TIMESTAMPTZ,
    hardware_fingerprint TEXT,
    scan_data_json JSONB,
    certificate_id UUID REFERENCES certificates(id) ON DELETE SET NULL,
    certificate_code TEXT,
    report_url TEXT,
    verification_url TEXT,
    qr_code_url TEXT,
    rejection_reason TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_scan_sessions_status ON scan_sessions(status);
CREATE INDEX idx_scan_sessions_expires_at ON scan_sessions(expires_at);

-- ─── Seed default Windows agent placeholder ───────────────────────────────
INSERT INTO agent_versions (platform, version, download_url, checksum, is_active, release_notes)
VALUES (
    'windows',
    '1.0.0',
    'windows/0.1.0/DeviceCertAgent.exe',
    'sha256:placeholder-checksum-replace-on-release',
    TRUE,
    'Initial POC Windows agent placeholder'
);

-- ─── Row Level Security ───────────────────────────────────────────────────
ALTER TABLE profiles ENABLE ROW LEVEL SECURITY;
ALTER TABLE tenants ENABLE ROW LEVEL SECURITY;
ALTER TABLE tenant_users ENABLE ROW LEVEL SECURITY;
ALTER TABLE devices ENABLE ROW LEVEL SECURITY;
ALTER TABLE device_reports ENABLE ROW LEVEL SECURITY;
ALTER TABLE certificates ENABLE ROW LEVEL SECURITY;
ALTER TABLE verification_attempts ENABLE ROW LEVEL SECURITY;
ALTER TABLE intake_responses ENABLE ROW LEVEL SECURITY;
ALTER TABLE scan_sessions ENABLE ROW LEVEL SECURITY;

-- Public read on active certificates via certificate_code (handled by backend API)
-- Service role used by backend-api for all writes

-- ─── Agent release storage (Supabase Storage) ─────────────────────────────
-- Private bucket — downloads require signed URLs issued by backend-api

INSERT INTO storage.buckets (id, name, public, file_size_limit)
VALUES (
    'agent-releases',
    'agent-releases',
    FALSE,
    209715200  -- 200 MB
)
ON CONFLICT (id) DO UPDATE
SET public = EXCLUDED.public,
    file_size_limit = EXCLUDED.file_size_limit;

DROP POLICY IF EXISTS "Public read agent releases" ON storage.objects;
DROP POLICY IF EXISTS "Authenticated upload agent releases" ON storage.objects;
DROP POLICY IF EXISTS "Authenticated update agent releases" ON storage.objects;

CREATE POLICY "Authenticated upload agent releases"
ON storage.objects
FOR INSERT
TO authenticated
WITH CHECK (bucket_id = 'agent-releases');

CREATE POLICY "Authenticated update agent releases"
ON storage.objects
FOR UPDATE
TO authenticated
USING (bucket_id = 'agent-releases')
WITH CHECK (bucket_id = 'agent-releases');
