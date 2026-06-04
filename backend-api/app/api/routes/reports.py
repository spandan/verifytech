from fastapi import APIRouter, Depends, HTTPException

from app.config import settings
from app.db.models import Database, get_db
from app.schemas.dto import (
    AgentCertifyRequest,
    AgentCertifyResponse,
    AgentVerifyRequest,
    AgentVerifyResponse,
    ReportSubmitRequest,
    ReportSubmitResponse,
    VerifySubmitRequest,
)
from app.services.agent_report_adapter import agent_report_to_internal
from app.services.report_service import ReportService
from app.services.verification_service import VerificationService

router = APIRouter(prefix="/api/reports", tags=["reports"])
_report_service = ReportService()
_verify_service = VerificationService()


@router.post("", response_model=ReportSubmitResponse)
def submit_report(body: ReportSubmitRequest, db: Database = Depends(get_db)):
    try:
        return _report_service.submit(db, body)
    except ValueError as e:
        raise HTTPException(status_code=422, detail=str(e))


@router.post("/certify", response_model=AgentCertifyResponse)
def certify_from_agent(body: AgentCertifyRequest, db: Database = Depends(get_db)):
    """Accept Windows agent canonical report and issue a certificate."""
    raw = body.model_dump(mode="json")
    internal_report = agent_report_to_internal(raw)
    ctx = body.collection_context or {}
    intake_id = ctx.get("intake_id")

    try:
        result = _report_service.submit(
            db,
            ReportSubmitRequest(
                report=internal_report,
                report_type="initial_certification",
                intake_id=intake_id,
            ),
        )
    except ValueError as e:
        raise HTTPException(status_code=422, detail=str(e))

    if not result.certificate_code:
        raise HTTPException(status_code=422, detail="Certificate could not be issued. Check Tier 1 identity fields.")

    cert = db.get_certificate_by_code(result.certificate_code)
    level = cert.certificate_level if cert else ("condition_certified" if result.tier2_complete else "identity_verified")

    return AgentCertifyResponse(
        certificate_code=result.certificate_code,
        certificate_url=result.public_url or f"{settings.public_base_url}/c/{result.certificate_code}",
        certificate_level=level,
        status=cert.status if cert else "active",
        message="Device certified successfully.",
    )


@router.post("/verify", response_model=AgentVerifyResponse)
def verify_from_agent(body: AgentVerifyRequest, db: Database = Depends(get_db)):
    """Accept Windows agent verification scan and compare against certificate."""
    ctx = body.collection_context or {}
    certificate_code = ctx.get("certificate_code")
    if not certificate_code:
        raise HTTPException(status_code=422, detail="collection_context.certificate_code is required for verify mode")

    raw = body.model_dump(mode="json")
    internal_report = agent_report_to_internal(raw)

    verify_result = _verify_service.submit_verification(
        db,
        VerifySubmitRequest(
            certificate_code=certificate_code,
            report=internal_report,
        ),
    )

    verification_url = None
    if verify_result.attempt_id:
        verification_url = f"{settings.public_base_url}/verification-result/{verify_result.attempt_id}"

    return AgentVerifyResponse(
        result=verify_result.result,
        message=verify_result.summary,
        changes=verify_result.changes,
        attempt_id=verify_result.attempt_id,
        verification_url=verification_url,
        identity_match_score=verify_result.identity_match_score,
        value_match_score=verify_result.value_match_score,
    )
