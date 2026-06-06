"""Validate Supabase Auth JWTs issued to browser clients."""

from __future__ import annotations

from dataclasses import dataclass
from functools import lru_cache

import jwt
from jwt import PyJWKClient
from jwt.exceptions import InvalidTokenError, PyJWKClientError

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


@lru_cache
def _jwks_client() -> PyJWKClient:
    base = settings.supabase_url.rstrip("/")
    return PyJWKClient(f"{base}/auth/v1/.well-known/jwks.json", cache_keys=True)


def _decode_jwks(token: str) -> SupabaseAuthClaims:
    key = _jwks_client().get_signing_key_from_jwt(token)
    payload = jwt.decode(
        token,
        key.key,
        algorithms=["ES256", "RS256"],
        audience="authenticated",
    )
    return _claims_from_payload(payload)


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


def _decode_via_supabase_api(token: str) -> SupabaseAuthClaims:
    from app.db.supabase_client import get_supabase_admin

    response = get_supabase_admin().auth.get_user(jwt=token)
    if not response or not response.user:
        raise ValueError("Invalid token")
    user = response.user
    return SupabaseAuthClaims(user_id=str(user.id), email=user.email)


def decode_supabase_access_token(token: str) -> SupabaseAuthClaims:
    last_error: Exception | None = None

    for decoder in (_decode_jwks, _decode_hs256, _decode_via_supabase_api):
        try:
            return decoder(token)
        except (InvalidTokenError, PyJWKClientError, ValueError, TypeError) as exc:
            last_error = exc
            continue

    raise ValueError("Invalid token") from last_error


def try_decode_supabase_access_token(token: str) -> SupabaseAuthClaims | None:
    try:
        return decode_supabase_access_token(token)
    except Exception:
        return None
