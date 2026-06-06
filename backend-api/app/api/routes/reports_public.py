from fastapi import APIRouter, Depends, HTTPException

from app.db.models import Database, get_db
from app.schemas.dto import CertificatePublicResponse, PublicScanReportResponse
from app.services.certification_service import CertificationService
from app.services.scan_report_service import format_verification_code

router = APIRouter(prefix="/api/reports", tags=["reports-public"])
_service = CertificationService()


def _to_public_response(cert) -> CertificatePublicResponse:
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


@router.get("/public/{token_or_code}", response_model=PublicScanReportResponse)
def get_public_report(token_or_code: str, db: Database = Depends(get_db)):
    report = db.get_scan_report_by_public_token(token_or_code)
    if not report:
        report = db.get_scan_report_by_verification_code(format_verification_code(token_or_code))

    if report:
        cert_response = None
        cert = _service.get_public_certificate(db, report.verification_code)
        if cert:
            cert_response = _to_public_response(cert)
        public_url = cert_response.public_url if cert_response else f"/c/{report.verification_code}"
        return PublicScanReportResponse(
            verification_code=report.verification_code,
            public_report_url=public_url,
            report_summary=report.report_summary,
            certificate=cert_response,
        )

    code = format_verification_code(token_or_code)
    cert = _service.get_public_certificate(db, code)
    if not cert:
        raise HTTPException(status_code=404, detail="Report not found")
    cert_response = _to_public_response(cert)
    return PublicScanReportResponse(
        verification_code=cert.certificate_code,
        public_report_url=cert_response.public_url,
        report_summary={
            "device_name": cert_response.device_name,
            "manufacturer": cert_response.manufacturer,
            "model": cert_response.model,
            "verification_status": cert_response.status,
        },
        certificate=cert_response,
    )
