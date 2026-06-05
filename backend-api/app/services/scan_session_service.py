"""Short-lived scan sessions for the Windows agent (nonce-bound, no API keys)."""

from __future__ import annotations

import secrets
from datetime import datetime, timedelta, timezone
from typing import Any
from uuid import uuid4

from shared.hashing import compute_hardware_fingerprint

from app.config import settings
from app.db.models import Database
from app.schemas.dto import (
    ReportSubmitRequest,
    ScanSessionStartRequest,
    ScanSessionStartResponse,
    ScanSessionSubmitRequest,
    ScanSessionSubmitResponse,
)
from app.services.agent_report_adapter import agent_report_to_internal
from app.services.audit_service import AuditService
from app.services.evidence_storage_service import EvidenceStorageService
from app.services.inspection_report_service import InspectionReportService
from app.services.report_service import ReportService


class ScanSessionService:
    def __init__(self) -> None:
        self._reports = ReportService()
        self._audit = AuditService()
        self._evidence = EvidenceStorageService()
        self._inspection = InspectionReportService()

    def start(self, db: Database, body: ScanSessionStartRequest) -> ScanSessionStartResponse:
        if body.platform.lower() != "windows":
            raise ValueError("Only the windows platform is supported for scan sessions.")

        allowed = settings.allowed_agent_version_list
        if allowed and body.agent_version not in allowed:
            raise ValueError(f"Agent version '{body.agent_version}' is not allowed.")

        session_id = str(uuid4())
        nonce = secrets.token_urlsafe(32)
        expires_at = datetime.now(timezone.utc) + timedelta(minutes=settings.scan_session_ttl_minutes)

        db.create_scan_session(
            {
                "session_id": session_id,
                "nonce": nonce,
                "platform": body.platform.lower(),
                "agent_version": body.agent_version,
                "build_channel": body.build_channel,
                "status": "started",
                "expires_at": expires_at.isoformat(),
            }
        )

        return ScanSessionStartResponse(
            session_id=session_id,
            nonce=nonce,
            expires_at=expires_at,
        )

    def submit(
        self,
        db: Database,
        session_id: str,
        body: ScanSessionSubmitRequest,
    ) -> ScanSessionSubmitResponse:
        if body.session_id != session_id:
            raise ValueError("session_id in path does not match payload.")

        session = db.get_scan_session(session_id)
        if not session:
            raise ValueError("Scan session not found.")

        if session.status != "started":
            raise ValueError("This scan session has already been used or is no longer valid.")

        if session.nonce != body.nonce:
            raise ValueError("Invalid session nonce.")

        expires_at = session.expires_at
        if expires_at and expires_at.tzinfo is None:
            expires_at = expires_at.replace(tzinfo=timezone.utc)
        if expires_at and datetime.now(timezone.utc) > expires_at:
            db.update_scan_session(session_id, {"status": "expired"})
            raise ValueError("Scan session has expired. Start a new scan.")

        if session.agent_version != body.agent_version:
            raise ValueError("Agent version does not match the started session.")

        self._validate_scan_timing(body.scan_started_at, body.scan_completed_at)
        self._validate_scan_data(body.scan_data)

        report_payload = self._normalize_scan_data(body)
        internal_report = agent_report_to_internal(report_payload)
        expected_fingerprint = compute_hardware_fingerprint(body.scan_data)
        if expected_fingerprint != body.hardware_fingerprint:
            raise ValueError("Hardware fingerprint does not match scan data.")

        try:
            result = self._reports.submit(
                db,
                ReportSubmitRequest(
                    report=internal_report,
                    report_type="initial_certification",
                ),
            )
        except ValueError as exc:
            db.update_scan_session(
                session_id,
                {"status": "rejected", "rejection_reason": str(exc)},
            )
            raise

        if not result.certificate_code:
            db.update_scan_session(
                session_id,
                {"status": "rejected", "rejection_reason": "Certificate could not be issued"},
            )
            raise ValueError("Certificate could not be issued. Check Tier 1 identity fields.")

        report_url = result.public_url or f"{settings.public_base_url}/c/{result.certificate_code}"
        qr_url = report_url

        evidence_manifest: list[dict] = []
        if body.evidence_artifacts and result.certificate_code:
            evidence_manifest = self._evidence.upload_artifacts(
                result.certificate_code,
                [a.model_dump(mode="json") for a in body.evidence_artifacts],
            )

        inspection = self._inspection.build(body.scan_data, evidence_manifest)
        bundle = body.scan_data.get("evidence_bundle") or {}
        provenance = bundle.get("build_provenance") if isinstance(bundle, dict) else None
        if cert := db.get_certificate_by_code(result.certificate_code):
            payload = dict(cert.public_payload_json or {})
            payload["inspection_report"] = inspection
            payload["agent_provenance"] = provenance
            payload["evidence_manifest"] = evidence_manifest
            db.client.table("certificates").update(
                {"public_payload_json": payload, "condition_grade": inspection.get("certification_grade")}
            ).eq("id", cert.id).execute()

        db.update_scan_session(
            session_id,
            {
                "status": "submitted",
                "submitted_at": datetime.now(timezone.utc).isoformat(),
                "admin_mode": body.admin_mode,
                "scan_started_at": body.scan_started_at.isoformat(),
                "scan_completed_at": body.scan_completed_at.isoformat(),
                "hardware_fingerprint": body.hardware_fingerprint,
                "scan_data_json": body.scan_data,
                "certificate_id": result.certificate_id,
                "certificate_code": result.certificate_code,
                "report_url": report_url,
                "verification_url": report_url,
                "qr_code_url": qr_url,
            },
        )

        self._audit.log(
            db,
            action="scan_session_submitted",
            resource_type="scan_session",
            resource_id=session.id,
            metadata={
                "session_id": session_id,
                "agent_version": body.agent_version,
                "admin_mode": body.admin_mode,
                "certificate_code": result.certificate_code,
            },
        )

        return ScanSessionSubmitResponse(
            certificate_id=result.certificate_id or "",
            certificate_code=result.certificate_code,
            report_url=report_url,
            verification_url=report_url,
            qr_code_url=qr_url,
        )

    def _validate_scan_timing(self, started: datetime, completed: datetime) -> None:
        if completed < started:
            raise ValueError("scan_completed_at must be after scan_started_at.")

        duration = (completed - started).total_seconds()
        if duration < settings.scan_session_min_duration_seconds:
            raise ValueError("Scan completed too quickly to be valid.")
        if duration > settings.scan_session_max_duration_hours * 3600:
            raise ValueError("Scan duration exceeds the allowed maximum.")

    def _validate_scan_data(self, scan_data: dict[str, Any]) -> None:
        t1 = scan_data.get("tier1_certification_identity") or {}
        if not t1.get("serial_number_hash") and not t1.get("hardware_uuid_hash"):
            raise ValueError("Scan data must include hashed device identity (Tier 1).")

        if not scan_data.get("platform"):
            raise ValueError("Scan data must include platform.")

    def _normalize_scan_data(self, body: ScanSessionSubmitRequest) -> dict[str, Any]:
        data = dict(body.scan_data)
        data.setdefault("platform", body.platform)
        data.setdefault("schema_version", "1.0")
        ctx = dict(data.get("collection_context") or {})
        ctx.setdefault("mode", "initial_certification")
        ctx.setdefault("collector_version", body.agent_version)
        ctx["scan_session_id"] = body.session_id
        ctx["admin_mode"] = body.admin_mode
        data["collection_context"] = ctx
        meta = dict(data.get("agent_metadata") or {})
        meta.setdefault("collection_warnings", [])
        if body.admin_mode:
            meta["collection_warnings"] = list(meta.get("collection_warnings", [])) + ["scan_type:enhanced"]
        else:
            meta["collection_warnings"] = list(meta.get("collection_warnings", [])) + ["scan_type:standard"]
        data["agent_metadata"] = meta
        return data
