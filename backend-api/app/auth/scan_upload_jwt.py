"""Short-lived upload tokens for paired Windows agent scan submissions."""

from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime, timedelta, timezone

import jwt
from jwt.exceptions import InvalidTokenError

from app.config import settings


@dataclass(frozen=True)
class ScanUploadClaims:
    scan_session_id: str
    jti: str
    owner_user_id: str | None
    tenant_id: str | None
    device_fingerprint: str


def _secret() -> str:
    secret = settings.scan_upload_jwt_secret or settings.supabase_jwt_secret
    if not secret:
        raise ValueError("SCAN_UPLOAD_JWT_SECRET is not configured")
    return secret


def create_upload_token(
    *,
    scan_session_id: str,
    jti: str,
    owner_user_id: str | None,
    tenant_id: str | None,
    device_fingerprint: str,
) -> tuple[str, int]:
    ttl = settings.scan_upload_token_ttl_seconds
    payload = {
        "sub": scan_session_id,
        "jti": jti,
        "purpose": "scan_upload",
        "aud": "certronx-scan-upload",
        "owner_user_id": owner_user_id,
        "tenant_id": tenant_id,
        "device_fingerprint": device_fingerprint,
        "exp": datetime.now(timezone.utc) + timedelta(seconds=ttl),
        "iat": datetime.now(timezone.utc),
    }
    token = jwt.encode(payload, _secret(), algorithm="HS256")
    return token, ttl


def decode_upload_token(token: str) -> ScanUploadClaims:
    payload = jwt.decode(
        token,
        _secret(),
        algorithms=["HS256"],
        audience="certronx-scan-upload",
    )
    if payload.get("purpose") != "scan_upload":
        raise InvalidTokenError("Invalid token purpose")

    scan_session_id = payload.get("sub")
    jti = payload.get("jti")
    if not scan_session_id or not jti:
        raise InvalidTokenError("Invalid upload token")

    return ScanUploadClaims(
        scan_session_id=str(scan_session_id),
        jti=str(jti),
        owner_user_id=payload.get("owner_user_id"),
        tenant_id=payload.get("tenant_id"),
        device_fingerprint=str(payload.get("device_fingerprint") or ""),
    )
