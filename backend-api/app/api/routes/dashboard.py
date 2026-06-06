from fastapi import APIRouter, Depends

from app.auth.deps import AuthUser, get_current_user
from app.db.models import Database, get_db
from app.schemas.dto import DashboardResponse, MyLaptopsResponse
from app.services.account_service import AccountService
from app.services.dashboard_service import DashboardService

router = APIRouter(prefix="/api/dashboard", tags=["dashboard"])
_service = DashboardService()
_account = AccountService()


@router.get("", response_model=DashboardResponse)
def get_dashboard(
    db: Database = Depends(get_db),
    user: AuthUser = Depends(get_current_user),
):
    return _service.get_user_dashboard(db, user.id)


@router.get("/laptops", response_model=MyLaptopsResponse)
def get_my_laptops(
    db: Database = Depends(get_db),
    user: AuthUser = Depends(get_current_user),
):
    return _account.list_my_laptops(db, user.id)
