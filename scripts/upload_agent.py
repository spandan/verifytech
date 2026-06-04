#!/usr/bin/env python3
"""Upload a built Windows agent to Supabase Storage and register the version in agent_versions."""

from __future__ import annotations

import argparse
import hashlib
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "backend-api"))

from app.config import settings  # noqa: E402
from app.db.supabase_client import get_supabase_admin  # noqa: E402
from app.services.agent_storage_service import (  # noqa: E402
    storage_object_path,
    upload_agent_release,
)


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return f"sha256:{digest.hexdigest()}"


def upsert_agent_version(platform: str, version: str, object_path: str, checksum: str, release_notes: str) -> None:
    client = get_supabase_admin()
    client.table("agent_versions").update({"is_active": False}).eq("platform", platform).execute()

    existing = (
        client.table("agent_versions")
        .select("id")
        .eq("platform", platform)
        .eq("version", version)
        .limit(1)
        .execute()
    )
    payload = {
        "platform": platform,
        "version": version,
        "download_url": object_path,
        "checksum": checksum,
        "is_active": True,
        "release_notes": release_notes,
    }
    if existing.data:
        client.table("agent_versions").update(payload).eq("id", existing.data[0]["id"]).execute()
    else:
        client.table("agent_versions").insert(payload).execute()


def main() -> int:
    parser = argparse.ArgumentParser(description="Upload DeviceCertAgent.exe to Supabase Storage")
    parser.add_argument(
        "--file",
        type=Path,
        default=ROOT / "agent" / "windows" / "publish" / "DeviceCertAgent.exe",
        help="Path to the built executable",
    )
    parser.add_argument("--platform", default="windows")
    parser.add_argument("--version", required=True, help="Release version (e.g. 0.1.0)")
    parser.add_argument("--notes", default="", help="Release notes stored in agent_versions")
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    if not args.file.exists():
        print(f"File not found: {args.file}", file=sys.stderr)
        print("Build on Windows first: ./scripts/build-agent.sh", file=sys.stderr)
        return 1

    object_path = storage_object_path(args.platform, args.version)
    checksum = sha256_file(args.file)

    print(f"Bucket:  {settings.supabase_agent_bucket}")
    print(f"Object:  {object_path}")
    print(f"Download: via GET /api/agents/{args.platform} (signed URL, TTL {settings.supabase_agent_signed_url_ttl_seconds}s)")
    print(f"SHA-256: {checksum}")

    if args.dry_run:
        return 0

    upload_agent_release(args.file, args.platform, args.version)
    upsert_agent_version(args.platform, args.version, object_path, checksum, args.notes)
    print("Upload complete and agent_versions updated.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
