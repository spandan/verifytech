"""Verification service — buyer verification flow."""

from __future__ import annotations

from datetime import datetime, timezone

from certificate_engine.models import CertificateStatus
from schema_engine.validator import SchemaValidator
from verification_engine.comparator import VerificationComparator

from app.db.models import Certificate, Database
from app.schemas.dto import ReportSubmitRequest, VerifySubmitRequest, VerifySubmitResponse, VerificationAttemptResponse
from app.services.audit_service import AuditService
from app.services.report_service import ReportService


class VerificationService:
    def __init__(self):
        self._comparator = VerificationComparator()
        self._validator = SchemaValidator()
        self._report_service = ReportService()
        self._audit = AuditService()

    def lookup_certificate(self, db: Database, certificate_code: str) -> Certificate | None:
        normalized = certificate_code.strip().upper()
        return db.get_certificate_by_code(normalized)

    def submit_verification(self, db: Database, request: VerifySubmitRequest) -> VerifySubmitResponse:
        cert = self.lookup_certificate(db, request.certificate_code)
        if not cert:
            attempt = self._record_attempt(
                db,
                certificate_id=None,
                report_id=None,
                result="CERTIFICATE_NOT_FOUND",
                identity_score=0,
                value_score=0,
                changes={"summary": "Certificate not found"},
            )
            return VerifySubmitResponse(
                attempt_id=attempt.id,
                result="CERTIFICATE_NOT_FOUND",
                identity_match_score=0,
                value_match_score=0,
                summary="No certificate found with this code.",
                certificate_code=request.certificate_code.upper(),
            )

        cert_status = CertificateStatus(cert.status)
        if cert_status == CertificateStatus.REVOKED:
            attempt = self._record_attempt(
                db, cert.id, None, "CERTIFICATE_REVOKED", 0, 0, {"summary": "Revoked"}
            )
            return VerifySubmitResponse(
                attempt_id=attempt.id,
                result="CERTIFICATE_REVOKED",
                identity_match_score=0,
                value_match_score=0,
                summary="This certificate has been revoked.",
                certificate_code=cert.certificate_code,
                device_name=self._device_name(db, cert),
            )

        now = datetime.now(timezone.utc)
        expires = cert.expires_at
        if expires and expires.tzinfo is None:
            expires = expires.replace(tzinfo=timezone.utc)
        if expires and expires < now or cert_status == CertificateStatus.EXPIRED:
            attempt = self._record_attempt(
                db, cert.id, None, "CERTIFICATE_EXPIRED", 0, 0, {"summary": "Expired"}
            )
            return VerifySubmitResponse(
                attempt_id=attempt.id,
                result="CERTIFICATE_EXPIRED",
                identity_match_score=0,
                value_match_score=0,
                summary="This certificate has expired.",
                certificate_code=cert.certificate_code,
                device_name=self._device_name(db, cert),
            )

        report_response = self._report_service.submit(
            db,
            ReportSubmitRequest(
                report=request.report,
                report_type="buyer_verification",
                tenant_id=cert.tenant_id,
            ),
            issue_certificate=False,
        )

        cert = db.get_certificate_by_code(cert.certificate_code, include_report=True)
        if not cert or not cert.initial_report:
            raise RuntimeError("Certified report not found for verification")

        certified_report = self._validator.parse(cert.initial_report.raw_report_json)
        live_report = self._validator.parse(request.report)

        comparison = self._comparator.compare(
            certified_report,
            live_report,
            certificate_status=cert_status,
            expires_at=cert.expires_at,
        )

        attempt = self._record_attempt(
            db,
            certificate_id=cert.id,
            report_id=report_response.report_id,
            result=comparison.outcome.value,
            identity_score=comparison.identity_match_score,
            value_score=comparison.value_match_score,
            changes={
                "summary": comparison.summary,
                "changes": [c.model_dump() for c in comparison.changes],
                "value_estimate_invalidated": comparison.value_estimate_invalidated,
            },
            tenant_id=cert.tenant_id,
        )

        self._audit.log(
            db,
            action="verification_completed",
            resource_type="verification_attempt",
            resource_id=attempt.id,
            tenant_id=cert.tenant_id,
            metadata={"result": comparison.outcome.value, "certificate_code": cert.certificate_code},
        )

        return VerifySubmitResponse(
            attempt_id=attempt.id,
            result=comparison.outcome.value,
            identity_match_score=comparison.identity_match_score,
            value_match_score=comparison.value_match_score,
            summary=comparison.summary,
            changes=[c.model_dump() for c in comparison.changes],
            value_estimate_invalidated=comparison.value_estimate_invalidated,
            certificate_code=cert.certificate_code,
            device_name=self._device_name(db, cert),
        )

    def get_attempt(self, db: Database, attempt_id: str) -> VerificationAttemptResponse | None:
        attempt = db.get_verification_attempt(attempt_id)
        if not attempt:
            return None

        cert_code = None
        device_name = None
        if attempt.certificate_id:
            cert = db.get_certificate_by_id(attempt.certificate_id)
            if cert:
                cert_code = cert.certificate_code
                device_name = self._device_name(db, cert)

        changes_data = attempt.changes_json or {}
        return VerificationAttemptResponse(
            attempt_id=attempt.id,
            result=attempt.result,
            identity_match_score=float(attempt.identity_match_score or 0),
            value_match_score=float(attempt.value_match_score or 0),
            summary=changes_data.get("summary", ""),
            changes=changes_data.get("changes", []),
            value_estimate_invalidated=changes_data.get("value_estimate_invalidated", False),
            certificate_code=cert_code,
            device_name=device_name,
            created_at=attempt.created_at,
        )

    def _record_attempt(
        self,
        db: Database,
        certificate_id: str | None,
        report_id: str | None,
        result: str,
        identity_score: float,
        value_score: float,
        changes: dict,
        tenant_id: str | None = None,
    ):
        return db.create_verification_attempt(
            {
                "certificate_id": certificate_id,
                "verification_report_id": report_id,
                "tenant_id": tenant_id,
                "result": result,
                "identity_match_score": identity_score,
                "value_match_score": value_score,
                "changes_json": changes,
            }
        )

    def _device_name(self, db: Database, cert: Certificate) -> str | None:
        device = db.get_device(cert.device_id)
        if device:
            return f"{device.manufacturer} {device.model}"
        payload = cert.public_payload_json or {}
        return payload.get("device_name")
