#!/usr/bin/env python3
"""Submit a buyer verification report against an existing certificate."""

from __future__ import annotations

import hashlib
import json
import os
import sys

try:
    import httpx
except ImportError:
    print("Install httpx: pip install httpx")
    sys.exit(1)

API_URL = os.environ.get("DEVICEPASSPORT_API_URL", "http://localhost:8000")
CERT_CODE = os.environ.get("CERTIFICATE_CODE", "")


def hash_id(value: str) -> str:
    return hashlib.sha256(value.strip().upper().encode()).hexdigest()


def verification_report(same_device: bool = True, battery: float = 87):
    """Generate a verification scan — same or different device."""
    from datetime import datetime, timezone

    serial = "SN-DEMO-12345" if same_device else "SN-OTHER-99999"
    uuid = "HW-UUID-DEMO-67890" if same_device else "HW-UUID-OTHER-99999"
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
            "cpu_model": "Intel Core i7-12700H",
            "ram_total_gb": 16,
            "storage_total_gb": 512,
            "collector_version": "0.1.0-verifier",
            "collected_at": now,
        },
        "tier2": {
            "battery_health_percent": battery,
            "cosmetic_grade": "B",
            "storage_details": [{"drive_index": 0, "capacity_gb": 512, "type": "NVMe", "health_percent": 95}],
            "keyboard_test_passed": True,
            "wifi_test_passed": True,
            "charging_test_passed": True,
        },
        "tier3": {},
    }


def main():
    if not CERT_CODE:
        print("Set CERTIFICATE_CODE env var")
        sys.exit(1)

    same = os.environ.get("SAME_DEVICE", "true").lower() == "true"
    battery = float(os.environ.get("BATTERY_HEALTH", "87"))

    payload = {
        "certificate_code": CERT_CODE,
        "report": verification_report(same_device=same, battery=battery),
    }

    print(f"Verifying certificate {CERT_CODE} ...")
    resp = httpx.post(f"{API_URL}/api/verify/submit", json=payload, timeout=30)
    resp.raise_for_status()
    result = resp.json()
    print(json.dumps(result, indent=2))
    print(f"\nView result: http://localhost:3000/verification-result/{result['attempt_id']}")


if __name__ == "__main__":
    main()
