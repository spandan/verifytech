from fastapi import APIRouter, Depends, Header, HTTPException, Request

from app.db.models import Database, get_db
from app.schemas.dto import IntakeCreateRequest, IntakeResponse
from app.services.intake_service import IntakeService

router = APIRouter(prefix="/api/intake", tags=["intake"])
_service = IntakeService()


@router.post("", response_model=IntakeResponse)
def create_intake(
    body: IntakeCreateRequest,
    db: Database = Depends(get_db),
    x_user_id: str | None = Header(default=None),
):
    return _service.create(db, body, user_id=x_user_id)


@router.get("/{intake_id}", response_model=IntakeResponse)
def get_intake(intake_id: str, db: Database = Depends(get_db)):
    result = _service.get(db, intake_id)
    if not result:
        raise HTTPException(status_code=404, detail="Intake not found")
    return result
