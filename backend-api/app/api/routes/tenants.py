from fastapi import APIRouter, Depends, Header, HTTPException

from app.db.models import Database, get_db
from app.schemas.dto import TenantResponse
from app.services.tenant_service import TenantService

router = APIRouter(prefix="/api/tenants", tags=["tenants"])
_service = TenantService()


@router.get("", response_model=list[TenantResponse])
def list_user_tenants(
    db: Database = Depends(get_db),
    x_user_id: str | None = Header(default=None),
):
    if not x_user_id:
        raise HTTPException(status_code=401, detail="Authentication required")
    return _service.get_user_tenants(db, x_user_id)


@router.get("/{tenant_id}", response_model=TenantResponse)
def get_tenant(
    tenant_id: str,
    db: Database = Depends(get_db),
    x_user_id: str | None = Header(default=None),
):
    tenant = _service.get_tenant(db, tenant_id)
    if not tenant:
        raise HTTPException(status_code=404, detail="Tenant not found")
    role = None
    if x_user_id:
        tenants = _service.get_user_tenants(db, x_user_id)
        match = next((t for t in tenants if t.id == tenant_id), None)
        role = match.role if match else None
    return TenantResponse(id=tenant.id, name=tenant.name, slug=tenant.slug, role=role)
