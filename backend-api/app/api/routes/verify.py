from fastapi import APIRouter, Depends, HTTPException

from app.db.models import Database, get_db
from app.schemas.dto import (
    VerifyLookupRequest,
    VerifySubmitRequest,
    VerifySubmitResponse,
    VerificationAttemptResponse,
)
from app.services.verification_service import VerificationService

router = APIRouter(prefix="/api/verify", tags=["verify"])
_service = VerificationService()


@router.post("/lookup")
def verify_lookup(body: VerifyLookupRequest, db: Database = Depends(get_db)):
    cert = _service.lookup_certificate(db, body.certificate_code)
    if not cert:
        return {"exists": False, "certificate_code": body.certificate_code.upper()}
    payload = cert.public_payload_json or {}
    return {
        "exists": True,
        "certificate_code": cert.certificate_code,
        "status": cert.status,
        "device_name": payload.get("device_name"),
        "expires_at": cert.expires_at.isoformat(),
    }


@router.post("/submit", response_model=VerifySubmitResponse)
def verify_submit(body: VerifySubmitRequest, db: Database = Depends(get_db)):
    return _service.submit_verification(db, body)


@router.get("/attempts/{attempt_id}", response_model=VerificationAttemptResponse)
def get_verification_attempt(attempt_id: str, db: Database = Depends(get_db)):
    result = _service.get_attempt(db, attempt_id)
    if not result:
        raise HTTPException(status_code=404, detail="Verification attempt not found")
    return result
