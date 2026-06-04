"""Dashboard service for authenticated users."""

from __future__ import annotations

from app.db.models import Database
from app.schemas.dto import DashboardCertificateSummary, DashboardResponse


class DashboardService:
    def get_user_dashboard(self, db: Database, user_id: str) -> DashboardResponse:
        devices = db.get_devices_by_owner(user_id)
        device_ids = [d.id for d in devices]

        certificates: list[DashboardCertificateSummary] = []
        if device_ids:
            certs = db.get_certificates_for_devices(device_ids)
            device_map = {d.id: d for d in devices}
            for cert in certs:
                device = device_map.get(cert.device_id)
                name = f"{device.manufacturer} {device.model}" if device else "Unknown Device"
                certificates.append(
                    DashboardCertificateSummary(
                        id=cert.id,
                        certificate_code=cert.certificate_code,
                        device_name=name,
                        certificate_level=cert.certificate_level,
                        status=cert.status,
                        condition_grade=cert.condition_grade,
                        issued_at=cert.issued_at,
                        expires_at=cert.expires_at,
                        public_url=cert.public_url or "",
                    )
                )

        cert_ids = [c.id for c in certificates]
        verification_count = db.count_verifications_for_certificate_ids(cert_ids)

        return DashboardResponse(
            user_id=user_id,
            certificates=certificates,
            verification_count=verification_count,
        )
