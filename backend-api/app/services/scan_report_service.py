"""Create and query account-facing scan report records."""

from __future__ import annotations

import secrets
from datetime import datetime, timezone
from typing import Any

from app.db.models import Database
from app.db.records import ScanReport


def normalize_verification_code(code: str) -> str:
    return code.replace("-", "").replace(" ", "").upper()


def format_verification_code(code: str) -> str:
    raw = normalize_verification_code(code)
    if len(raw) != 12:
        return code.upper()
    return f"{raw[0:4]}-{raw[4:8]}-{raw[8:12]}"


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
        user_id: str | None,
    ) -> ScanReport:
        formatted_code = format_verification_code(verification_code)
        existing = db.get_scan_report_by_verification_code(formatted_code)
        if existing:
            updates: dict[str, Any] = {
                "certificate_id": certificate_id,
                "device_id": device_id,
                "device_report_id": device_report_id,
                "scan_payload": scan_payload,
                "report_summary": report_summary,
                "updated_at": datetime.now(timezone.utc).isoformat(),
            }
            if user_id and not existing.user_id:
                updates["user_id"] = user_id
            db.update_scan_report(existing.id, updates)
            return db.get_scan_report(existing.id) or existing

        return db.create_scan_report(
            {
                "device_id": device_id,
                "user_id": user_id,
                "certificate_id": certificate_id,
                "device_report_id": device_report_id,
                "verification_code": formatted_code,
                "public_report_token": secrets.token_urlsafe(24),
                "scan_payload": scan_payload,
                "report_summary": report_summary,
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
        return {
            "device_name": summary.get("device_name") if isinstance(summary, dict) else None,
            "manufacturer": t1.get("manufacturer"),
            "model": t1.get("model"),
            "certification_grade": inspection.get("certification_grade") if isinstance(inspection, dict) else None,
            "verification_status": certificate_status,
            "headline": summary.get("grade_subtitle") if isinstance(summary, dict) else None,
        }

    def extract_serial_fields(self, scan_data: dict[str, Any]) -> tuple[str | None, str | None]:
        t1 = scan_data.get("tier1_certification_identity") or {}
        serial_hash = t1.get("serial_number_hash") or None
        last4 = t1.get("serial_number_last4") or t1.get("serial_last4")
        if isinstance(last4, str) and last4.strip():
            return serial_hash, last4.strip()[-4:]
        return serial_hash, None
