from fastapi import APIRouter, Depends

from app.auth.deps import AuthUser, get_current_user
from app.db.models import Database, get_db
from app.schemas.dto import AuthProfileResponse
from app.services.tenant_service import TenantService

router = APIRouter(prefix="/api/auth-profile", tags=["auth-profile"])
_tenant_service = TenantService()


@router.get("", response_model=AuthProfileResponse)
def get_auth_profile(
    db: Database = Depends(get_db),
    user: AuthUser = Depends(get_current_user),
):
    profile = db.upsert_profile(user.id, email=user.email)
    tenants = _tenant_service.get_user_tenants(db, user.id)
    return AuthProfileResponse(
        id=profile.id,
        email=profile.email or user.email,
        full_name=profile.full_name,
        tenants=tenants,
    )
