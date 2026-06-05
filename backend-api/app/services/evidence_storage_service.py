"""Store certification evidence artifacts in Supabase Storage."""

from __future__ import annotations

import base64
from typing import Any

from app.config import settings
from app.db.supabase_client import get_supabase_admin


class EvidenceStorageService:
    def __init__(self) -> None:
        self._bucket = settings.supabase_evidence_bucket

    def upload_artifacts(
        self,
        certificate_code: str,
        artifacts: list[dict[str, Any]],
    ) -> list[dict[str, Any]]:
        client = get_supabase_admin()
        manifest: list[dict[str, Any]] = []

        for artifact in artifacts:
            artifact_type = artifact.get("artifact_type") or "unknown"
            content_b64 = artifact.get("content_base64") or ""
            if not content_b64:
                continue

            data = base64.b64decode(content_b64)
            path = f"{certificate_code.lower()}/{artifact_type}.bin"
            content_type = artifact.get("content_type") or "application/octet-stream"

            client.storage.from_(self._bucket).upload(
                path,
                data,
                file_options={
                    "content-type": content_type,
                    "upsert": "true",
                },
            )

            signed = client.storage.from_(self._bucket).create_signed_url(
                path,
                settings.supabase_evidence_signed_url_ttl_seconds,
            )
            signed_url = signed.get("signedURL") or signed.get("signedUrl")

            manifest.append(
                {
                    "artifact_type": artifact_type,
                    "storage_path": path,
                    "content_type": content_type,
                    "source": artifact.get("source"),
                    "collected_at": artifact.get("collected_at"),
                    "signed_url": signed_url,
                    "label": _label_for(artifact_type),
                }
            )

        return manifest


def _label_for(artifact_type: str) -> str:
    return {
        "battery_report": "Battery Report",
        "storage_smart": "SMART / NVMe Health",
        "benchmark_telemetry": "Thermal & Benchmark Telemetry",
        "security_snapshot": "Security Snapshot",
        "validation_results": "Validation Results",
        "memory_stability": "Memory Stability Test",
    }.get(artifact_type, artifact_type.replace("_", " ").title())
