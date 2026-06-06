from fastapi import APIRouter, Depends, HTTPException

from app.auth.deps import get_current_user, get_scan_upload_claims
from app.auth.scan_upload_jwt import ScanUploadClaims
from app.db.models import Database, get_db
from app.schemas.dto import (
    ScanPairingCreateRequest,
    ScanPairingCreateResponse,
    ScanPairingExchangeRequest,
    ScanPairingExchangeResponse,
    ScanUploadRequest,
    ScanSessionSubmitResponse,
)
from app.services.scan_pairing_service import ScanPairingService
from app.services.scan_session_service import ScanSessionService

router = APIRouter(prefix="/api/scans", tags=["scans"])
_service = ScanSessionService()


@router.post("/upload", response_model=ScanSessionSubmitResponse)
def upload_scan(
    body: ScanUploadRequest,
    claims: ScanUploadClaims = Depends(get_scan_upload_claims),
    db: Database = Depends(get_db),
):
    """Accept a paired Windows agent scan using a short-lived upload JWT."""
    try:
        return _service.upload(db, claims, body)
    except ValueError as e:
        detail = str(e)
        status = 410 if "expired" in detail.lower() else 422
        if "already been used" in detail.lower() or "not ready" in detail.lower():
            status = 409
        raise HTTPException(status_code=status, detail=detail)
