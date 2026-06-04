"""Certificate domain models."""

from __future__ import annotations

from datetime import datetime
from enum import Enum
from typing import Any, Optional
from uuid import UUID

from pydantic import BaseModel, Field


class CertificateLevel(str, Enum):
    IDENTITY_VERIFIED = "identity_verified"
    CONDITION_CERTIFIED = "condition_certified"
    ENHANCED_CERTIFIED = "enhanced_certified"


class CertificateStatus(str, Enum):
    ACTIVE = "active"
    EXPIRED = "expired"
    REVOKED = "revoked"
    SUPERSEDED = "superseded"


class PublicCertificatePayload(BaseModel):
    """Buyer-safe public certificate data — no raw identifiers."""

    project_name: str = "DevicePassport Certified"
    certificate_code: str
    device_name: str
    manufacturer: str
    model: str
    device_type: str
    platform: str
    certificate_level: CertificateLevel
    status: CertificateStatus
    condition_grade: Optional[str] = None
    certification_date: datetime
    expires_at: datetime
    battery_health_percent: Optional[float] = None
    storage_health_percent: Optional[float] = None
    core_tests_passed: list[str] = Field(default_factory=list)
    core_tests_total: int = 7
    masked_serial: str = "****"
    verification_url: str
    qr_code_payload: str


class CertificateGenerationResult(BaseModel):
    """Output of certificate generation."""

    certificate_code: str
    certificate_level: CertificateLevel
    status: CertificateStatus
    condition_grade: Optional[str] = None
    value_score: Optional[float] = None
    issued_at: datetime
    expires_at: datetime
    public_url: str
    qr_code_payload: str
    public_payload: PublicCertificatePayload
    public_payload_json: dict[str, Any]
