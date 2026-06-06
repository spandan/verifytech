"""Validate Supabase Auth JWTs issued to browser clients."""

from __future__ import annotations

from dataclasses import dataclass

import jwt
from jwt.exceptions import InvalidTokenError

from app.config import settings


@dataclass(frozen=True)
class SupabaseAuthClaims:
    user_id: str
    email: str | None


def decode_supabase_access_token(token: str) -> SupabaseAuthClaims:
    if not settings.supabase_jwt_secret:
        raise ValueError("SUPABASE_JWT_SECRET is not configured")

    payload = jwt.decode(
        token,
        settings.supabase_jwt_secret,
        algorithms=["HS256"],
        audience="authenticated",
    )
    user_id = payload.get("sub")
    if not user_id:
        raise ValueError("Token missing subject")
    email = payload.get("email")
    return SupabaseAuthClaims(user_id=str(user_id), email=email if isinstance(email, str) else None)


def try_decode_supabase_access_token(token: str) -> SupabaseAuthClaims | None:
    try:
        return decode_supabase_access_token(token)
    except (InvalidTokenError, ValueError):
        return None
