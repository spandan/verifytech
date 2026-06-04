from fastapi import APIRouter, Depends, Header, HTTPException

from app.db.models import Database, get_db
from app.schemas.dto import AuthProfileResponse
from app.services.tenant_service import TenantService

router = APIRouter(prefix="/api/auth-profile", tags=["auth-profile"])
_tenant_service = TenantService()


@router.get("", response_model=AuthProfileResponse)
def get_auth_profile(
    db: Database = Depends(get_db),
    x_user_id: str | None = Header(default=None),
    x_user_email: str | None = Header(default=None),
):
    if not x_user_id:
        raise HTTPException(status_code=401, detail="Authentication required")

    profile = db.upsert_profile(x_user_id, email=x_user_email)

    tenants = _tenant_service.get_user_tenants(db, x_user_id)
    return AuthProfileResponse(
        id=profile.id,
        email=profile.email or x_user_email,
        full_name=profile.full_name,
        tenants=tenants,
    )
