"""Verification comparison logic."""

from __future__ import annotations

from datetime import datetime, timezone
from typing import Any, Optional

from certificate_engine.models import CertificateStatus
from schema_engine.models import DeviceReport
from shared.hashing import TIER1_IDENTITY_FIELDS, TIER2_VALUE_FIELDS

from verification_engine.models import FieldChange, VerificationOutcome, VerificationResult

# Tier 1 fields that must match exactly for same-device identity
TIER1_STRICT_MATCH = [
    "serial_number_hash",
    "hardware_uuid_hash",
    "manufacturer",
    "model",
    "cpu_model",
    "ram_total_gb",
]

TIER1_FUZZY_MATCH = [
    "motherboard_serial_hash",
    "primary_storage_serial_hash",
    "storage_total_gb",
    "os_version",
]

TIER2_COMPARE = TIER2_VALUE_FIELDS


class VerificationComparator:
    """Compare live buyer verification scan against original certified report."""

    IDENTITY_MATCH_THRESHOLD = 0.85
    VALUE_MATCH_THRESHOLD = 0.90

    def compare(
        self,
        certified_report: DeviceReport,
        live_report: DeviceReport,
        certificate_status: CertificateStatus = CertificateStatus.ACTIVE,
        expires_at: Optional[datetime] = None,
    ) -> VerificationResult:
        if certificate_status == CertificateStatus.REVOKED:
            return VerificationResult(
                outcome=VerificationOutcome.CERTIFICATE_REVOKED,
                summary="This certificate has been revoked.",
            )

        if certificate_status == CertificateStatus.EXPIRED:
            return VerificationResult(
                outcome=VerificationOutcome.CERTIFICATE_EXPIRED,
                summary="This certificate has expired.",
            )

        if expires_at and expires_at.replace(tzinfo=timezone.utc) < datetime.now(timezone.utc):
            return VerificationResult(
                outcome=VerificationOutcome.CERTIFICATE_EXPIRED,
                summary="This certificate has expired.",
            )

        cert_t1 = certified_report.tier1.model_dump()
        live_t1 = live_report.tier1.model_dump()
        cert_t2 = certified_report.tier2.model_dump()
        live_t2 = live_report.tier2.model_dump()

        # Critical identity hashes must match exactly
        for critical in ("serial_number_hash", "hardware_uuid_hash"):
            if cert_t1.get(critical) != live_t1.get(critical):
                changes: list[FieldChange] = [
                    FieldChange(
                        field=f"tier1.{critical}",
                        tier=1,
                        certified_value=self._safe_display(critical, cert_t1.get(critical)),
                        live_value=self._safe_display(critical, live_t1.get(critical)),
                        significance="identity",
                    )
                ]
                return VerificationResult(
                    outcome=VerificationOutcome.DEVICE_MISMATCH,
                    identity_match_score=0.0,
                    value_match_score=0.0,
                    changes=changes,
                    summary="The scanned device does not match the certified device.",
                )

        changes: list[FieldChange] = []
        identity_score = self._compute_identity_score(cert_t1, live_t1, changes)
        value_score, value_changes = self._compute_value_score(cert_t2, live_t2)
        changes.extend(value_changes)

        if identity_score < self.IDENTITY_MATCH_THRESHOLD:
            return VerificationResult(
                outcome=VerificationOutcome.DEVICE_MISMATCH,
                identity_match_score=identity_score,
                value_match_score=value_score,
                changes=changes,
                summary="The scanned device does not match the certified device.",
            )

        value_invalidated = any(c.significance == "value" for c in value_changes)

        if value_changes and value_score < self.VALUE_MATCH_THRESHOLD:
            return VerificationResult(
                outcome=VerificationOutcome.CERTIFIED_WITH_CHANGES,
                identity_match_score=identity_score,
                value_match_score=value_score,
                changes=changes,
                summary="Device identity matches, but condition/value fields have changed since certification.",
                value_estimate_invalidated=True,
            )

        return VerificationResult(
            outcome=VerificationOutcome.CERTIFIED_MATCH,
            identity_match_score=identity_score,
            value_match_score=value_score,
            changes=changes,
            summary="Device matches the certified report.",
            value_estimate_invalidated=value_invalidated,
        )

    def _compute_identity_score(
        self,
        cert: dict[str, Any],
        live: dict[str, Any],
        changes: list[FieldChange],
    ) -> float:
        strict_total = len(TIER1_STRICT_MATCH)
        strict_matches = 0

        for field in TIER1_STRICT_MATCH:
            cv, lv = cert.get(field), live.get(field)
            if cv == lv:
                strict_matches += 1
            else:
                changes.append(
                    FieldChange(
                        field=f"tier1.{field}",
                        tier=1,
                        certified_value=self._safe_display(field, cv),
                        live_value=self._safe_display(field, lv),
                        significance="identity",
                    )
                )

        fuzzy_total = len(TIER1_FUZZY_MATCH)
        fuzzy_matches = 0
        for field in TIER1_FUZZY_MATCH:
            cv, lv = cert.get(field), live.get(field)
            if cv is None and lv is None:
                fuzzy_matches += 1
            elif cv == lv:
                fuzzy_matches += 1
            elif field == "storage_total_gb" and cv and lv:
                # Allow small variance (e.g. formatting)
                if abs(float(cv) - float(lv)) < 1.0:
                    fuzzy_matches += 1
                else:
                    changes.append(
                        FieldChange(
                            field=f"tier1.{field}",
                            tier=1,
                            certified_value=cv,
                            live_value=lv,
                            significance="identity",
                        )
                    )
            elif cv != lv and cv is not None and lv is not None:
                changes.append(
                    FieldChange(
                        field=f"tier1.{field}",
                        tier=1,
                        certified_value=self._safe_display(field, cv),
                        live_value=self._safe_display(field, lv),
                        significance="identity",
                    )
                )

        strict_weight = 0.75
        fuzzy_weight = 0.25
        strict_score = strict_matches / strict_total if strict_total else 1.0
        fuzzy_score = fuzzy_matches / fuzzy_total if fuzzy_total else 1.0
        return strict_weight * strict_score + fuzzy_weight * fuzzy_score

    def _compute_value_score(
        self,
        cert: dict[str, Any],
        live: dict[str, Any],
    ) -> tuple[float, list[FieldChange]]:
        changes: list[FieldChange] = []
        comparable = [f for f in TIER2_COMPARE if cert.get(f) is not None or live.get(f) is not None]
        if not comparable:
            return 1.0, changes

        matches = 0
        for field in comparable:
            cv, lv = cert.get(field), live.get(field)
            if field == "storage_details":
                if self._storage_details_match(cv, lv):
                    matches += 1
                else:
                    changes.append(
                        FieldChange(
                            field="tier2.storage_details",
                            tier=2,
                            certified_value="(storage config)",
                            live_value="(storage config changed)",
                            significance="value",
                        )
                    )
            elif cv == lv:
                matches += 1
            elif cv is not None and lv is not None:
                changes.append(
                    FieldChange(
                        field=f"tier2.{field}",
                        tier=2,
                        certified_value=cv,
                        live_value=lv,
                        significance="value",
                    )
                )

        score = matches / len(comparable) if comparable else 1.0
        return score, changes

    def _storage_details_match(
        self, cert_details: Any, live_details: Any
    ) -> bool:
        if not cert_details and not live_details:
            return True
        if not cert_details or not live_details:
            return False
        cert_caps = sorted(d.get("capacity_gb", 0) for d in cert_details)
        live_caps = sorted(d.get("capacity_gb", 0) for d in live_details)
        return cert_caps == live_caps

    def _safe_display(self, field: str, value: Any) -> Any:
        if "hash" in field and value:
            return f"{str(value)[:8]}...{str(value)[-4:]}"
        return value
