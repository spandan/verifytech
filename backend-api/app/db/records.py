"""Database row types returned from Supabase."""

from __future__ import annotations

from dataclasses import dataclass, field
from datetime import datetime
from typing import Any


def _parse_dt(value: Any) -> datetime | None:
    if value is None:
        return None
    if isinstance(value, datetime):
        return value
    text = str(value).replace("Z", "+00:00")
    return datetime.fromisoformat(text)


def _parse_float(value: Any) -> float | None:
    if value is None:
        return None
    return float(value)


@dataclass
class Profile:
    id: str
    email: str | None = None
    full_name: str | None = None
    default_tenant_id: str | None = None
    created_at: datetime | None = None
    updated_at: datetime | None = None

    @classmethod
    def from_row(cls, row: dict[str, Any]) -> Profile:
        return cls(
            id=str(row["id"]),
            email=row.get("email"),
            full_name=row.get("full_name"),
            default_tenant_id=row.get("default_tenant_id"),
            created_at=_parse_dt(row.get("created_at")),
            updated_at=_parse_dt(row.get("updated_at")),
        )


@dataclass
class Tenant:
    id: str
    name: str
    slug: str
    branding_json: dict[str, Any] = field(default_factory=dict)
    is_active: bool = True
    created_at: datetime | None = None
    updated_at: datetime | None = None

    @classmethod
    def from_row(cls, row: dict[str, Any]) -> Tenant:
        return cls(
            id=str(row["id"]),
            name=row["name"],
            slug=row["slug"],
            branding_json=row.get("branding_json") or {},
            is_active=bool(row.get("is_active", True)),
            created_at=_parse_dt(row.get("created_at")),
            updated_at=_parse_dt(row.get("updated_at")),
        )


@dataclass
class TenantUser:
    id: str
    tenant_id: str
    user_id: str
    role: str = "viewer"
    created_at: datetime | None = None

    @classmethod
    def from_row(cls, row: dict[str, Any]) -> TenantUser:
        return cls(
            id=str(row["id"]),
            tenant_id=str(row["tenant_id"]),
            user_id=str(row["user_id"]),
            role=row.get("role", "viewer"),
            created_at=_parse_dt(row.get("created_at")),
        )


@dataclass
class Device:
    id: str
    tenant_id: str | None = None
    owner_user_id: str | None = None
    identity_hash: str = ""
    manufacturer: str | None = None
    model: str | None = None
    device_type: str | None = None
    platform: str | None = None
    nickname: str | None = None
    serial_hash: str | None = None
    serial_last4: str | None = None
    created_at: datetime | None = None
    updated_at: datetime | None = None

    @classmethod
    def from_row(cls, row: dict[str, Any]) -> Device:
        return cls(
            id=str(row["id"]),
            tenant_id=row.get("tenant_id"),
            owner_user_id=row.get("owner_user_id"),
            identity_hash=row.get("identity_hash", ""),
            manufacturer=row.get("manufacturer"),
            model=row.get("model"),
            device_type=row.get("device_type"),
            platform=row.get("platform"),
            nickname=row.get("nickname"),
            serial_hash=row.get("serial_hash"),
            serial_last4=row.get("serial_last4"),
            created_at=_parse_dt(row.get("created_at")),
            updated_at=_parse_dt(row.get("updated_at")),
        )


@dataclass
class DeviceReport:
    id: str
    device_id: str
    tenant_id: str | None = None
    report_type: str = ""
    schema_version: str = "1.0.0"
    platform: str = ""
    tier1_json: dict[str, Any] = field(default_factory=dict)
    tier2_json: dict[str, Any] = field(default_factory=dict)
    tier3_json: dict[str, Any] = field(default_factory=dict)
    raw_report_json: dict[str, Any] = field(default_factory=dict)
    identity_hash: str = ""
    value_hash: str | None = None
    report_hash: str = ""
    collector_version: str | None = None
    certification_assessment_json: dict[str, Any] | None = None
    inspection_report_json: dict[str, Any] | None = None
    assessment_version: str | None = None
    resale_grade: str | None = None
    overall_score: float | None = None
    battery_wear_percent: float | None = None
    created_at: datetime | None = None

    @classmethod
    def from_row(cls, row: dict[str, Any]) -> DeviceReport:
        return cls(
            id=str(row["id"]),
            device_id=str(row["device_id"]),
            tenant_id=row.get("tenant_id"),
            report_type=row.get("report_type", ""),
            schema_version=row.get("schema_version", "1.0.0"),
            platform=row.get("platform", ""),
            tier1_json=row.get("tier1_json") or {},
            tier2_json=row.get("tier2_json") or {},
            tier3_json=row.get("tier3_json") or {},
            raw_report_json=row.get("raw_report_json") or {},
            identity_hash=row.get("identity_hash", ""),
            value_hash=row.get("value_hash"),
            report_hash=row.get("report_hash", ""),
            collector_version=row.get("collector_version"),
            certification_assessment_json=row.get("certification_assessment_json"),
            inspection_report_json=row.get("inspection_report_json"),
            assessment_version=row.get("assessment_version"),
            resale_grade=row.get("resale_grade"),
            overall_score=_parse_float(row.get("overall_score")),
            battery_wear_percent=_parse_float(row.get("battery_wear_percent")),
            created_at=_parse_dt(row.get("created_at")),
        )


@dataclass
class Certificate:
    id: str
    certificate_code: str
    device_id: str
    tenant_id: str | None = None
    initial_report_id: str = ""
    certificate_level: str = ""
    status: str = "active"
    condition_grade: str | None = None
    value_score: float | None = None
    issued_at: datetime | None = None
    expires_at: datetime | None = None
    public_url: str | None = None
    qr_code_payload: str | None = None
    public_payload_json: dict[str, Any] = field(default_factory=dict)
    created_at: datetime | None = None
    updated_at: datetime | None = None
    initial_report: DeviceReport | None = None

    @classmethod
    def from_row(cls, row: dict[str, Any]) -> Certificate:
        return cls(
            id=str(row["id"]),
            certificate_code=row["certificate_code"],
            device_id=str(row["device_id"]),
            tenant_id=row.get("tenant_id"),
            initial_report_id=str(row["initial_report_id"]),
            certificate_level=row.get("certificate_level", ""),
            status=row.get("status", "active"),
            condition_grade=row.get("condition_grade"),
            value_score=_parse_float(row.get("value_score")),
            issued_at=_parse_dt(row.get("issued_at")),
            expires_at=_parse_dt(row.get("expires_at")),
            public_url=row.get("public_url"),
            qr_code_payload=row.get("qr_code_payload"),
            public_payload_json=row.get("public_payload_json") or {},
            created_at=_parse_dt(row.get("created_at")),
            updated_at=_parse_dt(row.get("updated_at")),
        )


@dataclass
class VerificationAttempt:
    id: str
    certificate_id: str | None = None
    verification_report_id: str | None = None
    tenant_id: str | None = None
    result: str = ""
    identity_match_score: float | None = None
    value_match_score: float | None = None
    changes_json: dict[str, Any] = field(default_factory=dict)
    created_at: datetime | None = None

    @classmethod
    def from_row(cls, row: dict[str, Any]) -> VerificationAttempt:
        return cls(
            id=str(row["id"]),
            certificate_id=row.get("certificate_id"),
            verification_report_id=row.get("verification_report_id"),
            tenant_id=row.get("tenant_id"),
            result=row.get("result", ""),
            identity_match_score=_parse_float(row.get("identity_match_score")),
            value_match_score=_parse_float(row.get("value_match_score")),
            changes_json=row.get("changes_json") or {},
            created_at=_parse_dt(row.get("created_at")),
        )


@dataclass
class AgentVersion:
    id: str
    platform: str
    version: str
    download_url: str
    checksum: str
    is_active: bool = True
    release_notes: str | None = None
    minimum_supported_schema_version: str = "1.0.0"
    created_at: datetime | None = None

    @classmethod
    def from_row(cls, row: dict[str, Any]) -> AgentVersion:
        return cls(
            id=str(row["id"]),
            platform=row["platform"],
            version=row["version"],
            download_url=row["download_url"],
            checksum=row["checksum"],
            is_active=bool(row.get("is_active", True)),
            release_notes=row.get("release_notes"),
            minimum_supported_schema_version=row.get("minimum_supported_schema_version", "1.0.0"),
            created_at=_parse_dt(row.get("created_at")),
        )


@dataclass
class IntakeResponse:
    id: str
    user_id: str | None = None
    tenant_id: str | None = None
    device_category: str = ""
    approximate_purchase_year: int = 0
    zip_code_or_region: str = ""
    charger_included: str | None = None
    created_at: datetime | None = None

    @classmethod
    def from_row(cls, row: dict[str, Any]) -> IntakeResponse:
        return cls(
            id=str(row["id"]),
            user_id=row.get("user_id"),
            tenant_id=row.get("tenant_id"),
            device_category=row.get("device_category", ""),
            approximate_purchase_year=int(row.get("approximate_purchase_year", 0)),
            zip_code_or_region=row.get("zip_code_or_region", ""),
            charger_included=row.get("charger_included"),
            created_at=_parse_dt(row.get("created_at")),
        )


@dataclass
class CertificateEvent:
    id: str
    certificate_id: str
    tenant_id: str | None = None
    event_type: str = ""
    event_data: dict[str, Any] = field(default_factory=dict)
    actor_user_id: str | None = None
    created_at: datetime | None = None

    @classmethod
    def from_row(cls, row: dict[str, Any]) -> CertificateEvent:
        return cls(
            id=str(row["id"]),
            certificate_id=str(row["certificate_id"]),
            tenant_id=row.get("tenant_id"),
            event_type=row.get("event_type", ""),
            event_data=row.get("event_data") or {},
            actor_user_id=row.get("actor_user_id"),
            created_at=_parse_dt(row.get("created_at")),
        )


@dataclass
class ScanSession:
    id: str
    session_id: str
    nonce: str
    platform: str
    agent_version: str
    build_channel: str
    status: str
    expires_at: datetime | None = None
    submitted_at: datetime | None = None
    admin_mode: bool | None = None
    scan_started_at: datetime | None = None
    scan_completed_at: datetime | None = None
    hardware_fingerprint: str | None = None
    certificate_id: str | None = None
    certificate_code: str | None = None
    report_url: str | None = None
    verification_url: str | None = None
    qr_code_url: str | None = None
    rejection_reason: str | None = None
    user_id: str | None = None
    tenant_id: str | None = None
    notification_email: str | None = None
    pairing_session_id: str | None = None
    certification_session_id: str | None = None
    upload_jti: str | None = None
    paired_device_fingerprint: str | None = None
    created_at: datetime | None = None

    @classmethod
    def from_row(cls, row: dict[str, Any]) -> ScanSession:
        return cls(
            id=str(row["id"]),
            session_id=row["session_id"],
            nonce=row["nonce"],
            platform=row.get("platform", "windows"),
            agent_version=row["agent_version"],
            build_channel=row.get("build_channel", "production"),
            status=row.get("status", "started"),
            expires_at=_parse_dt(row.get("expires_at")),
            submitted_at=_parse_dt(row.get("submitted_at")),
            admin_mode=row.get("admin_mode"),
            scan_started_at=_parse_dt(row.get("scan_started_at")),
            scan_completed_at=_parse_dt(row.get("scan_completed_at")),
            hardware_fingerprint=row.get("hardware_fingerprint"),
            certificate_id=row.get("certificate_id"),
            certificate_code=row.get("certificate_code"),
            report_url=row.get("report_url"),
            verification_url=row.get("verification_url"),
            qr_code_url=row.get("qr_code_url"),
            rejection_reason=row.get("rejection_reason"),
            user_id=row.get("user_id"),
            tenant_id=row.get("tenant_id"),
            notification_email=row.get("notification_email"),
            pairing_session_id=str(row["pairing_session_id"]) if row.get("pairing_session_id") else None,
            certification_session_id=str(row["certification_session_id"]) if row.get("certification_session_id") else None,
            upload_jti=row.get("upload_jti"),
            paired_device_fingerprint=row.get("paired_device_fingerprint"),
            created_at=_parse_dt(row.get("created_at")),
        )


@dataclass
class ScanReport:
    id: str
    device_id: str | None = None
    user_id: str | None = None
    certificate_id: str | None = None
    device_report_id: str | None = None
    verification_code: str = ""
    public_report_token: str = ""
    scan_payload: dict[str, Any] = field(default_factory=dict)
    report_summary: dict[str, Any] | None = None
    certification_assessment_json: dict[str, Any] | None = None
    inspection_report_json: dict[str, Any] | None = None
    status: str = "completed"
    created_at: datetime | None = None
    updated_at: datetime | None = None

    @classmethod
    def from_row(cls, row: dict[str, Any]) -> ScanReport:
        return cls(
            id=str(row["id"]),
            device_id=row.get("device_id"),
            user_id=row.get("user_id"),
            certificate_id=row.get("certificate_id"),
            device_report_id=row.get("device_report_id"),
            verification_code=row.get("verification_code", ""),
            public_report_token=row.get("public_report_token", ""),
            scan_payload=row.get("scan_payload") or {},
            report_summary=row.get("report_summary"),
            certification_assessment_json=row.get("certification_assessment_json"),
            inspection_report_json=row.get("inspection_report_json"),
            status=row.get("status", "completed"),
            created_at=_parse_dt(row.get("created_at")),
            updated_at=_parse_dt(row.get("updated_at")),
        )


@dataclass
class AuditLog:
    id: str
    action: str
    resource_type: str
    resource_id: str | None = None
    tenant_id: str | None = None
    actor_user_id: str | None = None
    metadata_json: dict[str, Any] = field(default_factory=dict)
    created_at: datetime | None = None

    @classmethod
    def from_row(cls, row: dict[str, Any]) -> AuditLog:
        return cls(
            id=str(row["id"]),
            action=row["action"],
            resource_type=row["resource_type"],
            resource_id=row.get("resource_id"),
            tenant_id=row.get("tenant_id"),
            actor_user_id=row.get("actor_user_id"),
            metadata_json=row.get("metadata_json") or {},
            created_at=_parse_dt(row.get("created_at")),
        )
