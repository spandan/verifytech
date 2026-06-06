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


def _claims_from_payload(payload: dict) -> SupabaseAuthClaims:
    user_id = payload.get("sub")
    if not user_id:
        raise ValueError("Token missing subject")
    email = payload.get("email")
    return SupabaseAuthClaims(user_id=str(user_id), email=email if isinstance(email, str) else None)


def _decode_hs256(token: str) -> SupabaseAuthClaims:
    if not settings.supabase_jwt_secret:
        raise ValueError("SUPABASE_JWT_SECRET is not configured")

    payload = jwt.decode(
        token,
        settings.supabase_jwt_secret,
        algorithms=["HS256"],
        audience="authenticated",
    )
    return _claims_from_payload(payload)


def _decode_via_supabase(token: str) -> SupabaseAuthClaims:
    """Validate ES256 (JWKS) or HS256 tokens via Supabase Auth."""
    from app.db.supabase_client import get_supabase_admin

    response = get_supabase_admin().auth.get_claims(jwt=token)
    if not response:
        raise ValueError("Invalid token")
    return _claims_from_payload(response.claims)


def decode_supabase_access_token(token: str) -> SupabaseAuthClaims:
    try:
        return _decode_hs256(token)
    except (InvalidTokenError, ValueError):
        return _decode_via_supabase(token)


def try_decode_supabase_access_token(token: str) -> SupabaseAuthClaims | None:
    try:
        return decode_supabase_access_token(token)
    except Exception:
        return None
