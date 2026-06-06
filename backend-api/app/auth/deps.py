from __future__ import annotations

from dataclasses import dataclass

from fastapi import Depends, Header, HTTPException

from app.auth.jwt import try_decode_supabase_access_token
from app.db.models import Database, get_db


@dataclass(frozen=True)
class AuthUser:
    id: str
    email: str | None = None


def _bearer_token(authorization: str | None) -> str | None:
    if not authorization:
        return None
    parts = authorization.split(" ", 1)
    if len(parts) != 2 or parts[0].lower() != "bearer":
        return None
    return parts[1].strip() or None


def get_optional_user(
    authorization: str | None = Header(default=None),
    db: Database = Depends(get_db),
) -> AuthUser | None:
    token = _bearer_token(authorization)
    if not token:
        return None
    claims = try_decode_supabase_access_token(token)
    if not claims:
        return None
    profile = db.upsert_profile(claims.user_id, email=claims.email)
    return AuthUser(id=profile.id, email=profile.email or claims.email)


def get_current_user(user: AuthUser | None = Depends(get_optional_user)) -> AuthUser:
    if not user:
        raise HTTPException(status_code=401, detail="Authentication required")
    return user


def get_scan_upload_claims(
    authorization: str | None = Header(default=None),
) -> ScanUploadClaims:
    token = _bearer_token(authorization)
    if not token:
        raise HTTPException(status_code=401, detail="Upload token required")

    try:
        from jwt.exceptions import InvalidTokenError

        from app.auth.scan_upload_jwt import decode_upload_token

        return decode_upload_token(token)
    except Exception:
        raise HTTPException(status_code=401, detail="Invalid or expired upload token")
