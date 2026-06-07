from datetime import datetime
from typing import Any, Optional

from pydantic import BaseModel, Field


# ─── Intake ───────────────────────────────────────────────────────────────────

class IntakeCreateRequest(BaseModel):
    device_category: str
    approximate_purchase_year: int = Field(ge=1990, le=2030)
    zip_code_or_region: str = Field(min_length=2, max_length=20)
    charger_included: Optional[str] = None
    tenant_id: Optional[str] = None


class IntakeResponse(BaseModel):
    id: str
    device_category: str
    approximate_purchase_year: int
    zip_code_or_region: str
    charger_included: Optional[str] = None
    created_at: datetime


# ─── Agents ───────────────────────────────────────────────────────────────────

class AgentVersionResponse(BaseModel):
    platform: str
    version: str
    download_url: str
    checksum: str
    release_notes: Optional[str] = None
    minimum_supported_schema_version: str


class AgentDownloadResponse(BaseModel):
    platform: str
    version: str
    download_url: str
    checksum: str
    full_download_url: str


# ─── Reports ──────────────────────────────────────────────────────────────────

class ReportSubmitRequest(BaseModel):
    report: dict[str, Any]
    report_type: str = "initial_certification"
    intake_id: Optional[str] = None
    tenant_id: Optional[str] = None
    owner_user_id: Optional[str] = None


class ReportSubmitResponse(BaseModel):
    report_id: str
    device_id: str
    identity_hash: str
    report_hash: str
    schema_valid: bool
    tier1_complete: bool
    tier2_complete: bool
    certificate_id: Optional[str] = None
    certificate_code: Optional[str] = None
    public_url: Optional[str] = None


class AgentCertifyRequest(BaseModel):
    schema_version: str = "1.0"
    platform: str = "windows"
    collection_context: dict[str, Any] = Field(default_factory=dict)
    tier1_certification_identity: dict[str, Any] = Field(default_factory=dict)
    tier2_value_determination: dict[str, Any] = Field(default_factory=dict)
    tier3_optional_intelligence: dict[str, Any] = Field(default_factory=dict)
    agent_metadata: dict[str, Any] = Field(default_factory=dict)


class AgentCertifyResponse(BaseModel):
    certificate_code: str
    certificate_url: str
    certificate_level: str
    status: str
    message: Optional[str] = None


class AgentVerifyRequest(AgentCertifyRequest):
    pass


class AgentVerifyResponse(BaseModel):
    result: str
    message: str
    changes: list[dict[str, Any]] = Field(default_factory=list)
    attempt_id: Optional[str] = None
    verification_url: Optional[str] = None
    identity_match_score: Optional[float] = None
    value_match_score: Optional[float] = None


# ─── Certificates ─────────────────────────────────────────────────────────────

class CertificatePublicResponse(BaseModel):
    certificate_code: str
    device_name: str
    manufacturer: str
    model: str
    device_type: str
    platform: str
    certificate_level: str
    status: str
    condition_grade: Optional[str] = None
    certification_date: datetime
    expires_at: datetime
    battery_health_percent: Optional[float] = None
    storage_health_percent: Optional[float] = None
    core_tests_passed: list[str] = Field(default_factory=list)
    core_tests_total: int = 7
    verification_url: str
    qr_code_payload: str
    public_url: str
    inspection_report: Optional[dict[str, Any]] = None
    agent_provenance: Optional[dict[str, Any]] = None


class CertificateLookupResponse(BaseModel):
    exists: bool
    certificate_code: str
    status: Optional[str] = None
    device_name: Optional[str] = None
    expires_at: Optional[datetime] = None


# ─── Scan sessions (Windows agent) ────────────────────────────────────────────

class ScanSessionStartRequest(BaseModel):
    agent_version: str = Field(min_length=1, max_length=32)
    platform: str = "windows"
    build_channel: str = "production"
    account_link_token: Optional[str] = None
    notification_email: Optional[str] = None


class ScanSessionStartResponse(BaseModel):
    session_id: str
    nonce: str
    expires_at: datetime


class EvidenceArtifactUpload(BaseModel):
    artifact_type: str
    content_type: str = "application/octet-stream"
    content_base64: str
    collected_at: Optional[str] = None
    source: Optional[str] = None


class ScanSessionSubmitRequest(BaseModel):
    session_id: str
    nonce: str
    agent_version: str
    platform: str = "windows"
    scan_started_at: datetime
    scan_completed_at: datetime
    admin_mode: bool = False
    hardware_fingerprint: str = Field(min_length=16, max_length=128)
    scan_data: dict[str, Any]
    evidence_artifacts: list[EvidenceArtifactUpload] = Field(default_factory=list)


class ScanSessionSubmitResponse(BaseModel):
    certificate_id: str
    certificate_code: str
    report_url: str
    verification_url: Optional[str] = None
    qr_code_url: Optional[str] = None
    scan_report_id: Optional[str] = None
    public_report_token: Optional[str] = None


class ScanPairingCreateRequest(BaseModel):
    business_id: Optional[str] = Field(default=None, alias="businessId")

    model_config = {"populate_by_name": True}


class ScanPairingCreateResponse(BaseModel):
    pairing_code: str = Field(alias="pairingCode")
    expires_at: datetime = Field(alias="expiresAt")
    deep_link: str = Field(alias="deepLink")

    model_config = {"populate_by_name": True}


class ScanPairingExchangeRequest(BaseModel):
    pairing_code: str = Field(min_length=8, max_length=32, alias="pairingCode")
    device_fingerprint: str = Field(min_length=16, max_length=128, alias="deviceFingerprint")
    agent_version: str = Field(min_length=1, max_length=32, alias="agentVersion")

    model_config = {"populate_by_name": True}


class ScanPairingExchangeResponse(BaseModel):
    upload_token: str = Field(alias="uploadToken")
    expires_in_seconds: int = Field(alias="expiresInSeconds")
    scan_session_id: str = Field(alias="scanSessionId")
    linked_account_name: Optional[str] = Field(default=None, alias="linkedAccountName")

    model_config = {"populate_by_name": True}


class ScanUploadRequest(BaseModel):
    session_id: str
    agent_version: str
    platform: str = "windows"
    scan_started_at: datetime
    scan_completed_at: datetime
    admin_mode: bool = False
    hardware_fingerprint: str = Field(min_length=16, max_length=128)
    scan_data: dict[str, Any]
    evidence_artifacts: list[EvidenceArtifactUpload] = Field(default_factory=list)


class CertificationSessionCreateRequest(BaseModel):
    expected_device_type: str = Field(default="laptop", alias="expectedDeviceType")
    business_id: Optional[str] = Field(default=None, alias="businessId")

    model_config = {"populate_by_name": True}


class CertificationSessionCreateResponse(BaseModel):
    session_id: str
    token: str
    expires_at: datetime
    expires_in_seconds: int
    deep_link: str
    expected_device_type: str


class CertificationSessionValidateRequest(BaseModel):
    token: str = Field(min_length=16)


class CertificationSessionValidateResponse(BaseModel):
    session_id: str
    user_id: str
    expected_device_type: str
    linked_account_name: Optional[str] = None


class CertificationSessionBeginScanRequest(BaseModel):
    token: str = Field(min_length=16)
    device_fingerprint: str = Field(min_length=16, max_length=128)
    agent_version: str = Field(min_length=1, max_length=32)


class CertificationSessionBeginScanResponse(BaseModel):
    upload_token: str
    expires_in_seconds: int
    scan_session_id: str
    linked_account_name: Optional[str] = None


class AgentPairingCreateRequest(BaseModel):
    device_nonce: str = Field(min_length=8, max_length=128)


class AgentPairingCreateResponse(BaseModel):
    pairing_code: str
    expires_in_minutes: int
    expires_at: datetime


class AgentPairingClaimRequest(BaseModel):
    pairing_code: str = Field(min_length=6, max_length=6)


class AgentPairingClaimResponse(BaseModel):
    pairing_code: str
    session_id: str
    user_id: str
    message: str


class AgentPairingClaimStatusResponse(BaseModel):
    connected: bool
    pairing_code: str
    message: str


class AgentPairingStatusResponse(BaseModel):
    status: str
    user_id: Optional[str] = None
    session_id: Optional[str] = None
    certification_token: Optional[str] = None
    expires_at: Optional[datetime] = None


# ─── Verification ─────────────────────────────────────────────────────────────

class VerifyLookupRequest(BaseModel):
    certificate_code: str


class VerifySubmitRequest(BaseModel):
    certificate_code: str
    report: dict[str, Any]


class VerifySubmitResponse(BaseModel):
    attempt_id: str
    result: str
    identity_match_score: float
    value_match_score: float
    summary: str
    changes: list[dict[str, Any]] = Field(default_factory=list)
    value_estimate_invalidated: bool = False
    certificate_code: str
    device_name: Optional[str] = None


class VerificationAttemptResponse(BaseModel):
    attempt_id: str
    result: str
    identity_match_score: float
    value_match_score: float
    summary: str
    changes: list[dict[str, Any]] = Field(default_factory=list)
    value_estimate_invalidated: bool = False
    certificate_code: Optional[str] = None
    device_name: Optional[str] = None
    created_at: datetime


# ─── Dashboard ────────────────────────────────────────────────────────────────

class DashboardCertificateSummary(BaseModel):
    id: str
    certificate_code: str
    device_name: str
    certificate_level: str
    status: str
    condition_grade: Optional[str] = None
    issued_at: datetime
    expires_at: datetime
    public_url: str


class DashboardResponse(BaseModel):
    user_id: Optional[str] = None
    certificates: list[DashboardCertificateSummary] = Field(default_factory=list)
    verification_count: int = 0


# ─── Tenants ──────────────────────────────────────────────────────────────────

class TenantResponse(BaseModel):
    id: str
    name: str
    slug: str
    role: Optional[str] = None


# ─── Auth Profile ─────────────────────────────────────────────────────────────

class AuthProfileResponse(BaseModel):
    id: str
    email: Optional[str] = None
    full_name: Optional[str] = None
    tenants: list[TenantResponse] = Field(default_factory=list)


# ─── Account / My Laptops ────────────────────────────────────────────────────

class ScanLinkTokenResponse(BaseModel):
    token: str
    expires_at: datetime


class MyLaptopSummary(BaseModel):
    device_id: str
    nickname: Optional[str] = None
    device_name: str
    manufacturer: Optional[str] = None
    model: Optional[str] = None
    serial_last4: Optional[str] = None
    last_scan_at: Optional[datetime] = None
    verification_status: str
    verification_code: str
    public_report_url: str
    report_token: Optional[str] = None


class MyLaptopsResponse(BaseModel):
    laptops: list[MyLaptopSummary] = Field(default_factory=list)


class RenameDeviceRequest(BaseModel):
    nickname: str = Field(min_length=1, max_length=80)


class ClaimReportRequest(BaseModel):
    verification_code: str = Field(min_length=4, max_length=20)


class ClaimReportResponse(BaseModel):
    device_id: str
    verification_code: str
    public_report_url: str
    message: str


class SendReportEmailRequest(BaseModel):
    verification_code: str = Field(min_length=4, max_length=20)
    email: str = Field(min_length=3, max_length=320)


class PublicScanReportResponse(BaseModel):
    verification_code: str
    public_report_url: str
    report_summary: Optional[dict[str, Any]] = None
    certificate: Optional[CertificatePublicResponse] = None
