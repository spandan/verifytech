"""Report submission and device registration."""

from __future__ import annotations

from typing import Any

from schema_engine.validator import SchemaValidator
from shared.hashing import compute_identity_hash, compute_value_hash, hash_payload

from app.db.models import Database, Device
from app.schemas.dto import ReportSubmitRequest, ReportSubmitResponse
from app.services.audit_service import AuditService
from app.services.certification_service import CertificationService


class ReportService:
    def __init__(self):
        self._validator = SchemaValidator()
        self._certification = CertificationService()
        self._audit = AuditService()

    def submit(
        self,
        db: Database,
        request: ReportSubmitRequest,
        issue_certificate: bool = True,
    ) -> ReportSubmitResponse:
        validation = self._validator.validate(request.report)
        if not validation.valid:
            raise ValueError(f"Schema validation failed: {validation.tier1_errors}")

        parsed = self._validator.parse(request.report)
        tier1 = parsed.tier1.model_dump(mode="json")
        tier2 = parsed.tier2.model_dump(mode="json")
        tier3 = parsed.tier3.model_dump(mode="json")

        identity_hash = compute_identity_hash(tier1)
        value_hash = compute_value_hash(tier2) if validation.tier2_complete else None
        report_hash = hash_payload(request.report)

        device = self._find_or_create_device(
            db,
            identity_hash=identity_hash,
            tier1=tier1,
            tenant_id=request.tenant_id,
            owner_user_id=request.owner_user_id,
        )

        report = db.create_device_report(
            {
                "device_id": device.id,
                "tenant_id": request.tenant_id,
                "report_type": request.report_type,
                "schema_version": parsed.schema_version,
                "platform": parsed.tier1.platform.value,
                "tier1_json": tier1,
                "tier2_json": tier2,
                "tier3_json": tier3,
                "raw_report_json": request.report,
                "identity_hash": identity_hash,
                "value_hash": value_hash,
                "report_hash": report_hash,
                "collector_version": parsed.tier1.collector_version,
            }
        )

        self._audit.log(
            db,
            action="report_submitted",
            resource_type="device_report",
            resource_id=report.id,
            tenant_id=request.tenant_id,
            actor_user_id=request.owner_user_id,
            metadata={"report_type": request.report_type, "platform": report.platform},
        )

        cert_id = None
        cert_code = None
        public_url = None

        if issue_certificate and request.report_type == "initial_certification":
            cert_result = self._certification.issue_from_report(
                db, device=device, report=report, parsed=parsed, validation=validation
            )
            cert_id = cert_result["certificate_id"]
            cert_code = cert_result["certificate_code"]
            public_url = cert_result["public_url"]

        return ReportSubmitResponse(
            report_id=report.id,
            device_id=device.id,
            identity_hash=identity_hash,
            report_hash=report_hash,
            schema_valid=True,
            tier1_complete=validation.tier1_complete,
            tier2_complete=validation.tier2_complete,
            certificate_id=cert_id,
            certificate_code=cert_code,
            public_url=public_url,
        )

    def _find_or_create_device(
        self,
        db: Database,
        identity_hash: str,
        tier1: dict[str, Any],
        tenant_id: str | None,
        owner_user_id: str | None,
    ) -> Device:
        existing = db.find_device_by_identity_hash(identity_hash)
        if existing:
            updates: dict[str, Any] = {}
            if tenant_id and not existing.tenant_id:
                updates["tenant_id"] = tenant_id
            if owner_user_id and not existing.owner_user_id:
                updates["owner_user_id"] = owner_user_id
            if updates:
                db.update_device(existing.id, updates)
                existing = db.get_device(existing.id) or existing
            return existing

        return db.create_device(
            identity_hash=identity_hash,
            tenant_id=tenant_id,
            owner_user_id=owner_user_id,
            tier1=tier1,
        )
