from datetime import datetime

from fastapi import APIRouter, Depends, HTTPException

from app.db.models import Database, get_db
from app.schemas.dto import CertificateLookupResponse, CertificatePublicResponse
from app.services.certification_service import CertificationService

router = APIRouter(prefix="/api/certificates", tags=["certificates"])
_service = CertificationService()


@router.get("/lookup/{certificate_code}", response_model=CertificateLookupResponse)
def lookup_certificate(certificate_code: str, db: Database = Depends(get_db)):
    cert = _service.lookup(db, certificate_code)
    if not cert:
        return CertificateLookupResponse(
            exists=False,
            certificate_code=certificate_code.upper(),
        )
    payload = cert.public_payload_json or {}
    return CertificateLookupResponse(
        exists=True,
        certificate_code=cert.certificate_code,
        status=cert.status,
        device_name=payload.get("device_name"),
        expires_at=cert.expires_at,
    )


@router.get("/{certificate_code}", response_model=CertificatePublicResponse)
def get_public_certificate(certificate_code: str, db: Database = Depends(get_db)):
    cert = _service.get_public_certificate(db, certificate_code)
    if not cert:
        raise HTTPException(status_code=404, detail="Certificate not found")

    payload = cert.public_payload_json or {}
    return CertificatePublicResponse(
        certificate_code=cert.certificate_code,
        device_name=payload.get("device_name", "Unknown Device"),
        manufacturer=payload.get("manufacturer", ""),
        model=payload.get("model", ""),
        device_type=payload.get("device_type", ""),
        platform=payload.get("platform", ""),
        certificate_level=cert.certificate_level,
        status=cert.status,
        condition_grade=cert.condition_grade,
        certification_date=cert.issued_at,
        expires_at=cert.expires_at,
        battery_health_percent=payload.get("battery_health_percent"),
        storage_health_percent=payload.get("storage_health_percent"),
        core_tests_passed=payload.get("core_tests_passed", []),
        core_tests_total=payload.get("core_tests_total", 7),
        verification_url=payload.get("verification_url", ""),
        qr_code_payload=cert.qr_code_payload or "",
        public_url=cert.public_url or "",
        inspection_report=payload.get("inspection_report"),
        agent_provenance=payload.get("agent_provenance"),
    )
