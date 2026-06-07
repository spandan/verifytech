"""Short-lived signed tokens for website-launched certification sessions."""

from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime, timedelta, timezone

import jwt
from jwt.exceptions import InvalidTokenError

from app.config import settings


@dataclass(frozen=True)
class CertificationSessionClaims:
    session_id: str
    jti: str
    user_id: str
    expected_device_type: str
    tenant_id: str | None = None


def _secret() -> str:
    secret = settings.certification_session_jwt_secret or settings.scan_upload_jwt_secret or settings.supabase_jwt_secret
    if not secret:
        raise ValueError("CERTIFICATION_SESSION_JWT_SECRET is not configured")
    return secret


def create_certification_token(
    *,
    session_id: str,
    jti: str,
    user_id: str,
    expected_device_type: str,
    tenant_id: str | None = None,
) -> tuple[str, int]:
    ttl = settings.certification_session_token_ttl_seconds
    payload = {
        "sub": session_id,
        "jti": jti,
        "purpose": "certification_session",
        "aud": "certronx-certification-session",
        "user_id": user_id,
        "expected_device_type": expected_device_type,
        "tenant_id": tenant_id,
        "exp": datetime.now(timezone.utc) + timedelta(seconds=ttl),
        "iat": datetime.now(timezone.utc),
    }
    return jwt.encode(payload, _secret(), algorithm="HS256"), ttl


def decode_certification_token(token: str) -> CertificationSessionClaims:
    payload = jwt.decode(
        token,
        _secret(),
        algorithms=["HS256"],
        audience="certronx-certification-session",
    )
    if payload.get("purpose") != "certification_session":
        raise InvalidTokenError("Invalid token purpose")

    session_id = payload.get("sub")
    jti = payload.get("jti")
    user_id = payload.get("user_id")
    expected_device_type = payload.get("expected_device_type")
    if not session_id or not jti or not user_id or not expected_device_type:
        raise InvalidTokenError("Invalid certification token")

    return CertificationSessionClaims(
        session_id=str(session_id),
        jti=str(jti),
        user_id=str(user_id),
        expected_device_type=str(expected_device_type),
        tenant_id=payload.get("tenant_id"),
    )
