from fastapi import APIRouter, Depends, HTTPException

from app.auth.deps import AuthUser, get_current_user
from app.db.models import Database, get_db
from app.schemas.dto import (
    CertificationSessionBeginScanRequest,
    CertificationSessionBeginScanResponse,
    CertificationSessionCreateRequest,
    CertificationSessionCreateResponse,
    CertificationSessionValidateRequest,
    CertificationSessionValidateResponse,
)
from app.services.certification_session_service import CertificationSessionService

router = APIRouter(prefix="/api/certification-sessions", tags=["certification-sessions"])
_service = CertificationSessionService()


@router.post("", response_model=CertificationSessionCreateResponse)
def create_certification_session(
    body: CertificationSessionCreateRequest,
    user: AuthUser = Depends(get_current_user),
    db: Database = Depends(get_db),
):
    try:
        return _service.create(db, user_id=user.id, body=body)
    except ValueError as e:
        raise HTTPException(status_code=422, detail=str(e))


@router.post("/validate", response_model=CertificationSessionValidateResponse)
def validate_certification_session(body: CertificationSessionValidateRequest, db: Database = Depends(get_db)):
    try:
        return _service.validate(db, body)
    except ValueError as e:
        detail = str(e)
        status = 410 if "expired" in detail.lower() else 422
        raise HTTPException(status_code=status, detail=detail)


@router.post("/begin-scan", response_model=CertificationSessionBeginScanResponse)
def begin_certification_scan(body: CertificationSessionBeginScanRequest, db: Database = Depends(get_db)):
    try:
        return _service.begin_scan(db, body)
    except ValueError as e:
        detail = str(e)
        status = 410 if "expired" in detail.lower() else 422
        if "already been used" in detail.lower() or "no longer valid" in detail.lower():
            status = 409
        raise HTTPException(status_code=status, detail=detail)
