#!/usr/bin/env python3
"""Empty VerifyTech Supabase Storage buckets via the Storage API (required by Supabase)."""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "backend-api"))

from app.config import settings  # noqa: E402
from app.db.supabase_client import get_supabase_admin  # noqa: E402

DEFAULT_BUCKETS = (
    "certification-evidence",
    "agent-releases",
)


def collect_file_paths(client, bucket: str, prefix: str = "") -> list[str]:
    """Recursively list all file paths in a bucket."""
    paths: list[str] = []
    listing = client.storage.from_(bucket).list(prefix or "")
    if not listing:
        return paths

    for item in listing:
        name = item.get("name")
        if not name:
            continue
        rel = f"{prefix}/{name}" if prefix else name
        # Files include an id; folder entries do not
        if item.get("id"):
            paths.append(rel)
        else:
            paths.extend(collect_file_paths(client, bucket, rel))

    return paths


def empty_bucket(client, bucket: str, *, dry_run: bool = False) -> int:
    paths = collect_file_paths(client, bucket)
    if not paths:
        print(f"  {bucket}: already empty")
        return 0

    print(f"  {bucket}: removing {len(paths)} object(s)")
    if dry_run:
        for p in paths[:10]:
            print(f"    - {p}")
        if len(paths) > 10:
            print(f"    ... and {len(paths) - 10} more")
        return len(paths)

    batch_size = 100
    removed = 0
    for i in range(0, len(paths), batch_size):
        batch = paths[i : i + batch_size]
        client.storage.from_(bucket).remove(batch)
        removed += len(batch)
    return removed


def main() -> int:
    parser = argparse.ArgumentParser(description="Empty Supabase storage buckets for database reset")
    parser.add_argument(
        "--bucket",
        action="append",
        dest="buckets",
        help="Bucket to empty (default: certification-evidence and agent-releases)",
    )
    parser.add_argument("--dry-run", action="store_true", help="List objects without deleting")
    args = parser.parse_args()

    buckets = tuple(args.buckets) if args.buckets else DEFAULT_BUCKETS
    if not settings.supabase_url or not settings.supabase_service_role_key:
        print("ERROR: SUPABASE_URL and SUPABASE_SERVICE_ROLE_KEY required in backend-api/.env", file=sys.stderr)
        return 1

    client = get_supabase_admin()
    total = 0
    print("→ Emptying storage buckets (Storage API)")
    for bucket in buckets:
        try:
            total += empty_bucket(client, bucket, dry_run=args.dry_run)
        except Exception as exc:
            print(f"  {bucket}: ERROR — {exc}", file=sys.stderr)
            return 1

    if args.dry_run:
        print(f"Done (dry run). Would remove {total} object(s).")
    else:
        print(f"Done. Removed {total} object(s).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
