#!/usr/bin/env python3
"""Submit a sample device report to the API for POC testing."""

from __future__ import annotations

import hashlib
import json
import os
import sys
from datetime import datetime, timezone

try:
    import httpx
except ImportError:
    print("Install httpx: pip install httpx")
    sys.exit(1)

API_URL = os.environ.get("DEVICEPASSPORT_API_URL", "http://localhost:8000")


def hash_id(value: str) -> str:
    return hashlib.sha256(value.strip().upper().encode()).hexdigest()


def sample_report():
    serial = "SN-DEMO-12345"
    uuid = "HW-UUID-DEMO-67890"
    now = datetime.now(timezone.utc).isoformat()

    return {
        "schema_version": "1.0.0",
        "tier1": {
            "manufacturer": "Dell",
            "model": "XPS 15 9520",
            "device_type": "laptop",
            "platform": "windows",
            "os_family": "windows",
            "os_version": "11 Pro 23H2",
            "serial_number_hash": hash_id(serial),
            "hardware_uuid_hash": hash_id(uuid),
            "motherboard_serial_hash": hash_id("MB-DEMO-111"),
            "primary_storage_serial_hash": hash_id("SSD-DEMO-222"),
            "cpu_model": "Intel Core i7-12700H",
            "ram_total_gb": 16,
            "storage_total_gb": 512,
            "collector_version": "0.1.0",
            "collected_at": now,
        },
        "tier2": {
            "cpu_details": "Intel Core i7-12700H @ 2.30GHz",
            "ram_details": "16GB DDR5",
            "storage_details": [
                {"drive_index": 0, "capacity_gb": 512, "type": "NVMe", "health_percent": 95}
            ],
            "battery_health_percent": 87,
            "battery_cycle_count": 142,
            "display_resolution": "3840x2400",
            "display_status": "ok",
            "gpu_model": "NVIDIA GeForce RTX 3050 Ti",
            "camera_test_passed": True,
            "microphone_test_passed": True,
            "speaker_test_passed": True,
            "keyboard_test_passed": True,
            "touchpad_test_passed": True,
            "wifi_test_passed": True,
            "charging_test_passed": True,
            "cosmetic_grade": "B",
        },
        "tier3": {
            "tpm_present": True,
            "secure_boot_enabled": True,
            "disk_encryption": "bitlocker",
        },
    }


def main():
    report = sample_report()
    payload = {
        "report": report,
        "report_type": "initial_certification",
        "owner_user_id": os.environ.get("OWNER_USER_ID", "demo-user-00000000-0000-0000-0000-000000000001"),
    }

    print(f"Submitting report to {API_URL}/api/reports ...")
    resp = httpx.post(f"{API_URL}/api/reports", json=payload, timeout=30)
    resp.raise_for_status()
    result = resp.json()
    print(json.dumps(result, indent=2))

    if result.get("public_url"):
        print(f"\n✓ Certificate ready: {result['public_url']}")
    elif result.get("certificate_code"):
        print(f"\n✓ Certificate code: {result['certificate_code']}")


if __name__ == "__main__":
    main()
