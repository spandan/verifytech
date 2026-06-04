"""Tests for schema validation."""

from datetime import datetime, timezone

import pytest
from schema_engine.validator import SchemaValidator


def _valid_tier1():
    return {
        "manufacturer": "Dell",
        "model": "XPS 15 9520",
        "device_type": "laptop",
        "platform": "windows",
        "os_family": "windows",
        "os_version": "11 Pro 23H2",
        "serial_number_hash": "a" * 64,
        "hardware_uuid_hash": "b" * 64,
        "motherboard_serial_hash": "c" * 64,
        "primary_storage_serial_hash": "d" * 64,
        "cpu_model": "Intel Core i7-12700H",
        "ram_total_gb": 16,
        "storage_total_gb": 512,
        "collector_version": "0.1.0",
        "collected_at": datetime.now(timezone.utc).isoformat(),
    }


def _valid_tier2():
    return {
        "battery_health_percent": 87,
        "cosmetic_grade": "B",
        "storage_details": [{"drive_index": 0, "capacity_gb": 512, "type": "NVMe", "health_percent": 95}],
        "camera_test_passed": True,
        "microphone_test_passed": True,
        "speaker_test_passed": True,
        "keyboard_test_passed": True,
        "touchpad_test_passed": True,
        "wifi_test_passed": True,
        "charging_test_passed": True,
    }


def test_valid_report_tier1_complete():
    validator = SchemaValidator()
    report = {"schema_version": "1.0.0", "tier1": _valid_tier1(), "tier2": {}, "tier3": {}}
    result = validator.validate(report)
    assert result.valid
    assert result.tier1_complete


def test_invalid_report_missing_serial_hash():
    validator = SchemaValidator()
    tier1 = _valid_tier1()
    del tier1["serial_number_hash"]
    report = {"tier1": tier1, "tier2": {}, "tier3": {}}
    result = validator.validate(report)
    assert not result.valid
    assert any("serial_number_hash" in e for e in result.tier1_errors)


def test_tier2_complete_with_condition_fields():
    validator = SchemaValidator()
    report = {"tier1": _valid_tier1(), "tier2": _valid_tier2(), "tier3": {}}
    result = validator.validate(report)
    assert result.tier1_complete
    assert result.tier2_complete


def test_macos_platform_supported():
    validator = SchemaValidator()
    tier1 = _valid_tier1()
    tier1["platform"] = "macos"
    tier1["os_family"] = "macos"
    tier1["os_version"] = "14.5 Sonoma"
    report = {"tier1": tier1, "tier2": _valid_tier2(), "tier3": {}}
    result = validator.validate(report)
    assert result.tier1_complete
