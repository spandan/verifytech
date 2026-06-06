"""User account devices, report claims, and My Laptops dashboard."""

from __future__ import annotations

import secrets
from datetime import datetime, timedelta, timezone
from typing import Any

from app.config import settings
from app.db.models import Database
from app.db.records import Device, ScanReport
from app.schemas.dto import (
    ClaimReportRequest,
    ClaimReportResponse,
    MyLaptopSummary,
    MyLaptopsResponse,
    RenameDeviceRequest,
    ScanLinkTokenResponse,
    SendReportEmailRequest,
)
from app.services.email_service import EmailService
from app.services.scan_report_service import ScanReportService, format_verification_code, normalize_verification_code


class AccountService:
    def __init__(self) -> None:
        self._email = EmailService()
        self._scan_reports = ScanReportService()

    def create_scan_link_token(self, db: Database, user_id: str) -> ScanLinkTokenResponse:
        token = secrets.token_urlsafe(18)
        expires_at = datetime.now(timezone.utc) + timedelta(minutes=settings.scan_session_ttl_minutes)
        db.create_account_scan_link_token(
            {
                "token": token,
                "user_id": user_id,
                "expires_at": expires_at.isoformat(),
            }
        )
        return ScanLinkTokenResponse(token=token, expires_at=expires_at)

    def resolve_scan_link_token(self, db: Database, token: str, session_id: str) -> str | None:
        row = db.get_account_scan_link_token(token)
        if not row:
            return None
        expires_at = row["expires_at"]
        if isinstance(expires_at, str):
            expires_at = datetime.fromisoformat(expires_at.replace("Z", "+00:00"))
        if expires_at.tzinfo is None:
            expires_at = expires_at.replace(tzinfo=timezone.utc)
        if datetime.now(timezone.utc) > expires_at or row.get("used_at"):
            return None
        db.mark_account_scan_link_token_used(token, session_id)
        return str(row["user_id"])

    def list_my_laptops(self, db: Database, user_id: str) -> MyLaptopsResponse:
        devices = db.get_devices_by_owner(user_id)
        reports = db.get_scan_reports_for_user(user_id)
        reports_by_device = {r.device_id: r for r in reports if r.device_id}
        certs = db.get_certificates_for_devices([d.id for d in devices])
        cert_by_device = {c.device_id: c for c in certs}

        items: list[MyLaptopSummary] = []
        for device in devices:
            report = reports_by_device.get(device.id)
            cert = cert_by_device.get(device.id)
            device_name = device.nickname or self._default_device_name(device)
            items.append(
                MyLaptopSummary(
                    device_id=device.id,
                    nickname=device.nickname,
                    device_name=device_name,
                    manufacturer=device.manufacturer,
                    model=device.model,
                    serial_last4=device.serial_last4,
                    last_scan_at=report.created_at if report else device.updated_at,
                    verification_status=cert.status if cert else (report.status if report else "unknown"),
                    verification_code=report.verification_code if report else (cert.certificate_code if cert else ""),
                    public_report_url=self._public_report_url(report, cert),
                    report_token=report.public_report_token if report else None,
                )
            )

        items.sort(
            key=lambda item: item.last_scan_at or datetime.min.replace(tzinfo=timezone.utc),
            reverse=True,
        )
        return MyLaptopsResponse(laptops=items)

    def rename_device(self, db: Database, user_id: str, device_id: str, body: RenameDeviceRequest) -> MyLaptopSummary:
        device = db.get_device(device_id)
        if not device or device.owner_user_id != user_id:
            raise ValueError("Device not found")
        nickname = body.nickname.strip()
        if not nickname:
            raise ValueError("Nickname is required")
        db.update_device(
            device_id,
            {"nickname": nickname, "updated_at": datetime.now(timezone.utc).isoformat()},
        )
        updated = db.get_device(device_id)
        assert updated
        laptops = self.list_my_laptops(db, user_id)
        match = next((item for item in laptops.laptops if item.device_id == device_id), None)
        if match:
            return match
        return MyLaptopSummary(
            device_id=updated.id,
            nickname=updated.nickname,
            device_name=nickname,
            manufacturer=updated.manufacturer,
            model=updated.model,
            serial_last4=updated.serial_last4,
            last_scan_at=updated.updated_at,
            verification_status="unknown",
            verification_code="",
            public_report_url="",
            report_token=None,
        )

    def claim_report(self, db: Database, user_id: str, body: ClaimReportRequest) -> ClaimReportResponse:
        code = format_verification_code(body.verification_code)
        report = db.get_scan_report_by_verification_code(code)
        if not report:
            cert = db.get_certificate_by_code(code)
            if not cert:
                raise ValueError("Verification code not found")
            report = self._ensure_scan_report_for_certificate(db, cert.id, user_id)

        if report.user_id and report.user_id != user_id:
            raise ValueError("This report is already saved to another account")

        if report.user_id == user_id:
            return ClaimReportResponse(
                device_id=report.device_id or "",
                verification_code=report.verification_code,
                public_report_url=self._public_report_url(report),
                message="Laptop is already in your account.",
            )

        device = db.get_device(report.device_id) if report.device_id else None
        if device and device.owner_user_id and device.owner_user_id != user_id:
            raise ValueError("This device belongs to another account")

        now = datetime.now(timezone.utc).isoformat()
        db.update_scan_report(report.id, {"user_id": user_id, "updated_at": now})
        if report.device_id:
            db.update_device(
                report.device_id,
                {"owner_user_id": user_id, "updated_at": now},
            )
        db.create_report_claim({"scan_report_id": report.id, "user_id": user_id})

        profile = db.get_profile(user_id)
        if profile and profile.email:
            self._send_report_email(db, report, profile.email)

        refreshed = db.get_scan_report(report.id)
        assert refreshed
        return ClaimReportResponse(
            device_id=refreshed.device_id or "",
            verification_code=refreshed.verification_code,
            public_report_url=self._public_report_url(refreshed),
            message="Laptop saved to your account.",
        )

    def send_report_email(self, db: Database, body: SendReportEmailRequest) -> bool:
        code = format_verification_code(body.verification_code)
        report = db.get_scan_report_by_verification_code(code)
        if not report:
            raise ValueError("Report not found")
        return self._send_report_email(db, report, body.email.strip())

    def associate_scan_with_user(
        self,
        db: Database,
        *,
        user_id: str,
        device_id: str,
        scan_report: ScanReport,
    ) -> None:
        now = datetime.now(timezone.utc).isoformat()
        db.update_device(device_id, {"owner_user_id": user_id, "updated_at": now})
        db.update_scan_report(scan_report.id, {"user_id": user_id, "updated_at": now})
        db.create_report_claim({"scan_report_id": scan_report.id, "user_id": user_id})

    def notify_scan_complete(
        self,
        db: Database,
        *,
        scan_report: ScanReport,
        device: Device | None,
        email: str | None,
    ) -> None:
        if not email:
            return
        self._send_report_email(db, scan_report, email)

    def _ensure_scan_report_for_certificate(
        self,
        db: Database,
        certificate_id: str,
        user_id: str | None,
    ) -> ScanReport:
        cert = db.get_certificate_by_id(certificate_id)
        if not cert:
            raise ValueError("Certificate not found")
        report = db.get_scan_report_by_certificate_id(certificate_id)
        if report:
            return report
        device_report = db.get_device_report(cert.initial_report_id)
        scan_payload = device_report.raw_report_json if device_report else {}
        return self._scan_reports.create_from_scan(
            db,
            certificate_id=cert.id,
            device_id=cert.device_id,
            device_report_id=cert.initial_report_id,
            verification_code=cert.certificate_code,
            scan_payload=scan_payload,
            report_summary=self._scan_reports.build_report_summary(
                scan_payload,
                (cert.public_payload_json or {}).get("inspection_report"),
                cert.status,
            ),
            user_id=user_id,
        )

    def _send_report_email(self, db: Database, report: ScanReport, email: str) -> bool:
        summary = report.report_summary or {}
        device_name = summary.get("device_name") or "Your laptop"
        lines: list[str] = []
        if summary.get("certification_grade"):
            lines.append(f"Grade: {summary['certification_grade']}")
        if summary.get("headline"):
            lines.append(str(summary["headline"]))
        return self._email.send_scan_report_email(
            to_email=email,
            device_name=str(device_name),
            verification_code=report.verification_code,
            report_url=self._public_report_url(report),
            dashboard_url=f"{settings.public_base_url.rstrip('/')}/my-laptops",
            summary_lines=lines or None,
        )

    def _default_device_name(self, device: Device) -> str:
        parts = [p for p in (device.manufacturer, device.model) if p]
        return " ".join(parts) if parts else "My Laptop"

    def _public_report_url(self, report: ScanReport | None, cert: Any | None = None) -> str:
        if report and report.verification_code:
            return f"{settings.public_base_url.rstrip('/')}/c/{report.verification_code}"
        if cert and getattr(cert, "certificate_code", None):
            return f"{settings.public_base_url.rstrip('/')}/c/{cert.certificate_code}"
        return settings.public_base_url
