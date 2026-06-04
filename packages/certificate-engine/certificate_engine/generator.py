"""Certificate generation logic."""

from __future__ import annotations

import secrets
import string
from datetime import datetime, timedelta, timezone
from typing import Any, Optional

from schema_engine.models import DeviceReport, ValidationResult
from schema_engine.validator import SchemaValidator

from certificate_engine.models import (
    CertificateGenerationResult,
    CertificateLevel,
    CertificateStatus,
    PublicCertificatePayload,
)

TEST_LABELS = {
    "camera_test_passed": "Camera",
    "microphone_test_passed": "Microphone",
    "speaker_test_passed": "Speaker",
    "keyboard_test_passed": "Keyboard",
    "touchpad_test_passed": "Touchpad",
    "wifi_test_passed": "Wi-Fi",
    "charging_test_passed": "Charging",
}

DEFAULT_CERTIFICATE_TTL_DAYS = 365


class CertificateGenerator:
    """Decides certificate level and generates public certificate payload."""

    def __init__(
        self,
        public_base_url: str = "https://devicepassport.example.com",
        certificate_ttl_days: int = DEFAULT_CERTIFICATE_TTL_DAYS,
    ):
        self.public_base_url = public_base_url.rstrip("/")
        self.certificate_ttl_days = certificate_ttl_days
        self._schema_validator = SchemaValidator()

    def determine_level(self, validation: ValidationResult) -> CertificateLevel:
        """Decide certificate level based on schema validation."""
        if not validation.tier1_complete:
            raise ValueError("Cannot issue certificate: Tier 1 identity incomplete")

        if not validation.tier2_complete:
            return CertificateLevel.IDENTITY_VERIFIED

        tier2 = validation  # we need report for enhanced check
        return CertificateLevel.CONDITION_CERTIFIED

    def determine_level_from_report(self, report: DeviceReport, validation: ValidationResult) -> CertificateLevel:
        if not validation.tier1_complete:
            raise ValueError("Cannot issue certificate: Tier 1 identity incomplete")

        if not validation.tier2_complete:
            return CertificateLevel.IDENTITY_VERIFIED

        tier2 = report.tier2
        passed_tests = sum(
            1 for key in TEST_LABELS if getattr(tier2, key, None) is True
        )
        if passed_tests >= 5 and tier2.battery_health_percent is not None:
            return CertificateLevel.ENHANCED_CERTIFIED

        return CertificateLevel.CONDITION_CERTIFIED

    def generate_code(self, length: int = 12) -> str:
        """Generate human-readable certificate code (uppercase alphanumeric, grouped)."""
        alphabet = string.ascii_uppercase + string.digits
        # Exclude ambiguous chars
        alphabet = alphabet.replace("O", "").replace("0", "").replace("I", "").replace("1", "")
        raw = "".join(secrets.choice(alphabet) for _ in range(length))
        # Format as XXXX-XXXX-XXXX
        return "-".join(raw[i : i + 4] for i in range(0, len(raw), 4))

    def _compute_condition_grade(self, report: DeviceReport) -> str:
        tier2 = report.tier2
        if tier2.cosmetic_grade:
            return tier2.cosmetic_grade.upper()

        score = 100.0
        if tier2.battery_health_percent is not None:
            if tier2.battery_health_percent < 70:
                score -= 20
            elif tier2.battery_health_percent < 85:
                score -= 10

        for test_key in TEST_LABELS:
            if getattr(tier2, test_key, None) is False:
                score -= 5

        if score >= 90:
            return "A"
        if score >= 75:
            return "B"
        if score >= 60:
            return "C"
        return "D"

    def _storage_health(self, report: DeviceReport) -> Optional[float]:
        details = report.tier2.storage_details
        if not details:
            return None
        healths = [d.health_percent for d in details if d.health_percent is not None]
        if not healths:
            return None
        return sum(healths) / len(healths)

    def _core_tests_passed(self, report: DeviceReport) -> list[str]:
        tier2 = report.tier2
        return [
            label
            for key, label in TEST_LABELS.items()
            if getattr(tier2, key, None) is True
        ]

    def _compute_value_score(self, report: DeviceReport) -> Optional[float]:
        if not report.tier2.cosmetic_grade:
            return None
        grade_scores = {"A": 95, "B": 80, "C": 65, "D": 45}
        base = grade_scores.get(report.tier2.cosmetic_grade.upper(), 70)
        if report.tier2.battery_health_percent:
            base = (base + report.tier2.battery_health_percent) / 2
        return round(min(100, max(0, base)), 2)

    def generate(
        self,
        report: DeviceReport,
        validation: ValidationResult,
        certificate_code: Optional[str] = None,
    ) -> CertificateGenerationResult:
        """Generate a full certificate from a validated device report."""
        level = self.determine_level_from_report(report, validation)
        code = certificate_code or self.generate_code()
        now = datetime.now(timezone.utc)
        expires = now + timedelta(days=self.certificate_ttl_days)

        public_url = f"{self.public_base_url}/c/{code}"
        qr_payload = f"{self.public_base_url}/verify?code={code}"

        condition_grade = self._compute_condition_grade(report) if validation.tier2_complete else None
        storage_health = self._storage_health(report)
        tests_passed = self._core_tests_passed(report)

        public_payload = PublicCertificatePayload(
            certificate_code=code,
            device_name=f"{report.tier1.manufacturer} {report.tier1.model}",
            manufacturer=report.tier1.manufacturer,
            model=report.tier1.model,
            device_type=report.tier1.device_type.value,
            platform=report.tier1.platform.value,
            certificate_level=level,
            status=CertificateStatus.ACTIVE,
            condition_grade=condition_grade,
            certification_date=now,
            expires_at=expires,
            battery_health_percent=report.tier2.battery_health_percent,
            storage_health_percent=storage_health,
            core_tests_passed=tests_passed,
            verification_url=f"{self.public_base_url}/verify",
            qr_code_payload=qr_payload,
        )

        return CertificateGenerationResult(
            certificate_code=code,
            certificate_level=level,
            status=CertificateStatus.ACTIVE,
            condition_grade=condition_grade,
            value_score=self._compute_value_score(report) if validation.tier2_complete else None,
            issued_at=now,
            expires_at=expires,
            public_url=public_url,
            qr_code_payload=qr_payload,
            public_payload=public_payload,
            public_payload_json=public_payload.model_dump(mode="json"),
        )
