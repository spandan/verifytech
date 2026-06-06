"""Browser-initiated pairing for seamless Windows agent scan sessions."""

from __future__ import annotations

import secrets
import string
from datetime import datetime, timedelta, timezone
from typing import Any
from uuid import uuid4

from app.auth.scan_upload_jwt import create_upload_token
from app.config import settings
from app.db.models import Database
from app.schemas.dto import (
    ScanPairingCreateRequest,
    ScanPairingCreateResponse,
    ScanPairingExchangeRequest,
    ScanPairingExchangeResponse,
)
from app.services.audit_service import AuditService

_PAIRING_ALLOWED_ROLES = {"owner", "admin", "technician"}


class ScanPairingService:
    def __init__(self) -> None:
        self._audit = AuditService()

    def create_pairing(
        self,
        db: Database,
        *,
        user_id: str,
        body: ScanPairingCreateRequest,
    ) -> ScanPairingCreateResponse:
        owner_user_id = user_id
        tenant_id = None

        if body.business_id:
            tenant_id = self._authorize_business(db, user_id, body.business_id)
            owner_user_id = None

        pairing_code = _generate_pairing_code()
        expires_at = datetime.now(timezone.utc) + timedelta(minutes=settings.scan_pairing_ttl_minutes)

        db.create_scan_pairing_session(
            {
                "pairing_code": pairing_code,
                "owner_user_id": owner_user_id,
                "tenant_id": tenant_id,
                "created_by_user_id": user_id,
                "status": "pending",
                "expires_at": expires_at.isoformat(),
                "max_uses": 1,
                "uses_count": 0,
            }
        )

        deep_link = f"certronx://scan/start?pairingCode={pairing_code}"
        self._audit.log(
            db,
            action="pairing_session_created",
            resource_type="scan_pairing_session",
            resource_id=pairing_code,
            actor_user_id=user_id,
            tenant_id=tenant_id,
        )

        return ScanPairingCreateResponse(
            pairing_code=pairing_code,
            expires_at=expires_at,
            deep_link=deep_link,
        )

    def exchange(
        self,
        db: Database,
        body: ScanPairingExchangeRequest,
    ) -> ScanPairingExchangeResponse:
        pairing = db.get_scan_pairing_session(body.pairing_code)
        if not pairing:
            raise ValueError("Invalid pairing code.")

        self._validate_pairing_pending(pairing)

        allowed = settings.allowed_agent_version_list
        if allowed and body.agent_version not in allowed:
            raise ValueError(f"Agent version '{body.agent_version}' is not allowed.")

        session_id = str(uuid4())
        nonce = secrets.token_urlsafe(32)
        session_expires = datetime.now(timezone.utc) + timedelta(minutes=settings.scan_session_ttl_minutes)
        upload_jti = secrets.token_urlsafe(16)

        session_payload: dict[str, Any] = {
            "session_id": session_id,
            "nonce": nonce,
            "platform": "windows",
            "agent_version": body.agent_version,
            "build_channel": "production",
            "status": "exchanged",
            "expires_at": session_expires.isoformat(),
            "user_id": pairing.get("owner_user_id"),
            "tenant_id": pairing.get("tenant_id"),
            "pairing_session_id": pairing["id"],
            "upload_jti": upload_jti,
            "paired_device_fingerprint": body.device_fingerprint.strip(),
        }
        db.create_scan_session(session_payload)

        now = datetime.now(timezone.utc)
        db.update_scan_pairing_session(
            pairing["pairing_code"],
            {
                "status": "exchanged",
                "uses_count": int(pairing.get("uses_count") or 0) + 1,
                "paired_device_fingerprint": body.device_fingerprint.strip(),
                "agent_version": body.agent_version,
                "scan_session_id": session_id,
                "exchanged_at": now.isoformat(),
            },
        )

        upload_token, expires_in = create_upload_token(
            scan_session_id=session_id,
            jti=upload_jti,
            owner_user_id=pairing.get("owner_user_id"),
            tenant_id=pairing.get("tenant_id"),
            device_fingerprint=body.device_fingerprint.strip(),
        )

        linked_name = self._linked_account_label(db, pairing)

        return ScanPairingExchangeResponse(
            upload_token=upload_token,
            expires_in_seconds=expires_in,
            scan_session_id=session_id,
            linked_account_name=linked_name,
        )

    def mark_uploaded(self, db: Database, pairing_code: str | None) -> None:
        if not pairing_code:
            return
        db.update_scan_pairing_session(
            pairing_code,
            {
                "status": "uploaded",
                "uploaded_at": datetime.now(timezone.utc).isoformat(),
            },
        )

    def _validate_pairing_pending(self, pairing: dict[str, Any]) -> None:
        if pairing.get("status") != "pending":
            raise ValueError("Pairing code has already been used or is no longer valid.")

        expires_at = pairing.get("expires_at")
        if isinstance(expires_at, str):
            expires_at = datetime.fromisoformat(expires_at.replace("Z", "+00:00"))
        if expires_at and expires_at.tzinfo is None:
            expires_at = expires_at.replace(tzinfo=timezone.utc)
        if expires_at and datetime.now(timezone.utc) > expires_at:
            raise ValueError("Pairing code has expired.")

        uses = int(pairing.get("uses_count") or 0)
        max_uses = int(pairing.get("max_uses") or 1)
        if uses >= max_uses:
            raise ValueError("Pairing code has already been used.")

        if not pairing.get("owner_user_id") and not pairing.get("tenant_id"):
            raise ValueError("Pairing session is misconfigured.")

    def _authorize_business(self, db: Database, user_id: str, business_id: str) -> str:
        memberships = db.get_user_tenants(user_id)
        for tenant, membership in memberships:
            if tenant.id != business_id:
                continue
            role = membership.role.value if hasattr(membership.role, "value") else str(membership.role)
            if role in _PAIRING_ALLOWED_ROLES:
                return tenant.id
        raise ValueError("You do not have permission to start scans for this business.")

    def _linked_account_label(self, db: Database, pairing: dict[str, Any]) -> str | None:
        if pairing.get("owner_user_id"):
            profile = db.get_profile(str(pairing["owner_user_id"]))
            return profile.email or profile.full_name or "Your Certronx account"
        if pairing.get("tenant_id"):
            tenant = db.get_tenant(str(pairing["tenant_id"]))
            return tenant.name if tenant else "Business workspace"
        return None


def _generate_pairing_code() -> str:
    alphabet = string.ascii_uppercase + string.digits
    chunks = ["".join(secrets.choice(alphabet) for _ in range(4)) for _ in range(2)]
    return f"CPR-{'-'.join(chunks)}"
