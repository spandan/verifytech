"""Cryptographic hashing utilities for device identity and report integrity."""

from __future__ import annotations

import hashlib
import json
from typing import Any


def hash_identifier(value: str) -> str:
    """Hash a sensitive identifier (serial, UUID, MAC) for storage."""
    normalized = value.strip().upper()
    return hashlib.sha256(normalized.encode("utf-8")).hexdigest()


def hash_payload(data: dict[str, Any]) -> str:
    """Deterministic hash of a JSON-serializable payload."""
    canonical = json.dumps(data, sort_keys=True, separators=(",", ":"), default=str)
    return hashlib.sha256(canonical.encode("utf-8")).hexdigest()


TIER1_IDENTITY_FIELDS = [
    "manufacturer",
    "model",
    "device_type",
    "platform",
    "os_family",
    "os_version",
    "serial_number_hash",
    "hardware_uuid_hash",
    "motherboard_serial_hash",
    "primary_storage_serial_hash",
    "cpu_model",
    "ram_total_gb",
    "storage_total_gb",
    "collector_version",
    "collected_at",
]


TIER2_VALUE_FIELDS = [
    "cpu_details",
    "ram_details",
    "storage_details",
    "battery_health_percent",
    "battery_cycle_count",
    "display_resolution",
    "display_status",
    "gpu_model",
    "camera_test_passed",
    "microphone_test_passed",
    "speaker_test_passed",
    "keyboard_test_passed",
    "touchpad_test_passed",
    "wifi_test_passed",
    "charging_test_passed",
    "cosmetic_grade",
]


def compute_identity_hash(tier1: dict[str, Any]) -> str:
    """Compute identity hash from Tier 1 fields only."""
    subset = {k: tier1.get(k) for k in TIER1_IDENTITY_FIELDS if k in tier1}
    return hash_payload(subset)


def compute_hardware_fingerprint(scan_data: dict[str, Any]) -> str:
    """Match Windows agent HardwareFingerprintService (session submit validation)."""
    t1 = scan_data.get("tier1_certification_identity") or {}
    parts = [
        t1.get("serial_number_hash"),
        t1.get("hardware_uuid_hash"),
        t1.get("motherboard_serial_hash"),
        t1.get("primary_storage_serial_hash"),
    ]
    payload = "|".join(p for p in parts if p)
    if not payload:
        payload = "|".join(
            filter(
                None,
                [t1.get("manufacturer"), t1.get("model"), t1.get("cpu_model")],
            )
        )
    return hashlib.sha256(payload.encode("utf-8")).hexdigest()


def compute_value_hash(tier2: dict[str, Any]) -> str:
    """Compute value hash from Tier 2 fields only."""
    subset = {k: tier2.get(k) for k in TIER2_VALUE_FIELDS if k in tier2}
    return hash_payload(subset)
