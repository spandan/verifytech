"""Tenant management service."""

from __future__ import annotations

from app.db.models import Database, Tenant
from app.schemas.dto import TenantResponse


class TenantService:
    def get_user_tenants(self, db: Database, user_id: str) -> list[TenantResponse]:
        rows = db.get_user_tenants(user_id)
        return [TenantResponse(id=t.id, name=t.name, slug=t.slug, role=tu.role) for t, tu in rows]

    def get_tenant(self, db: Database, tenant_id: str) -> Tenant | None:
        return db.get_tenant(tenant_id)

    def resolve_tenant_id(
        self,
        db: Database,
        tenant_id: str | None,
        user_id: str | None,
    ) -> str | None:
        """Validate user has access to tenant if tenant_id provided."""
        if not tenant_id:
            return None
        if not user_id:
            return tenant_id  # agent/system context
        membership = db.get_tenant_membership(tenant_id, user_id)
        if not membership:
            raise PermissionError("User is not a member of this tenant")
        return tenant_id
