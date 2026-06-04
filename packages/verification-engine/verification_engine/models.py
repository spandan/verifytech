"""Verification domain models."""

from __future__ import annotations

from enum import Enum
from typing import Any, Optional

from pydantic import BaseModel, Field


class VerificationOutcome(str, Enum):
    CERTIFIED_MATCH = "CERTIFIED_MATCH"
    CERTIFIED_WITH_CHANGES = "CERTIFIED_WITH_CHANGES"
    DEVICE_MISMATCH = "DEVICE_MISMATCH"
    CERTIFICATE_NOT_FOUND = "CERTIFICATE_NOT_FOUND"
    CERTIFICATE_EXPIRED = "CERTIFICATE_EXPIRED"
    CERTIFICATE_REVOKED = "CERTIFICATE_REVOKED"


class FieldChange(BaseModel):
    field: str
    tier: int
    certified_value: Any
    live_value: Any
    significance: str  # identity | value | informational


class VerificationResult(BaseModel):
    outcome: VerificationOutcome
    identity_match_score: float = 0.0
    value_match_score: float = 0.0
    changes: list[FieldChange] = Field(default_factory=list)
    summary: str = ""
    value_estimate_invalidated: bool = False
