"""Verification engine — compare live scan vs certified report."""

from verification_engine.comparator import VerificationComparator
from verification_engine.models import VerificationResult, VerificationOutcome, FieldChange

__all__ = [
    "VerificationComparator",
    "VerificationResult",
    "VerificationOutcome",
    "FieldChange",
]
