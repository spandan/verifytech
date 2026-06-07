"""Signed-token certification sessions launched from the Certronx website."""

from __future__ import annotations

import secrets
from datetime import datetime, timedelta, timezone
from typing import Any
from uuid import uuid4

from app.auth.certification_session_jwt import CertificationSessionClaims, create_certification_token, decode_certification_token
from app.auth.scan_upload_jwt import create_upload_token
from app.config import settings
from app.db.models import Database
from app.schemas.dto import (
    CertificationSessionBeginScanRequest,
    CertificationSessionBeginScanResponse,
    CertificationSessionCreateRequest,
    CertificationSessionCreateResponse,
    CertificationSessionValidateRequest,
    CertificationSessionValidateResponse,
)
from app.services.audit_service import AuditService


class CertificationSessionService:
    def __init__(self) -> None:
        self._audit = AuditService()

    def create(
        self,
        db: Database,
        *,
        user_id: str,
        body: CertificationSessionCreateRequest,
    ) -> CertificationSessionCreateResponse:
        expected = (body.expected_device_type or "laptop").strip().lower()
        if expected != "laptop":
            raise ValueError("Only laptop certification sessions are supported in Phase 1.")

        session_id = str(uuid4())
        token_jti = secrets.token_urlsafe(16)
        expires_at = datetime.now(timezone.utc) + timedelta(
            seconds=settings.certification_session_token_ttl_seconds
        )

        db.create_certification_session(
            {
                "session_id": session_id,
                "user_id": user_id,
                "tenant_id": body.business_id,
                "expected_device_type": expected,
                "token_jti": token_jti,
                "status": "pending",
                "expires_at": expires_at.isoformat(),
            }
        )

        token, ttl = create_certification_token(
            session_id=session_id,
            jti=token_jti,
            user_id=user_id,
            expected_device_type=expected,
            tenant_id=body.business_id,
        )
        deep_link = f"certronx://scan/start?token={token}"

        self._audit.log(
            db,
            action="certification_session_created",
            resource_type="certification_session",
            resource_id=session_id,
            actor_user_id=user_id,
            tenant_id=body.business_id,
            metadata={"expected_device_type": expected},
        )

        return CertificationSessionCreateResponse(
            session_id=session_id,
            token=token,
            expires_at=expires_at,
            expires_in_seconds=ttl,
            deep_link=deep_link,
            expected_device_type=expected,
        )

    def validate(
        self,
        db: Database,
        body: CertificationSessionValidateRequest,
    ) -> CertificationSessionValidateResponse:
        claims = decode_certification_token(body.token)
        row = self._load_pending_session(db, claims)

        profile = db.get_profile(str(row["user_id"]))
        linked_name = (profile.full_name or profile.email or "Your Certronx account") if profile else "Your Certronx account"

        now = datetime.now(timezone.utc)
        db.update_certification_session(
            claims.session_id,
            {
                "status": "validated",
                "validated_at": now.isoformat(),
            },
        )

        return CertificationSessionValidateResponse(
            session_id=claims.session_id,
            user_id=claims.user_id,
            expected_device_type=claims.expected_device_type,
            linked_account_name=linked_name,
        )

    def begin_scan(
        self,
        db: Database,
        body: CertificationSessionBeginScanRequest,
    ) -> CertificationSessionBeginScanResponse:
        claims = decode_certification_token(body.token)
        row = self._load_validated_session(db, claims)

        allowed = settings.allowed_agent_version_list
        if allowed and body.agent_version not in allowed:
            raise ValueError(f"Agent version '{body.agent_version}' is not allowed.")

        scan_session_id = str(uuid4())
        nonce = secrets.token_urlsafe(32)
        session_expires = datetime.now(timezone.utc) + timedelta(minutes=settings.scan_session_ttl_minutes)
        upload_jti = secrets.token_urlsafe(16)
        now = datetime.now(timezone.utc)

        db.create_scan_session(
            {
                "session_id": scan_session_id,
                "nonce": nonce,
                "platform": "windows",
                "agent_version": body.agent_version,
                "build_channel": "production",
                "status": "exchanged",
                "expires_at": session_expires.isoformat(),
                "user_id": row["user_id"],
                "tenant_id": row.get("tenant_id"),
                "certification_session_id": row["id"],
                "upload_jti": upload_jti,
                "paired_device_fingerprint": body.device_fingerprint.strip(),
            }
        )

        db.update_certification_session(
            claims.session_id,
            {
                "status": "exchanged",
                "exchanged_at": now.isoformat(),
                "agent_version": body.agent_version,
                "scan_session_id": scan_session_id,
                "paired_device_fingerprint": body.device_fingerprint.strip(),
            },
        )

        upload_token, expires_in = create_upload_token(
            scan_session_id=scan_session_id,
            jti=upload_jti,
            owner_user_id=row.get("user_id"),
            tenant_id=row.get("tenant_id"),
            device_fingerprint=body.device_fingerprint.strip(),
        )

        profile = db.get_profile(str(row["user_id"]))
        linked_name = (profile.full_name or profile.email or "Your Certronx account") if profile else "Your Certronx account"

        return CertificationSessionBeginScanResponse(
            upload_token=upload_token,
            expires_in_seconds=expires_in,
            scan_session_id=scan_session_id,
            linked_account_name=linked_name,
        )

    def mark_uploaded(self, db: Database, certification_session_id: str | None) -> None:
        if not certification_session_id:
            return
        row = db.get_certification_session_by_id(certification_session_id)
        if not row:
            return
        db.update_certification_session(
            row["session_id"],
            {
                "status": "uploaded",
                "uploaded_at": datetime.now(timezone.utc).isoformat(),
            },
        )

    def _load_pending_session(self, db: Database, claims: CertificationSessionClaims) -> dict[str, Any]:
        row = db.get_certification_session(claims.session_id)
        if not row:
            raise ValueError("Invalid certification session.")
        if row.get("token_jti") != claims.jti:
            raise ValueError("Certification token has already been used or is invalid.")
        if row.get("status") not in ("pending", "validated"):
            raise ValueError("Certification session is no longer valid.")
        self._ensure_not_expired(row)
        if str(row.get("user_id")) != claims.user_id:
            raise ValueError("Certification token does not match session owner.")
        if str(row.get("expected_device_type")) != claims.expected_device_type:
            raise ValueError("Certification token device type mismatch.")
        return row

    def _load_validated_session(self, db: Database, claims: CertificationSessionClaims) -> dict[str, Any]:
        row = db.get_certification_session(claims.session_id)
        if not row:
            raise ValueError("Invalid certification session.")
        if row.get("token_jti") != claims.jti:
            raise ValueError("Certification token has already been used or is invalid.")
        if row.get("status") not in ("pending", "validated"):
            raise ValueError("Certification session is no longer valid.")
        self._ensure_not_expired(row)
        return row

    @staticmethod
    def _ensure_not_expired(row: dict[str, Any]) -> None:
        expires_at = row.get("expires_at")
        if isinstance(expires_at, str):
            expires_at = datetime.fromisoformat(expires_at.replace("Z", "+00:00"))
        if expires_at and expires_at.tzinfo is None:
            expires_at = expires_at.replace(tzinfo=timezone.utc)
        if expires_at and datetime.now(timezone.utc) > expires_at:
            raise ValueError("Certification session has expired.")
