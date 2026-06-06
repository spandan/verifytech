"""Map v2.3 functional validation results to public certificate labels."""

from __future__ import annotations

from typing import Any

LABEL_VERIFIED = "Verified"
LABEL_NOT_TESTED = "Detected but Not Tested"
LABEL_FAILED = "Failed"
LABEL_INCONCLUSIVE = "Inconclusive"


def validation_label(test: Any, *, verified_detail: str) -> str:
    if not isinstance(test, dict):
        return LABEL_NOT_TESTED
    result = test.get("result")
    if result == "passed":
        return verified_detail if test.get("tested") else LABEL_VERIFIED
    if result == "failed":
        return LABEL_FAILED
    if result == "inconclusive":
        return LABEL_INCONCLUSIVE
    if test.get("playback_confirmed") and test.get("tested"):
        return LABEL_VERIFIED
    if test.get("present") and not test.get("tested"):
        return LABEL_NOT_TESTED
    return LABEL_NOT_TESTED


def legacy_passed(test: Any) -> bool | None:
    if not isinstance(test, dict):
        return None
    if test.get("result") == "passed":
        return True
    if test.get("tested") and test.get("result") == "failed":
        return False
    return None
