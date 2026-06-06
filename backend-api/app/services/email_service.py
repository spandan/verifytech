"""Transactional email for scan completion and account flows."""

from __future__ import annotations

import logging
from typing import Any

import httpx

from app.config import settings

logger = logging.getLogger(__name__)


class EmailService:
    def send_scan_report_email(
        self,
        *,
        to_email: str,
        device_name: str,
        verification_code: str,
        report_url: str,
        dashboard_url: str,
        summary_lines: list[str] | None = None,
    ) -> bool:
        subject = f"Your laptop verification report — {device_name}"
        summary = summary_lines or []
        summary_block = "\n".join(f"  • {line}" for line in summary) if summary else "  • Scan completed successfully"
        body = f"""Hello,

Your laptop verification scan is complete.

Device: {device_name}

Summary:
{summary_block}

Verification code: {verification_code}

View your report:
{report_url}

Save and manage this laptop in your account:
{dashboard_url}

You can share the verification code or report link with buyers when selling your device.

— VerifyTech / DevicePassport
"""
        return self._send(to_email=to_email, subject=subject, text=body)

    def _send(self, *, to_email: str, subject: str, text: str) -> bool:
        if not to_email.strip():
            return False

        if settings.resend_api_key and settings.email_from:
            return self._send_via_resend(to_email=to_email, subject=subject, text=text)

        if settings.debug:
            logger.info("Email (dev mode — not sent)\nTo: %s\nSubject: %s\n\n%s", to_email, subject, text)
            return True

        logger.warning("Email not sent: configure RESEND_API_KEY and EMAIL_FROM")
        return False

    def _send_via_resend(self, *, to_email: str, subject: str, text: str) -> bool:
        payload: dict[str, Any] = {
            "from": settings.email_from,
            "to": [to_email],
            "subject": subject,
            "text": text,
        }
        try:
            response = httpx.post(
                "https://api.resend.com/emails",
                headers={
                    "Authorization": f"Bearer {settings.resend_api_key}",
                    "Content-Type": "application/json",
                },
                json=payload,
                timeout=15.0,
            )
            response.raise_for_status()
            return True
        except httpx.HTTPError as exc:
            logger.error("Failed to send email via Resend: %s", exc)
            return False
