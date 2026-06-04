"""Transform Windows agent canonical report into internal schema-engine format."""

from __future__ import annotations

from datetime import datetime
from typing import Any, Optional


def _get(d: dict[str, Any], *keys: str, default: Any = None) -> Any:
    cur: Any = d
    for key in keys:
        if not isinstance(cur, dict):
            return default
        cur = cur.get(key, default)
    return cur


def agent_report_to_internal(raw: dict[str, Any]) -> dict[str, Any]:
    """Map agent JSON (tier1_certification_identity, etc.) to schema-engine DeviceReport dict."""
    t1 = raw.get("tier1_certification_identity") or {}
    t2 = raw.get("tier2_value_determination") or {}
    t3 = raw.get("tier3_optional_intelligence") or {}
    ctx = raw.get("collection_context") or {}

    cpu = t2.get("cpu") or {}
    memory = t2.get("memory") or {}
    battery = t2.get("battery") or {}
    display = t2.get("display") or {}
    graphics = t2.get("graphics") or {}
    functional = t2.get("functional_readiness") or {}
    security = t3.get("security") or {}
    firmware = t3.get("firmware") or {}
    network = t3.get("network") or {}
    performance = t3.get("performance") or {}
    software = t3.get("software") or {}

    storage_details = []
    for idx, drive in enumerate(t2.get("storage") or []):
        storage_details.append(
            {
                "drive_index": drive.get("index", idx),
                "capacity_gb": drive.get("capacity_gb", 0),
                "type": drive.get("drive_type") or "Unknown",
                "health_percent": drive.get("health_percent"),
                "smart_status": drive.get("smart_status"),
            }
        )

    collected_at = ctx.get("collected_at") or datetime.utcnow().isoformat()

    internal = {
        "schema_version": "1.0.0",
        "tier1": {
            "manufacturer": t1.get("manufacturer") or "Unknown",
            "model": t1.get("model") or "Unknown",
            "device_type": t1.get("device_type") or "laptop",
            "platform": raw.get("platform") or "windows",
            "os_family": t1.get("os_family") or "windows",
            "os_version": t1.get("os_version") or "Windows",
            "serial_number_hash": t1.get("serial_number_hash") or "",
            "hardware_uuid_hash": t1.get("hardware_uuid_hash") or "",
            "motherboard_serial_hash": t1.get("motherboard_serial_hash"),
            "primary_storage_serial_hash": t1.get("primary_storage_serial_hash"),
            "cpu_model": t1.get("cpu_model") or cpu.get("model") or "Unknown",
            "ram_total_gb": t1.get("ram_total_gb") or memory.get("total_gb") or 0,
            "storage_total_gb": t1.get("storage_total_gb") or sum(
                d.get("capacity_gb", 0) for d in (t2.get("storage") or [])
            ),
            "collector_version": ctx.get("collector_version") or "0.1.0",
            "collected_at": collected_at,
        },
        "tier2": {
            "cpu_details": _cpu_details(cpu),
            "ram_details": memory.get("details"),
            "storage_details": storage_details,
            "battery_health_percent": battery.get("health_percent"),
            "battery_cycle_count": battery.get("cycle_count"),
            "display_resolution": display.get("resolution"),
            "display_status": "ok" if display.get("resolution") else None,
            "gpu_model": graphics.get("gpu_model"),
            "camera_test_passed": functional.get("camera_present"),
            "microphone_test_passed": functional.get("microphone_present"),
            "speaker_test_passed": functional.get("speaker_present"),
            "keyboard_test_passed": functional.get("keyboard_present"),
            "touchpad_test_passed": functional.get("touchpad_present"),
            "wifi_test_passed": functional.get("wifi_present"),
            "charging_test_passed": _charging_ok(functional.get("charging_status")),
            "cosmetic_grade": None,
        },
        "tier3": {
            "tpm_present": security.get("tpm_present"),
            "secure_boot_enabled": security.get("secure_boot_enabled"),
            "disk_encryption": security.get("bitlocker_status"),
            "bios_version": firmware.get("bios_version"),
            "firmware_version": firmware.get("bios_version"),
            "network_adapters": network.get("adapters") or [],
            "port_details": [{"summary": network.get("port_summary")}] if network.get("port_summary") else [],
            "thermal_data": None,
            "benchmark_scores": {"boot_time_seconds": performance.get("boot_time_seconds")}
            if performance.get("boot_time_seconds")
            else None,
            "driver_issues": software.get("driver_issues") or [],
            "activation_status": None,
        },
        "raw_extensions": {
            "agent_schema_version": raw.get("schema_version"),
            "collection_context": ctx,
            "agent_metadata": raw.get("agent_metadata"),
            "os_build": t1.get("os_build"),
        },
    }

    return internal


def _cpu_details(cpu: dict[str, Any]) -> Optional[str]:
    model = cpu.get("model")
    cores = cpu.get("core_count")
    threads = cpu.get("thread_count")
    if not model:
        return None
    parts = [model]
    if cores:
        parts.append(f"{cores}C")
    if threads:
        parts.append(f"{threads}T")
    return " ".join(parts)


def _charging_ok(status: Optional[str]) -> Optional[bool]:
    if not status:
        return None
    return status in {"ac_power", "fully_charged", "charging", "charging_high", "charging_low"}
