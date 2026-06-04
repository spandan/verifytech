"""Intake module — pre-download information collection."""

from __future__ import annotations

from app.db.models import Database
from app.schemas.dto import IntakeCreateRequest, IntakeResponse


class IntakeService:
    def create(self, db: Database, request: IntakeCreateRequest, user_id: str | None = None) -> IntakeResponse:
        record = db.create_intake(
            {
                "user_id": user_id,
                "tenant_id": request.tenant_id,
                "device_category": request.device_category,
                "approximate_purchase_year": request.approximate_purchase_year,
                "zip_code_or_region": request.zip_code_or_region,
                "charger_included": request.charger_included,
            }
        )
        return IntakeResponse(
            id=record.id,
            device_category=record.device_category,
            approximate_purchase_year=record.approximate_purchase_year,
            zip_code_or_region=record.zip_code_or_region,
            charger_included=record.charger_included,
            created_at=record.created_at,
        )

    def get(self, db: Database, intake_id: str) -> IntakeResponse | None:
        record = db.get_intake(intake_id)
        if not record:
            return None
        return IntakeResponse(
            id=record.id,
            device_category=record.device_category,
            approximate_purchase_year=record.approximate_purchase_year,
            zip_code_or_region=record.zip_code_or_region,
            charger_included=record.charger_included,
            created_at=record.created_at,
        )
