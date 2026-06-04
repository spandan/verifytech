"""Tests for verification comparison logic."""

from datetime import datetime, timedelta, timezone

from certificate_engine.models import CertificateStatus
from schema_engine.models import DeviceReport, Tier1Identity, Tier2Value, DeviceType, Platform, OSFamily
from verification_engine.comparator import VerificationComparator


def _make_report(
    serial_hash: str = "a" * 64,
    battery: float = 87,
    cosmetic: str = "B",
) -> DeviceReport:
    return DeviceReport(
        tier1=Tier1Identity(
            manufacturer="Dell",
            model="XPS 15",
            device_type=DeviceType.LAPTOP,
            platform=Platform.WINDOWS,
            os_family=OSFamily.WINDOWS,
            os_version="11",
            serial_number_hash=serial_hash,
            hardware_uuid_hash="b" * 64,
            cpu_model="i7-12700H",
            ram_total_gb=16,
            storage_total_gb=512,
            collector_version="0.1.0",
            collected_at=datetime.now(timezone.utc),
        ),
        tier2=Tier2Value(
            battery_health_percent=battery,
            cosmetic_grade=cosmetic,
            storage_details=[],
            keyboard_test_passed=True,
            wifi_test_passed=True,
            charging_test_passed=True,
        ),
    )


def test_certified_match():
    comparator = VerificationComparator()
    certified = _make_report()
    live = _make_report()
    result = comparator.compare(certified, live)
    assert result.outcome.value == "CERTIFIED_MATCH"
    assert result.identity_match_score >= 0.85


def test_device_mismatch():
    comparator = VerificationComparator()
    certified = _make_report(serial_hash="a" * 64)
    live = _make_report(serial_hash="c" * 64)
    result = comparator.compare(certified, live)
    assert result.outcome.value == "DEVICE_MISMATCH"


def test_certified_with_changes():
    comparator = VerificationComparator()
    certified = _make_report(battery=90)
    live = _make_report(battery=65)
    result = comparator.compare(certified, live)
    assert result.outcome.value == "CERTIFIED_WITH_CHANGES"
    assert result.value_estimate_invalidated


def test_certificate_expired():
    comparator = VerificationComparator()
    certified = _make_report()
    live = _make_report()
    expired = datetime.now(timezone.utc) - timedelta(days=1)
    result = comparator.compare(certified, live, expires_at=expired)
    assert result.outcome.value == "CERTIFICATE_EXPIRED"


def test_certificate_revoked():
    comparator = VerificationComparator()
    certified = _make_report()
    live = _make_report()
    result = comparator.compare(
        certified, live, certificate_status=CertificateStatus.REVOKED
    )
    assert result.outcome.value == "CERTIFICATE_REVOKED"
