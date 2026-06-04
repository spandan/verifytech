"""Audit logging service."""

from __future__ import annotations

from typing import Any, Optional

from app.db.models import AuditLog, Database


class AuditService:
    def log(
        self,
        db: Database,
        action: str,
        resource_type: str,
        resource_id: Optional[str] = None,
        tenant_id: Optional[str] = None,
        actor_user_id: Optional[str] = None,
        metadata: Optional[dict[str, Any]] = None,
    ) -> AuditLog:
        return db.create_audit_log(
            {
                "action": action,
                "resource_type": resource_type,
                "resource_id": resource_id,
                "tenant_id": tenant_id,
                "actor_user_id": actor_user_id,
                "metadata_json": metadata or {},
            }
        )
