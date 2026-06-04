from fastapi import APIRouter, Depends, Header, HTTPException

from app.db.models import Database, get_db
from app.schemas.dto import DashboardResponse
from app.services.dashboard_service import DashboardService

router = APIRouter(prefix="/api/dashboard", tags=["dashboard"])
_service = DashboardService()


@router.get("", response_model=DashboardResponse)
def get_dashboard(
    db: Database = Depends(get_db),
    x_user_id: str | None = Header(default=None),
):
    if not x_user_id:
        raise HTTPException(status_code=401, detail="Authentication required")
    return _service.get_user_dashboard(db, x_user_id)
