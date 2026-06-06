"""Create and query account-facing scan report records."""

from __future__ import annotations

import secrets
from datetime import datetime, timezone
from typing import Any

from app.db.models import Database
from app.db.records import ScanReport
from app.services.plain_language_report import _cv


def normalize_verification_code(code: str) -> str:
    return code.replace("-", "").replace(" ", "").upper()


def format_verification_code(code: str) -> str:
    raw = normalize_verification_code(code)
    if len(raw) != 12:
        return code.upper()
    return f"{raw[0:4]}-{raw[4:8]}-{raw[8:12]}"


def extract_assessment_metadata(
    scan_data: dict[str, Any],
    inspection: dict[str, Any] | None,
) -> dict[str, Any]:
    """Denormalized fields for indexed columns and report summaries."""
    assessment = scan_data.get("certification_assessment") or {}
    summary = (inspection or {}).get("summary") if isinstance(inspection, dict) else {}
    summary = summary if isinstance(summary, dict) else {}

    grade = summary.get("certification_grade")
    if not grade:
        grade_obj = (assessment.get("resale_grade") or {}).get("grade")
        grade = grade_obj.get("value") if isinstance(grade_obj, dict) else grade_obj

    benchmark = assessment.get("benchmark") or {}
    battery = assessment.get("battery") or {}
    overall = _cv(benchmark.get("overall_score"))
    wear = _cv(battery.get("wear_percent"))

    return {
        "assessment_version": assessment.get("assessment_version"),
        "resale_grade": str(grade) if grade else None,
        "overall_score": float(overall) if overall is not None else None,
        "battery_wear_percent": float(wear) if wear is not None else None,
    }


class ScanReportService:
    def create_from_scan(
        self,
        db: Database,
        *,
        certificate_id: str,
        device_id: str,
        device_report_id: str,
        verification_code: str,
        scan_payload: dict[str, Any],
        report_summary: dict[str, Any] | None,
        certification_assessment: dict[str, Any] | None = None,
        inspection_report: dict[str, Any] | None = None,
        user_id: str | None,
    ) -> ScanReport:
        formatted_code = format_verification_code(verification_code)
        existing = db.get_scan_report_by_verification_code(formatted_code)
        shared: dict[str, Any] = {
            "certificate_id": certificate_id,
            "device_id": device_id,
            "device_report_id": device_report_id,
            "scan_payload": scan_payload,
            "report_summary": report_summary,
            "certification_assessment_json": certification_assessment,
            "inspection_report_json": inspection_report,
            "updated_at": datetime.now(timezone.utc).isoformat(),
        }
        if existing:
            updates = dict(shared)
            if user_id and not existing.user_id:
                updates["user_id"] = user_id
            db.update_scan_report(existing.id, updates)
            return db.get_scan_report(existing.id) or existing

        return db.create_scan_report(
            {
                **shared,
                "user_id": user_id,
                "verification_code": formatted_code,
                "public_report_token": secrets.token_urlsafe(24),
                "status": "completed",
            }
        )

    def build_report_summary(
        self,
        scan_data: dict[str, Any],
        inspection: dict[str, Any] | None,
        certificate_status: str,
    ) -> dict[str, Any]:
        t1 = scan_data.get("tier1_certification_identity") or {}
        summary = inspection.get("summary") if isinstance(inspection, dict) else None
        summary = summary if isinstance(summary, dict) else {}
        meta = extract_assessment_metadata(scan_data, inspection)
        functional = summary.get("functional") if isinstance(summary.get("functional"), dict) else {}

        return {
            "device_name": summary.get("device_name") or f"{t1.get('manufacturer', '')} {t1.get('model', '')}".strip(),
            "manufacturer": t1.get("manufacturer"),
            "model": t1.get("model"),
            "certification_grade": summary.get("certification_grade") or meta.get("resale_grade"),
            "verification_status": certificate_status,
            "headline": summary.get("grade_subtitle"),
            "specs_line": summary.get("specs_line"),
            "battery_summary": summary.get("battery"),
            "storage_summary": summary.get("storage"),
            "performance_summary": summary.get("performance"),
            "screen_summary": summary.get("screen"),
            "security_headline": (summary.get("security") or {}).get("headline") if isinstance(summary.get("security"), dict) else None,
            "resale_readiness": summary.get("resale_readiness"),
            "overall_score": meta.get("overall_score"),
            "battery_wear_percent": meta.get("battery_wear_percent"),
            "functional_passed": sum(
                1 for v in functional.values() if isinstance(v, str) and v.lower() == "verified"
            ),
            "functional_total": len(functional),
            "warning_count": len(summary.get("warnings") or []),
        }

    def extract_serial_fields(self, scan_data: dict[str, Any]) -> tuple[str | None, str | None]:
        t1 = scan_data.get("tier1_certification_identity") or {}
        serial_hash = t1.get("serial_number_hash") or None
        last4 = t1.get("serial_number_last4") or t1.get("serial_last4")
        if isinstance(last4, str) and last4.strip():
            return serial_hash, last4.strip()[-4:]
        return serial_hash, None
