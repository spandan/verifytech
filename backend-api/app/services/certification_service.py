"""Certification service — orchestrates certificate engine with persistence."""

from __future__ import annotations

from certificate_engine.generator import CertificateGenerator
from schema_engine.models import DeviceReport as ParsedReport
from schema_engine.models import ValidationResult
from schema_engine.validator import SchemaValidator

from app.config import settings
from app.db.models import Certificate, Database, Device, DeviceReport
from app.services.audit_service import AuditService


class CertificationService:
    def __init__(self):
        self._generator = CertificateGenerator(
            public_base_url=settings.public_base_url,
            certificate_ttl_days=settings.certificate_ttl_days,
        )
        self._validator = SchemaValidator()
        self._audit = AuditService()

    def issue_from_report(
        self,
        db: Database,
        device: Device,
        report: DeviceReport,
        parsed: ParsedReport | None = None,
        validation: ValidationResult | None = None,
    ) -> dict:
        if parsed is None:
            parsed = self._validator.parse(report.raw_report_json)
        if validation is None:
            validation = self._validator.validate(report.raw_report_json)

        generated = self._generator.generate(parsed, validation)

        cert = db.create_certificate(
            {
                "certificate_code": generated.certificate_code,
                "device_id": device.id,
                "tenant_id": report.tenant_id,
                "initial_report_id": report.id,
                "certificate_level": generated.certificate_level.value,
                "status": generated.status.value,
                "condition_grade": generated.condition_grade,
                "value_score": generated.value_score,
                "issued_at": generated.issued_at.isoformat(),
                "expires_at": generated.expires_at.isoformat(),
                "public_url": generated.public_url,
                "qr_code_payload": generated.qr_code_payload,
                "public_payload_json": generated.public_payload_json,
            }
        )

        db.create_certificate_event(
            {
                "certificate_id": cert.id,
                "tenant_id": report.tenant_id,
                "event_type": "issued",
                "event_data": {"level": generated.certificate_level.value},
            }
        )

        self._audit.log(
            db,
            action="certificate_created",
            resource_type="certificate",
            resource_id=cert.id,
            tenant_id=report.tenant_id,
            metadata={"certificate_code": cert.certificate_code},
        )

        return {
            "certificate_id": cert.id,
            "certificate_code": cert.certificate_code,
            "public_url": cert.public_url,
        }

    def get_public_certificate(self, db: Database, certificate_code: str) -> Certificate | None:
        normalized = certificate_code.strip().upper()
        return db.get_certificate_by_code(normalized)

    def lookup(self, db: Database, certificate_code: str) -> Certificate | None:
        return self.get_public_certificate(db, certificate_code)
