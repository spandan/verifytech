"""Supabase Storage helpers for agent release binaries."""

from __future__ import annotations

from pathlib import Path

from app.config import settings
from app.db.supabase_client import get_supabase_admin


def is_absolute_url(value: str) -> bool:
    return value.startswith(("http://", "https://"))


def is_api_path(value: str) -> bool:
    return value.startswith("/")


def is_storage_object_path(value: str) -> bool:
    """Object key inside the configured agent bucket (e.g. windows/0.1.0/DeviceCertAgent.exe)."""
    return not is_absolute_url(value) and not is_api_path(value)


def storage_object_path(platform: str, version: str, filename: str | None = None) -> str:
    name = filename or settings.supabase_agent_filename
    return f"{platform.lower().strip('/')}/{version.strip('/')}/{name}"


def create_signed_download_url(object_path: str) -> str:
    """Return a short-lived signed URL for a private storage object."""
    normalized = object_path.lstrip("/")
    client = get_supabase_admin()
    result = client.storage.from_(settings.supabase_agent_bucket).create_signed_url(
        normalized,
        settings.supabase_agent_signed_url_ttl_seconds,
        options={"download": settings.supabase_agent_filename},
    )
    signed = result.get("signedURL") or result.get("signedUrl")
    if not signed:
        raise RuntimeError(f"Failed to create signed URL for {normalized}")
    return signed


def resolve_agent_download_url(download_url: str) -> str:
    """
    Resolve agent_versions.download_url to a browser-downloadable URL.

    Supported formats:
    - Absolute URL: returned as-is (external CDN)
    - API path (/agents/...): prefixed with API_BASE_URL (local dev fallback)
    - Storage object path: short-lived signed Supabase URL (requires API call)
    """
    if is_absolute_url(download_url):
        return download_url
    if is_api_path(download_url):
        return f"{settings.api_base_url.rstrip('/')}{download_url}"
    return create_signed_download_url(download_url)


def upload_agent_release(
    file_path: Path,
    platform: str,
    version: str,
    filename: str | None = None,
    upsert: bool = True,
) -> str:
    """Upload an agent binary to Supabase Storage. Returns the object path."""
    path = storage_object_path(platform, version, filename)
    data = file_path.read_bytes()
    options = {
        "content-type": "application/octet-stream",
        "upsert": str(upsert).lower(),
    }
    client = get_supabase_admin()
    client.storage.from_(settings.supabase_agent_bucket).upload(path, data, file_options=options)
    return path
