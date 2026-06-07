"""Build two-layer public inspection reports: buyer summary + collapsed technical details."""

from __future__ import annotations

from typing import Any

from app.services.plain_language_report import (
    humanize_field_name,
    parse_check_item,
    plain_battery,
    plain_encryption,
    plain_functional_label,
    plain_keyboard,
    plain_network,
    plain_performance,
    plain_ports,
    plain_screen,
    plain_secure_boot,
    plain_security_headline,
    plain_storage_drive,
    plain_storage_summary,
    plain_touchpad,
    plain_tpm,
    plain_windows,
    resale_readiness,
    sanitize_warning,
    tri_state_detail,
    grade_subtitle,
    _cv,
)


class InspectionReportService:
    def build(
        self,
        scan_data: dict[str, Any],
        evidence_manifest: list[dict[str, Any]] | None = None,
    ) -> dict[str, Any]:
        assessment = scan_data.get("certification_assessment") or {}
        functional = scan_data.get("functional_tests") or {}
        t1 = scan_data.get("tier1_certification_identity") or {}
        t2 = scan_data.get("tier2_value_determination") or {}
        summary_text = assessment.get("summary") or {}
        grade = (assessment.get("resale_grade") or {}).get("grade", {})
        grade_value = grade.get("value") if isinstance(grade, dict) else grade
        grade_value = grade_value or summary_text.get("recommended_resale_grade") or "C"

        battery = assessment.get("battery") or {}
        storage = assessment.get("storage") or []
        security = assessment.get("security") or {}
        benchmark = assessment.get("benchmark") or {}
        thermals = assessment.get("thermals") or {}
        memory = assessment.get("memory") or {}
        display_a = assessment.get("display") or {}
        ports = assessment.get("ports") or {}
        network = assessment.get("network") or {}
        windows = assessment.get("windows") or {}

        device_name = f"{t1.get('manufacturer', '')} {t1.get('model', '')}".strip() or "This device"
        ram_gb = _cv(t1.get("ram_total_gb"))
        storage_gb = _cv(t1.get("storage_total_gb"))
        cpu = t1.get("cpu_model") or _cv((t2.get("cpu") or {}).get("model"))
        specs_parts = [p for p in [
            cpu,
            f"{int(float(ram_gb))} GB RAM" if ram_gb is not None else None,
            f"{int(float(storage_gb))} GB storage" if storage_gb is not None else None,
        ] if p]
        specs_line = " · ".join(specs_parts) if specs_parts else "Specifications on file"

        functional_summary = _build_functional_summary(functional, display_a)

        security_summary = {
            "headline": plain_security_headline(security),
            "secure_boot": plain_secure_boot(security.get("secure_boot")),
            "encryption": plain_encryption(security.get("device_encryption")),
            "tpm": plain_tpm(security.get("tpm_present")),
        }

        warnings_raw = _collect_warnings(summary_text.get("warnings") or [], storage, battery, functional)
        warnings_plain = [
            sanitize_warning(w) if isinstance(w, str) else w
            for w in warnings_raw
            if w
        ]

        battery_text = plain_battery(
            battery.get("condition"),
            battery.get("wear_percent"),
            battery.get("life_recommendation"),
        )
        storage_text = plain_storage_summary(storage)
        performance_text = plain_performance(benchmark.get("performance_rating"))
        memory_text = _cv(memory.get("health_summary")) or "Checked during scan"
        thermals_text = _plain_thermals(thermals)

        layer1 = {
            "certification_grade": grade_value,
            "grade_subtitle": grade_subtitle(str(grade_value)),
            "device_name": device_name,
            "specs_line": specs_line,
            "display_resolution": (t2.get("display") or {}).get("resolution"),
            "battery": battery_text,
            "storage": storage_text,
            "performance": performance_text,
            "memory": memory_text,
            "thermals": thermals_text,
            "screen": functional_summary["screen"],
            "functional": functional_summary,
            "security": security_summary,
            "network": plain_network(network),
            "windows": plain_windows(windows),
            "ports": plain_ports(ports),
            "check_items": [
                parse_check_item("Battery", battery_text),
                parse_check_item("Storage", storage_text),
                parse_check_item("Performance", performance_text),
                parse_check_item("Screen", functional_summary["screen"]),
                parse_check_item("Memory", memory_text),
                parse_check_item("Cooling", thermals_text),
            ],
            "functional_checks": [
                parse_check_item(
                    {"camera": "Camera", "microphone": "Microphone", "speaker": "Speakers",
                     "keyboard": "Keyboard", "touchpad": "Touchpad", "usb_port": "USB port",
                     "audio_jack": "Headset jack",
                     "screen": "Screen"}.get(k, k.replace("_", " ").title()),
                    v,
                )
                for k, v in functional_summary.items()
            ],
            "resale_readiness": resale_readiness(
                str(grade_value),
                summary_text.get("refurbisher_notes") or "",
                (assessment.get("resale_grade") or {}).get("expected_remaining_service_life"),
            ),
            "warnings": warnings_plain,
        }

        layer2 = {
            "battery": _advanced_battery(battery),
            "storage": _advanced_storage(storage),
            "security": _advanced_security(security),
            "performance": _advanced_performance(benchmark, thermals, memory),
            "functional": _advanced_functional(functional, ports),
            "network": _field_rows(network, [
                "wifi_standard",
                "wifi_generation",
                "bluetooth_version",
                "link_speed_summary",
                "capability_summary",
            ]),
            "windows": _field_rows(windows, [
                "edition",
                "build",
                "readiness_score",
            ]),
            "ports": _advanced_ports(ports),
            "collection_metadata": _advanced_metadata(scan_data, assessment),
            "evidence": [
                {
                    "artifact_type": e.get("artifact_type"),
                    "label": e.get("label") or "Evidence file",
                    "signed_url": e.get("signed_url"),
                }
                for e in (evidence_manifest or [])
            ],
            "narrative_details": {
                "device_overview": summary_text.get("device_overview"),
                "health_summary": summary_text.get("health_summary"),
                "battery_condition": summary_text.get("battery_condition"),
                "storage_condition": summary_text.get("storage_condition"),
                "performance_rating": summary_text.get("performance_rating"),
                "security_rating": summary_text.get("security_rating"),
                "functional_test_results": summary_text.get("functional_test_results"),
            },
        }

        return {
            "version": assessment.get("assessment_version") or "2.3",
            "summary": layer1,
            "advanced": layer2,
        }


def _functional_test_skipped(test: Any) -> bool:
    return isinstance(test, dict) and test.get("reason") == "skipped"


def _legacy_block_skipped(block: Any) -> bool:
    return isinstance(block, dict) and block.get("skipped") is True


def _build_functional_summary(functional: dict[str, Any], display_a: dict[str, Any]) -> dict[str, str]:
    summary: dict[str, str] = {
        "screen": plain_screen(functional.get("display") or {}, display_a),
    }

    if not _legacy_block_skipped(functional.get("keyboard")):
        summary["keyboard"] = plain_keyboard(functional.get("keyboard"))
    if not _legacy_block_skipped(functional.get("touchpad")):
        summary["touchpad"] = plain_touchpad(functional.get("touchpad"))

    optional_tests = [
        ("camera", "camera_test", plain_functional_label),
        ("microphone", "microphone_test", plain_functional_label),
        ("speaker", "speaker_test", plain_functional_label),
        ("usb_port", "usb_test", plain_functional_label),
        ("audio_jack", "audio_jack_test", plain_functional_label),
    ]
    for key, field, formatter in optional_tests:
        raw = functional.get(field)
        if _functional_test_skipped(raw):
            continue
        summary[key] = formatter(raw)

    return summary


def _plain_thermals(thermals: dict[str, Any]) -> str:
    cooling = _cv(thermals.get("cooling_health")) or _cv(thermals.get("condition"))
    if not cooling:
        return "Checked during performance test"
    mapping = {
        "excellent": "Excellent — stays cool under load",
        "good": "Good — normal temperatures",
        "fair": "Fair — runs warm under load",
        "poor": "Poor — may throttle when busy",
    }
    return mapping.get(str(cooling).lower(), str(cooling))


def _collect_warnings(
    warnings: list,
    storage: list,
    battery: dict,
    functional: dict,
) -> list[str]:
    out: list[str] = []
    for w in warnings:
        if w and str(w) not in out:
            out.append(str(w))
    for drive in storage:
        if not isinstance(drive, dict):
            continue
        honesty = drive.get("storage_health") or {}
        disc = honesty.get("public_disclosure")
        if disc and disc not in out:
            out.append(disc)
    wear = _cv(battery.get("wear_percent"))
    if wear is not None and isinstance(wear, (int, float)) and wear >= 35:
        msg = "Battery has significant wear — buyers should expect shorter runtime."
        if msg not in out:
            out.append(msg)
    for key in ("camera_test", "microphone_test", "speaker_test"):
        t = functional.get(key)
        if isinstance(t, dict) and t.get("result") == "failed":
            label = key.replace("_test", "").replace("_", " ").title()
            msg = f"{label} did not pass interactive verification."
            if msg not in out:
                out.append(msg)
    return out


def _field_rows(obj: dict[str, Any], keys: list[str]) -> list[dict[str, str]]:
    rows = []
    for key in keys:
        val = _cv(obj.get(key))
        if val is None:
            continue
        rows.append({"label": humanize_field_name(key), "value": str(val)})
    return rows


def _advanced_battery(battery: dict[str, Any]) -> dict[str, Any]:
    return {
        "fields": _field_rows(battery, [
            "design_capacity_mwh",
            "full_charge_capacity_mwh",
            "current_capacity_mwh",
            "cycle_count",
            "wear_percent",
            "degradation_trend",
            "estimated_remaining_months",
            "estimated_remaining_cycles",
            "condition",
            "life_recommendation",
        ]),
        "capacity_history": [
            {
                "period": p.get("period"),
                "full_charge_mwh": p.get("full_charge_capacity_mwh"),
                "design_mwh": p.get("design_capacity_mwh"),
            }
            for p in (battery.get("capacity_history") or [])
            if isinstance(p, dict)
        ],
        "certification_notes": battery.get("certification_notes") or [],
    }


def _advanced_storage(storage: list) -> list[dict[str, Any]]:
    drives = []
    for drive in storage:
        if not isinstance(drive, dict):
            continue
        honesty = drive.get("storage_health") or {}
        drives.append({
            "headline": plain_storage_drive(drive),
            "fields": _field_rows(drive, [
                "model",
                "health_percent",
                "percentage_used",
                "power_on_hours",
                "power_cycle_count",
                "unsafe_shutdown_count",
                "reallocated_sector_count",
                "pending_sector_count",
                "media_error_count",
                "temperature_c",
                "remaining_life_estimate",
            ]),
            "collection": {
                "collection_level": honesty.get("collection_level"),
                "confidence": honesty.get("confidence"),
                "windows_reliability_counters": honesty.get("windows_reliability_counters_collected"),
                "full_smart_attributes": honesty.get("full_smart_attributes_collected"),
                "nvme_log_pages": honesty.get("nvme_log_pages_collected"),
            },
            "disclosure": honesty.get("public_disclosure"),
        })
    return drives


def _advanced_security(security: dict[str, Any]) -> dict[str, Any]:
    return {
        "fields": _field_rows(security, [
            "security_score",
            "tpm_version",
            "bitlocker_status",
        ]),
        "tpm": tri_state_detail(security.get("tpm_present")),
        "tpm_enabled": tri_state_detail(security.get("tpm_enabled")),
        "secure_boot": tri_state_detail(security.get("secure_boot")),
        "device_encryption": tri_state_detail(security.get("device_encryption")),
        "vbs": tri_state_detail(security.get("vbs_enabled")),
        "credential_guard": tri_state_detail(security.get("credential_guard")),
    }


def _advanced_performance(
    benchmark: dict[str, Any],
    thermals: dict[str, Any],
    memory: dict[str, Any],
) -> dict[str, Any]:
    return {
        "benchmark": _field_rows(benchmark, [
            "performance_rating",
            "overall_score",
            "cpu_score",
            "memory_score",
            "storage_score",
        ]),
        "thermals": _field_rows(thermals, [
            "cooling_health",
            "condition",
            "peak_cpu_temperature_c",
            "average_cpu_temperature_c",
            "thermal_stability_score",
        ]),
        "memory": _field_rows(memory, [
            "health_summary",
            "stability_score",
        ]),
    }


def _advanced_functional(functional: dict[str, Any], ports: dict[str, Any]) -> dict[str, Any]:
    return {
        "camera_test": functional.get("camera_test"),
        "microphone_test": functional.get("microphone_test"),
        "speaker_test": functional.get("speaker_test"),
        "usb_test": functional.get("usb_test"),
        "audio_jack_test": functional.get("audio_jack_test"),
        "keyboard": functional.get("keyboard"),
        "touchpad": functional.get("touchpad"),
        "display_checks": functional.get("display"),
        "port_inventory": ports.get("port_certification_status") or {},
    }


def _advanced_ports(ports: dict[str, Any]) -> dict[str, Any]:
    return {
        "inventory": _field_rows(ports, [
            "usb_a_count",
            "usb_c_count",
            "hdmi_count",
            "display_port_count",
            "thunderbolt_count",
        ]),
        "features": {
            "thunderbolt": tri_state_detail(ports.get("thunderbolt")),
            "ethernet": tri_state_detail(ports.get("ethernet")),
            "audio_jack": tri_state_detail(ports.get("audio_jack")),
            "sd_card_reader": tri_state_detail(ports.get("sd_card_reader")),
        },
        "certification_status": ports.get("port_certification_status") or {},
        "notes": ports.get("inventory_notes") or [],
    }


def _advanced_metadata(scan_data: dict[str, Any], assessment: dict[str, Any]) -> dict[str, Any]:
    ctx = scan_data.get("collection_context") or {}
    meta = scan_data.get("agent_metadata") or {}
    return {
        "assessment_version": assessment.get("assessment_version"),
        "schema_version": scan_data.get("schema_version"),
        "collector_version": ctx.get("collector_version"),
        "collected_at": ctx.get("collected_at"),
        "scan_warnings": meta.get("collection_warnings") or [],
    }
