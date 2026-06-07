from fastapi import APIRouter, Depends, HTTPException

import logging

from app.auth.deps import AuthUser, get_current_user
from app.db.models import Database, get_db
from app.schemas.dto import (
    ScanPairingCreateRequest,
    ScanPairingCreateResponse,
    ScanPairingExchangeRequest,
    ScanPairingExchangeResponse,
    ScanSessionStartRequest,
    ScanSessionStartResponse,
    ScanSessionSubmitRequest,
    ScanSessionSubmitResponse,
)
from app.services.scan_pairing_service import ScanPairingService
from app.services.scan_session_service import ScanSessionService

router = APIRouter(prefix="/api/scan-sessions", tags=["scan-sessions"])
_service = ScanSessionService()
_pairing = ScanPairingService()
logger = logging.getLogger(__name__)


@router.post("/create-pairing", response_model=ScanPairingCreateResponse)
def create_pairing_session(
    body: ScanPairingCreateRequest,
    user: AuthUser = Depends(get_current_user),
    db: Database = Depends(get_db),
):
    """Create a short-lived pairing code for seamless Windows agent launch."""
    try:
        return _pairing.create_pairing(db, user_id=user.id, body=body)
    except ValueError as e:
        raise HTTPException(status_code=422, detail=str(e))


@router.post("/exchange", response_model=ScanPairingExchangeResponse)
def exchange_pairing_code(body: ScanPairingExchangeRequest, db: Database = Depends(get_db)):
    """Exchange a one-time pairing code for a short-lived upload token (Windows agent)."""
    try:
        return _pairing.exchange(db, body)
    except ValueError as e:
        detail = str(e)
        status = 410 if "expired" in detail.lower() else 422
        if "already been used" in detail.lower():
            status = 409
        raise HTTPException(status_code=status, detail=detail)


@router.post("/start", response_model=ScanSessionStartResponse)
def start_scan_session(body: ScanSessionStartRequest, db: Database = Depends(get_db)):
    """Issue a short-lived session + nonce for a single agent scan submission."""
    try:
        return _service.start(db, body)
    except ValueError as e:
        raise HTTPException(status_code=422, detail=str(e))
    except Exception as e:
        logger.exception("scan session start failed")
        raise HTTPException(
            status_code=500,
            detail="Could not start a scan session. Please try again shortly.",
        ) from e


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
    except Exception as e:
        logger.exception("scan session submit failed session_id=%s", session_id)
        raise HTTPException(
            status_code=500,
            detail="Certificate submission failed on the server. Please try again in a few minutes.",
        ) from e
