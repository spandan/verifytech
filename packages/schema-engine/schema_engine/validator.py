"""Schema validation logic."""

from __future__ import annotations

from typing import Any

from schema_engine.models import (
    CURRENT_SCHEMA_VERSION,
    DeviceReport,
    ValidationResult,
)


class SchemaValidator:
    """Validates device reports against the canonical schema."""

    TIER1_REQUIRED = [
        "manufacturer",
        "model",
        "device_type",
        "platform",
        "os_family",
        "os_version",
        "serial_number_hash",
        "hardware_uuid_hash",
        "cpu_model",
        "ram_total_gb",
        "storage_total_gb",
        "collector_version",
        "collected_at",
    ]

    TIER2_REQUIRED_FOR_CONDITION = [
        "battery_health_percent",
        "cosmetic_grade",
    ]

    TIER2_TESTS = [
        "camera_test_passed",
        "microphone_test_passed",
        "speaker_test_passed",
        "keyboard_test_passed",
        "touchpad_test_passed",
        "wifi_test_passed",
        "charging_test_passed",
    ]

    def validate(self, raw: dict[str, Any]) -> ValidationResult:
        """Validate a raw report dict. Returns structured validation result."""
        tier1_errors: list[str] = []
        tier2_errors: list[str] = []
        warnings: list[str] = []

        schema_version = raw.get("schema_version", CURRENT_SCHEMA_VERSION)
        if schema_version != CURRENT_SCHEMA_VERSION:
            warnings.append(
                f"Schema version {schema_version} differs from current {CURRENT_SCHEMA_VERSION}"
            )

        tier1_raw = raw.get("tier1") or {}
        tier2_raw = raw.get("tier2") or {}

        for field in self.TIER1_REQUIRED:
            val = tier1_raw.get(field)
            if val is None or val == "":
                tier1_errors.append(f"tier1.{field} is required")

        # Validate hash format (should be hex sha256)
        for hash_field in ("serial_number_hash", "hardware_uuid_hash"):
            h = tier1_raw.get(hash_field, "")
            if h and (len(h) != 64 or not all(c in "0123456789abcdef" for c in h.lower())):
                tier1_errors.append(f"tier1.{hash_field} must be a 64-char hex SHA-256 hash")

        if tier1_raw.get("ram_total_gb") is not None and tier1_raw["ram_total_gb"] <= 0:
            tier1_errors.append("tier1.ram_total_gb must be positive")

        if tier1_raw.get("storage_total_gb") is not None and tier1_raw["storage_total_gb"] <= 0:
            tier1_errors.append("tier1.storage_total_gb must be positive")

        tier1_complete = len(tier1_errors) == 0

        for field in self.TIER2_REQUIRED_FOR_CONDITION:
            if tier2_raw.get(field) is None:
                tier2_errors.append(f"tier2.{field} is required for condition certification")

        storage = tier2_raw.get("storage_details") or []
        if tier1_complete and not storage:
            tier2_errors.append("tier2.storage_details is required for condition certification")

        passed_tests = sum(
            1 for t in self.TIER2_TESTS if tier2_raw.get(t) is True
        )
        if tier1_complete and passed_tests < 3:
            warnings.append(
                f"Only {passed_tests}/7 hardware tests passed; enhanced certification unavailable"
            )

        tier2_complete = len(tier2_errors) == 0

        # Attempt pydantic parse if tier1 looks valid
        if tier1_complete:
            try:
                DeviceReport.model_validate(raw)
            except Exception as e:
                tier1_errors.append(f"Schema parse error: {e}")
                tier1_complete = False

        return ValidationResult(
            valid=tier1_complete,
            schema_version=schema_version,
            tier1_complete=tier1_complete,
            tier2_complete=tier2_complete,
            tier1_errors=tier1_errors,
            tier2_errors=tier2_errors,
            warnings=warnings,
        )

    def parse(self, raw: dict[str, Any]) -> DeviceReport:
        """Parse and validate; raises on invalid tier1."""
        result = self.validate(raw)
        if not result.tier1_complete:
            raise ValueError(f"Invalid report: {result.tier1_errors}")
        return DeviceReport.model_validate(raw)
