from fastapi import APIRouter, Depends, HTTPException, Query

from app.auth.deps import AuthUser, get_current_user
from app.db.models import Database, get_db
from app.schemas.dto import (
    AgentPairingClaimRequest,
    AgentPairingClaimResponse,
    AgentPairingCreateRequest,
    AgentPairingCreateResponse,
    AgentPairingStatusResponse,
)
from app.services.agent_pairing_service import AgentPairingService

router = APIRouter(prefix="/api/agent/pairing", tags=["agent-pairing"])
_service = AgentPairingService()


@router.post("/create", response_model=AgentPairingCreateResponse)
def create_agent_pairing(body: AgentPairingCreateRequest, db: Database = Depends(get_db)):
    try:
        return _service.create(db, body)
    except ValueError as e:
        raise HTTPException(status_code=422, detail=str(e))


@router.post("/claim", response_model=AgentPairingClaimResponse)
def claim_agent_pairing(
    body: AgentPairingClaimRequest,
    user: AuthUser = Depends(get_current_user),
    db: Database = Depends(get_db),
):
    try:
        return _service.claim(db, user_id=user.id, body=body)
    except ValueError as e:
        detail = str(e)
        status = 410 if "expired" in detail.lower() else 422
        if "already been used" in detail.lower():
            status = 409
        raise HTTPException(status_code=status, detail=detail)


@router.get("/status", response_model=AgentPairingStatusResponse)
def get_agent_pairing_status(
    pairing_code: str = Query(min_length=6, max_length=6),
    device_nonce: str = Query(min_length=8),
    db: Database = Depends(get_db),
):
    try:
        return _service.status(db, pairing_code=pairing_code, device_nonce=device_nonce)
    except ValueError as e:
        raise HTTPException(status_code=422, detail=str(e))
