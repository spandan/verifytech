from fastapi import APIRouter, Depends, HTTPException

from app.db.models import Database, get_db
from app.schemas.dto import (
    ScanSessionStartRequest,
    ScanSessionStartResponse,
    ScanSessionSubmitRequest,
    ScanSessionSubmitResponse,
)
from app.services.scan_session_service import ScanSessionService

router = APIRouter(prefix="/api/scan-sessions", tags=["scan-sessions"])
_service = ScanSessionService()


@router.post("/start", response_model=ScanSessionStartResponse)
def start_scan_session(body: ScanSessionStartRequest, db: Database = Depends(get_db)):
    """Issue a short-lived session + nonce for a single agent scan submission."""
    try:
        return _service.start(db, body)
    except ValueError as e:
        raise HTTPException(status_code=422, detail=str(e))


@router.post("/{session_id}/submit", response_model=ScanSessionSubmitResponse)
def submit_scan_session(
    session_id: str,
    body: ScanSessionSubmitRequest,
    db: Database = Depends(get_db),
):
    """Validate session/nonce and issue a server-signed certificate from scan data."""
    try:
        return _service.submit(db, session_id, body)
    except ValueError as e:
        detail = str(e)
        status = 410 if "expired" in detail.lower() else 422
        if "already been used" in detail.lower():
            status = 409
        raise HTTPException(status_code=status, detail=detail)
