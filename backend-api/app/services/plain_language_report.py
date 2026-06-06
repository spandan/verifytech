"""Plain-English labels for public certificates — no WMI/API jargon in default view."""

from __future__ import annotations

import re
from typing import Any

# Short labels for summary cards (buyer-facing)
LABEL_VERIFIED = "Verified"
LABEL_NOT_TESTED = "Not tested"
LABEL_FAILED = "Failed"
LABEL_INCONCLUSIVE = "Unclear"

_TECHNICAL_PATTERN = re.compile(
    r"win32_|wmi|cim|msft_|powershell|registry|pnp|nvme\s+log|"
    r"configmanager|smartctl|\.json|reliability\s+counter",
    re.I,
)


def _cv(field: Any) -> Any:
    if isinstance(field, dict):
        return field.get("value")
    return field


def parse_check_item(label: str, value: str | None) -> dict[str, str | None]:
    """Split a buyer-facing line into label + headline + optional detail for UI hierarchy."""
    raw = (value or "").strip()
    if not raw or raw.lower() in ("not available", "not tested", "not measured", "not checked"):
        return {
            "label": label,
            "headline": "Not checked",
            "detail": None,
            "tone": "muted",
        }

    parts = re.split(r"\s*[—–-]\s*", raw, maxsplit=1)
    headline = parts[0].strip()
    detail = parts[1].strip() if len(parts) > 1 else None
    tone = _check_tone(headline)
    return {
        "label": label,
        "headline": headline,
        "detail": detail,
        "tone": tone,
    }


def _check_tone(headline: str) -> str:
    h = headline.lower()
    if any(w in h for w in ("verified", "enabled", "healthy", "excellent", "checked")):
        return "good"
    if any(w in h for w in ("failed", "poor", "disabled", "replacement", "problem", "issues")):
        return "bad"
    if any(w in h for w in ("not tested", "not verified", "not detected", "unclear", "not checked")):
        return "muted"
    if any(w in h for w in ("fair", "caution", "monitor", "some wear")):
        return "warn"
    if any(w in h for w in ("good", "normal", "adequate", "suitable")):
        return "good"
    return "neutral"


def plain_functional_label(test: Any) -> str:
    if not isinstance(test, dict):
        return LABEL_NOT_TESTED
    result = test.get("result")
    if result == "passed":
        return LABEL_VERIFIED
    if result == "failed":
        return LABEL_FAILED
    if result == "inconclusive":
        return LABEL_INCONCLUSIVE
    if test.get("playback_confirmed") and test.get("tested"):
        return LABEL_VERIFIED
    if test.get("present") and not test.get("tested"):
        return LABEL_NOT_TESTED
    return LABEL_NOT_TESTED


def plain_keyboard(keyboard: Any) -> str:
    if not isinstance(keyboard, dict):
        return LABEL_NOT_TESTED
    if keyboard.get("skipped"):
        return LABEL_NOT_TESTED
    if keyboard.get("passed") is True:
        return LABEL_VERIFIED
    if keyboard.get("passed") is False:
        missing = keyboard.get("keys_missing") or []
        if missing:
            return f"{LABEL_FAILED} — some keys not detected"
        return LABEL_FAILED
    return LABEL_NOT_TESTED


def plain_touchpad(touchpad: Any) -> str:
    if not isinstance(touchpad, dict):
        return LABEL_NOT_TESTED
    if touchpad.get("skipped"):
        return LABEL_NOT_TESTED
    op = touchpad.get("operational")
    if isinstance(op, dict):
        if op.get("value") is True:
            return LABEL_VERIFIED
        if op.get("value") is False:
            return LABEL_FAILED
    return LABEL_NOT_TESTED


def plain_battery(condition: Any, wear: Any, life_rec: Any) -> str:
    cond = (_cv(condition) or "").strip()
    wear_val = _cv(wear)
    life = (_cv(life_rec) or "").strip()

    mapping = {
        "excellent": "Excellent — capacity is strong",
        "good": "Good — normal wear for age",
        "fair": "Fair — noticeable wear",
        "poor": "Poor — replacement recommended",
        "replace soon": "Poor — replacement recommended",
        "warning": "Fair — monitor before resale",
        "critical": "Poor — replacement recommended",
    }
    base = mapping.get(cond.lower(), cond or "Not available")
    if wear_val is not None and isinstance(wear_val, (int, float)):
        if wear_val >= 40 and "replacement" not in base.lower():
            base = "Fair — higher wear than typical"
        elif wear_val >= 25 and base.startswith("Good"):
            base = "Good — some wear present"
    if life and "replacement" in life.lower() and "Poor" not in base:
        base = "Poor — replacement recommended"
    return base


def plain_storage_drive(drive: dict[str, Any]) -> str:
    cond = (_cv(drive.get("condition")) or "Unknown").strip()
    health = _cv(drive.get("health_percent"))
    mapping = {
        "excellent": "Healthy",
        "good": "Healthy",
        "fair": "Fair — some wear",
        "warning": "Caution — declining health",
        "critical": "Poor — consider replacement",
    }
    label = mapping.get(cond.lower(), cond)
    if health is not None and isinstance(health, (int, float)):
        if health < 50:
            label = "Poor — consider replacement"
        elif health < 70 and label == "Healthy":
            label = "Fair — some wear"
    return label


def plain_storage_summary(storage: list) -> str:
    if not storage:
        return "Not available"
    labels = [plain_storage_drive(d) for d in storage if isinstance(d, dict)]
    if not labels:
        return "Not available"
    if all(l.startswith("Healthy") for l in labels):
        return "Healthy"
    return labels[0] if len(labels) == 1 else "; ".join(labels[:2])


def plain_performance(rating: Any) -> str:
    r = (_cv(rating) or "").strip()
    mapping = {
        "excellent": "Excellent — well above typical",
        "good": "Good — suitable for everyday use",
        "fair": "Fair — adequate for basic tasks",
        "poor": "Below average — may feel slow",
        "average": "Good — suitable for everyday use",
    }
    return mapping.get(r.lower(), r or "Not measured")


def plain_secure_boot(field: Any) -> str:
    if not isinstance(field, dict):
        return "Not verified"
    if field.get("collection_status") == "unknown":
        return "Not verified"
    if field.get("value") is True:
        return "Enabled"
    if field.get("value") is False:
        return "Disabled"
    return "Not verified"


def plain_encryption(field: Any) -> str:
    if not isinstance(field, dict):
        return "Not verified"
    if field.get("collection_status") == "unknown":
        return "Not verified"
    if field.get("value") is True:
        return "Enabled"
    if field.get("value") is False:
        return "Not enabled"
    return "Not verified"


def plain_tpm(field: Any) -> str:
    if not isinstance(field, dict):
        return "Not detected"
    if field.get("value") is True:
        return "Present"
    if field.get("value") is False:
        return "Not detected"
    return "Not verified"


def plain_security_headline(security: dict[str, Any]) -> str:
    score = _cv(security.get("security_score"))
    sb = plain_secure_boot(security.get("secure_boot"))
    enc = plain_encryption(security.get("device_encryption"))
    if score is not None and isinstance(score, (int, float)) and score >= 75:
        return "Security features look good"
    if sb == "Enabled" and enc == "Enabled":
        return "Security features are enabled"
    if sb == "Disabled" or enc == "Not enabled":
        return "Some security features need attention"
    return "Security status partially verified"


def plain_screen(display_fn: dict[str, Any], display_assessment: dict[str, Any]) -> str:
    grade = _cv(display_assessment.get("grade")) if display_assessment else None
    if isinstance(display_fn, dict) and not display_fn.get("skipped"):
        dead = display_fn.get("dead_pixel_test_passed")
        bright = display_fn.get("brightness_test_passed")
        if dead is False:
            return "Issues reported — check screen carefully"
        if dead and bright:
            return "Checked — no issues reported"
        if dead:
            return "Checked — acceptable"
    if grade and str(grade).lower() in ("pass", "excellent", "good"):
        return "Checked — no issues reported"
    return "Not fully tested"


def plain_network(network: dict[str, Any]) -> str:
    if not network:
        return "Checked during scan"
    cap = _cv(network.get("capability_summary"))
    if cap and isinstance(cap, str) and not _TECHNICAL_PATTERN.search(cap):
        return cap
    parts = []
    wifi = _cv(network.get("wifi_generation")) or _cv(network.get("wifi_standard"))
    if wifi:
        parts.append(f"Wi‑Fi {wifi}")
    bt = _cv(network.get("bluetooth_version"))
    if bt:
        parts.append(f"Bluetooth {bt}")
    link = _cv(network.get("link_speed_summary"))
    if link and not _TECHNICAL_PATTERN.search(str(link)):
        parts.append(str(link))
    return " · ".join(parts) if parts else "Wireless and Bluetooth checked"


def plain_windows(windows: dict[str, Any]) -> str:
    if not windows:
        return "Windows configuration checked"
    edition = _cv(windows.get("edition"))
    activation = windows.get("activation_status")
    act_val = activation.get("value") if isinstance(activation, dict) else activation
    parts = []
    if edition:
        parts.append(str(edition))
    if act_val is True:
        parts.append("Activated")
    elif act_val is False:
        parts.append("Activation needs attention")
    pending = windows.get("pending_updates")
    pending_val = pending.get("value") if isinstance(pending, dict) else pending
    if pending_val is True:
        parts.append("Updates pending")
    elif pending_val is False:
        parts.append("Up to date")
    return " · ".join(parts) if parts else "Windows configuration checked"


def plain_ports(ports: dict[str, Any]) -> str:
    if not ports:
        return "Port inventory recorded"
    parts = []
    usb_a = _cv(ports.get("usb_a_count"))
    usb_c = _cv(ports.get("usb_c_count"))
    if usb_a:
        parts.append(f"{usb_a} USB‑A")
    if usb_c:
        parts.append(f"{usb_c} USB‑C")
    hdmi = _cv(ports.get("hdmi_count"))
    if hdmi:
        parts.append(f"{hdmi} HDMI")
    dp = _cv(ports.get("display_port_count"))
    if dp:
        parts.append(f"{dp} DisplayPort")
    tb = ports.get("thunderbolt")
    tb_val = tb.get("value") if isinstance(tb, dict) else tb
    if tb_val is True:
        parts.append("Thunderbolt")
    eth = ports.get("ethernet")
    eth_val = eth.get("value") if isinstance(eth, dict) else eth
    if eth_val is True:
        parts.append("Ethernet")
    sd = ports.get("sd_card_reader")
    sd_val = sd.get("value") if isinstance(sd, dict) else sd
    if sd_val is True:
        parts.append("SD card reader")
    return ", ".join(parts) if parts else "Port inventory recorded"


def grade_subtitle(grade: str) -> str:
    g = (grade or "C").upper().replace("+", "")
    subtitles = {
        "A": "Excellent condition for resale",
        "B": "Good condition for resale",
        "C": "Acceptable with noted limitations",
        "D": "Significant concerns — review before buying",
    }
    key = grade.upper()[0] if grade else "C"
    return subtitles.get(key, "Review inspection details before buying")


def resale_readiness(grade: str, refurb_notes: str, service_life: Any) -> str:
    g = (grade or "").upper()
    if g in ("A+", "A"):
        return "Ready for resale"
    if g in ("B+", "B"):
        return "Ready for resale — minor refresh may help value"
    if g == "C":
        return "Sell with disclosures — buyer should review warnings"
    if refurb_notes and len(refurb_notes) < 120 and not _TECHNICAL_PATTERN.search(refurb_notes):
        return refurb_notes
    life = _cv(service_life)
    if life and isinstance(life, str) and not _TECHNICAL_PATTERN.search(life):
        return f"Expected useful life: {life}"
    return "Review warnings before listing for resale"


def sanitize_warning(text: str) -> dict[str, str]:
    raw = (text or "").strip()
    if not raw:
        return {"title": "Notice", "explanation": "See inspection details."}
    if _TECHNICAL_PATTERN.search(raw) or "storage api" in raw.lower():
        if "storage" in raw.lower() or "smart" in raw.lower():
            return {
                "title": "Storage check limited",
                "explanation": (
                    "Full drive diagnostics were not available on this device. "
                    "Storage health is based on standard Windows checks only."
                ),
            }
        if "battery" in raw.lower():
            return {
                "title": "Battery data limited",
                "explanation": "Battery history could not be fully analyzed on this scan.",
            }
        return {
            "title": "Diagnostic note",
            "explanation": "Some checks were limited on this device. Expand technical details for more.",
        }
    if len(raw) > 160:
        return {"title": "Important", "explanation": raw[:157] + "…"}
    return {"title": "Important", "explanation": raw}


def humanize_field_name(key: str) -> str:
    labels = {
        "design_capacity_mwh": "Design capacity",
        "full_charge_capacity_mwh": "Full charge capacity",
        "current_capacity_mwh": "Current capacity",
        "cycle_count": "Cycle count",
        "wear_percent": "Capacity wear",
        "power_on_hours": "Power-on hours",
        "power_cycle_count": "Power cycles",
        "percentage_used": "Percentage used",
        "health_percent": "Health score",
        "reallocated_sector_count": "Reallocated sectors",
        "pending_sector_count": "Pending sectors",
        "media_error_count": "Media errors",
        "collection_level": "How data was collected",
        "confidence": "Confidence level",
        "degradation_trend": "Degradation trend",
        "estimated_remaining_months": "Est. months remaining",
        "estimated_remaining_cycles": "Est. cycles remaining",
        "tpm_version": "TPM version",
        "bitlocker_status": "Encryption status",
        "cpu_score": "CPU score",
        "memory_score": "Memory score",
        "storage_score": "Disk score",
        "overall_score": "Overall score",
        "cooling_health": "Cooling health",
    }
    return labels.get(key, key.replace("_", " ").title())


def tri_state_detail(field: Any) -> dict[str, Any] | None:
    if not isinstance(field, dict):
        return None
    return {
        "status": "Verified" if field.get("value") is True else "Not verified" if field.get("value") is False else "Unknown",
        "collection_method": field.get("method"),
        "data_source": field.get("source"),
        "confidence": field.get("collection_status"),
    }
