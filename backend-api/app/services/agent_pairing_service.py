"""Agent-initiated pairing for manual launch and token recovery."""

from __future__ import annotations

import logging
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
    AgentPairingClaimStatusResponse,
    AgentPairingCreateRequest,
    AgentPairingCreateResponse,
    AgentPairingStatusResponse,
)
from app.services.audit_service import AuditService

logger = logging.getLogger(__name__)

_PAIRING_ALPHABET = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"


class AgentPairingService:
    def __init__(self) -> None:
        self._audit = AuditService()

    def create(self, db: Database, body: AgentPairingCreateRequest) -> AgentPairingCreateResponse:
        device_nonce = body.device_nonce.strip()
        if len(device_nonce) < 8:
            raise ValueError("device_nonce is required.")

        paired = db.get_agent_pairing_paired_by_device_nonce(device_nonce)
        if paired and _pairing_window_open(paired):
            expires_at = _parse_dt(paired.get("expires_at"))
            return AgentPairingCreateResponse(
                pairing_code=str(paired["pairing_code"]),
                expires_in_minutes=settings.agent_pairing_ttl_minutes,
                expires_at=expires_at or datetime.now(timezone.utc),
            )

        existing = db.get_agent_pairing_session_by_device_nonce(device_nonce)
        if existing and existing.get("status") == "PENDING":
            expires_at = _parse_dt(existing.get("expires_at"))
            if expires_at and datetime.now(timezone.utc) < expires_at:
                return AgentPairingCreateResponse(
                    pairing_code=str(existing["pairing_code"]),
                    expires_in_minutes=settings.agent_pairing_ttl_minutes,
                    expires_at=expires_at,
                )

        db.expire_pending_agent_pairings_for_device(device_nonce)

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
            return _claim_idempotent(db, code, row, user_id)

        self._ensure_not_expired(row)

        if row.get("status") != "PENDING":
            raise ValueError("Pairing code is no longer valid.")

        cert_session_id = str(uuid4())
        token_jti = secrets.token_urlsafe(16)
        cert_expires = datetime.now(timezone.utc) + timedelta(
            seconds=settings.certification_session_token_ttl_seconds
        )

        cert_row = db.create_certification_session(
            {
                "session_id": cert_session_id,
                "user_id": user_id,
                "expected_device_type": "laptop",
                "token_jti": token_jti,
                "status": "pending",
                "expires_at": cert_expires.isoformat(),
            }
        )
        cert_id = cert_row["id"]

        now = datetime.now(timezone.utc)
        claimed = db.claim_agent_pairing_session_if_pending(
            code,
            {
                "status": "PAIRED",
                "user_id": user_id,
                "certification_session_id": cert_id,
                "paired_at": now.isoformat(),
            },
        )
        if not claimed:
            row = db.get_agent_pairing_session(code)
            if row and row.get("status") == "PAIRED":
                return _claim_idempotent(db, code, row, user_id)
            raise ValueError("Pairing code is no longer valid.")

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

    def claim_status(
        self,
        db: Database,
        *,
        user_id: str,
        pairing_code: str,
    ) -> AgentPairingClaimStatusResponse:
        code = pairing_code.strip().upper()
        row = db.get_agent_pairing_session(code)
        if not row:
            raise ValueError("Invalid pairing code.")

        if row.get("status") == "PAIRED" and _same_user(str(row.get("user_id") or ""), user_id):
            return AgentPairingClaimStatusResponse(
                connected=True,
                pairing_code=code,
                message="Already connected. Return to the Certronx Agent on this device.",
            )

        if row.get("status") == "PAIRED":
            return AgentPairingClaimStatusResponse(
                connected=False,
                pairing_code=code,
                message="This pairing code has already been used by another account.",
            )

        return AgentPairingClaimStatusResponse(
            connected=False,
            pairing_code=code,
            message="Not connected yet. Click Connect agent to link this device.",
        )

    def status_for_device(self, db: Database, *, device_nonce: str) -> AgentPairingStatusResponse:
        nonce = device_nonce.strip()
        paired = db.get_agent_pairing_paired_by_device_nonce(nonce)
        if paired and _pairing_window_open(paired):
            return _build_paired_status(db, paired)

        pending = db.get_agent_pairing_session_by_device_nonce(nonce)
        if pending:
            return self.status(
                db,
                pairing_code=str(pending["pairing_code"]),
                device_nonce=nonce,
            )

        return AgentPairingStatusResponse(status="PENDING")

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
            return _build_paired_status(db, row, expires_at)

        return AgentPairingStatusResponse(status="PENDING", expires_at=expires_at)

    @staticmethod
    def _ensure_not_expired(row: dict[str, Any]) -> None:
        expires_at = _parse_dt(row.get("expires_at"))
        if expires_at and datetime.now(timezone.utc) > expires_at:
            raise ValueError("Pairing code has expired.")


def _build_paired_status(
    db: Database,
    row: dict[str, Any],
    expires_at: datetime | None = None,
) -> AgentPairingStatusResponse:
    expires_at = expires_at or _parse_dt(row.get("expires_at"))
    session_id = _resolve_cert_session_id(db, row)
    token = None
    cert = _resolve_cert_row(db, row)
    if cert and session_id:
        try:
            token, _ = create_certification_token(
                session_id=str(session_id),
                jti=str(cert["token_jti"]),
                user_id=str(cert["user_id"]),
                expected_device_type=str(cert.get("expected_device_type") or "laptop"),
                tenant_id=cert.get("tenant_id"),
            )
        except Exception as exc:
            logger.exception(
                "Failed to mint certification token for pairing code %s",
                row.get("pairing_code"),
            )
            raise ValueError(
                "Pairing is linked but the agent token could not be issued. Contact support."
            ) from exc
    elif row.get("status") == "PAIRED":
        logger.error(
            "Paired session %s missing certification session (certification_session_id=%s)",
            row.get("pairing_code"),
            row.get("certification_session_id"),
        )

    return AgentPairingStatusResponse(
        status="PAIRED",
        user_id=str(row.get("user_id")) if row.get("user_id") else None,
        session_id=str(session_id) if session_id else None,
        certification_token=token,
        expires_at=expires_at,
    )


def _resolve_cert_row(db: Database, pairing_row: dict[str, Any]) -> dict[str, Any] | None:
    cert_session_id = pairing_row.get("certification_session_id")
    if not cert_session_id:
        return None
    return db.get_certification_session_by_id(str(cert_session_id))


def _resolve_cert_session_id(db: Database, pairing_row: dict[str, Any]) -> str | None:
    cert = _resolve_cert_row(db, pairing_row)
    if not cert:
        return None
    session_id = cert.get("session_id")
    return str(session_id) if session_id else None


def _pairing_window_open(row: dict[str, Any]) -> bool:
    expires_at = _parse_dt(row.get("expires_at"))
    return not expires_at or datetime.now(timezone.utc) <= expires_at


def _same_user(left: str, right: str) -> bool:
    return left.strip().lower() == right.strip().lower()


def _claim_idempotent(
    db: Database,
    code: str,
    row: dict[str, Any],
    user_id: str,
) -> AgentPairingClaimResponse:
    existing_user = str(row.get("user_id") or "")
    if existing_user and _same_user(existing_user, user_id):
        session_id = _resolve_cert_session_id(db, row)
        return AgentPairingClaimResponse(
            pairing_code=code,
            session_id=session_id or "",
            user_id=user_id,
            message="Already connected. Return to the Certronx Agent on this device.",
        )
    raise ValueError("This pairing code has already been used.")


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
