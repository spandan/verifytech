"""Agent-initiated pairing for manual launch and token recovery."""

from __future__ import annotations

import secrets
import string
from datetime import datetime, timedelta, timezone
from typing import Any
from uuid import uuid4

from app.auth.certification_session_jwt import create_certification_token
from app.config import settings
from app.db.models import Database
from app.schemas.dto import (
    AgentPairingClaimRequest,
    AgentPairingClaimResponse,
    AgentPairingCreateRequest,
    AgentPairingCreateResponse,
    AgentPairingStatusResponse,
)
from app.services.audit_service import AuditService

_PAIRING_ALPHABET = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"


class AgentPairingService:
    def __init__(self) -> None:
        self._audit = AuditService()

    def create(self, db: Database, body: AgentPairingCreateRequest) -> AgentPairingCreateResponse:
        device_nonce = body.device_nonce.strip()
        if len(device_nonce) < 8:
            raise ValueError("device_nonce is required.")

        existing = db.get_agent_pairing_session_by_device_nonce(device_nonce)
        if existing and existing.get("status") == "PENDING":
            expires_at = _parse_dt(existing.get("expires_at"))
            if expires_at and datetime.now(timezone.utc) < expires_at:
                return AgentPairingCreateResponse(
                    pairing_code=str(existing["pairing_code"]),
                    expires_in_minutes=settings.agent_pairing_ttl_minutes,
                    expires_at=expires_at,
                )

        pairing_code = _generate_pairing_code(db)
        expires_at = datetime.now(timezone.utc) + timedelta(minutes=settings.agent_pairing_ttl_minutes)

        db.create_agent_pairing_session(
            {
                "pairing_code": pairing_code,
                "device_nonce": device_nonce,
                "status": "PENDING",
                "expires_at": expires_at.isoformat(),
            }
        )

        return AgentPairingCreateResponse(
            pairing_code=pairing_code,
            expires_in_minutes=settings.agent_pairing_ttl_minutes,
            expires_at=expires_at,
        )

    def claim(
        self,
        db: Database,
        *,
        user_id: str,
        body: AgentPairingClaimRequest,
    ) -> AgentPairingClaimResponse:
        code = body.pairing_code.strip().upper()
        row = db.get_agent_pairing_session(code)
        if not row:
            raise ValueError("Invalid pairing code.")

        if row.get("status") == "PAIRED":
            raise ValueError("This pairing code has already been used.")

        self._ensure_not_expired(row)

        if row.get("status") != "PENDING":
            raise ValueError("Pairing code is no longer valid.")

        cert_session_id = str(uuid4())
        token_jti = secrets.token_urlsafe(16)
        cert_expires = datetime.now(timezone.utc) + timedelta(
            seconds=settings.certification_session_token_ttl_seconds
        )

        db.create_certification_session(
            {
                "session_id": cert_session_id,
                "user_id": user_id,
                "expected_device_type": "laptop",
                "token_jti": token_jti,
                "status": "pending",
                "expires_at": cert_expires.isoformat(),
            }
        )

        cert_row = db.get_certification_session(cert_session_id)
        cert_id = cert_row["id"] if cert_row else None

        now = datetime.now(timezone.utc)
        db.update_agent_pairing_session(
            code,
            {
                "status": "PAIRED",
                "user_id": user_id,
                "certification_session_id": cert_id,
                "paired_at": now.isoformat(),
            },
        )

        self._audit.log(
            db,
            action="agent_pairing_claimed",
            resource_type="agent_pairing_session",
            resource_id=code,
            actor_user_id=user_id,
        )

        return AgentPairingClaimResponse(
            pairing_code=code,
            session_id=cert_session_id,
            user_id=user_id,
            message="Agent paired successfully. Return to the Certronx Agent on this device.",
        )

    def status(
        self,
        db: Database,
        *,
        pairing_code: str,
        device_nonce: str,
    ) -> AgentPairingStatusResponse:
        code = pairing_code.strip().upper()
        row = db.get_agent_pairing_session(code)
        if not row:
            raise ValueError("Invalid pairing code.")

        if row.get("device_nonce") != device_nonce.strip():
            raise ValueError("Pairing code does not match this agent session.")

        expires_at = _parse_dt(row.get("expires_at"))
        if expires_at and datetime.now(timezone.utc) > expires_at:
            if row.get("status") == "PENDING":
                db.update_agent_pairing_session(code, {"status": "EXPIRED"})
            return AgentPairingStatusResponse(status="EXPIRED", expires_at=expires_at)

        if row.get("status") == "PAIRED":
            cert = None
            cert_session_id = row.get("certification_session_id")
            if cert_session_id:
                cert = db.get_certification_session_by_id(str(cert_session_id))
            session_id = cert.get("session_id") if cert else None
            token = None
            if cert and session_id:
                token, _ = create_certification_token(
                    session_id=str(session_id),
                    jti=str(cert["token_jti"]),
                    user_id=str(cert["user_id"]),
                    expected_device_type=str(cert.get("expected_device_type") or "laptop"),
                    tenant_id=cert.get("tenant_id"),
                )
            return AgentPairingStatusResponse(
                status="PAIRED",
                user_id=str(row.get("user_id")) if row.get("user_id") else None,
                session_id=str(session_id) if session_id else None,
                certification_token=token,
                expires_at=expires_at,
            )

        return AgentPairingStatusResponse(status="PENDING", expires_at=expires_at)

    @staticmethod
    def _ensure_not_expired(row: dict[str, Any]) -> None:
        expires_at = _parse_dt(row.get("expires_at"))
        if expires_at and datetime.now(timezone.utc) > expires_at:
            raise ValueError("Pairing code has expired.")


def _generate_pairing_code(db: Database) -> str:
    for _ in range(30):
        code = "".join(secrets.choice(_PAIRING_ALPHABET) for _ in range(6))
        if not db.get_agent_pairing_session(code):
            return code
    raise RuntimeError("Could not generate pairing code.")


def _parse_dt(value: Any) -> datetime | None:
    if not value:
        return None
    if isinstance(value, datetime):
        dt = value
    else:
        dt = datetime.fromisoformat(str(value).replace("Z", "+00:00"))
    if dt.tzinfo is None:
        dt = dt.replace(tzinfo=timezone.utc)
    return dt
