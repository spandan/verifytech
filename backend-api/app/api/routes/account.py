from fastapi import APIRouter, Depends, HTTPException

from app.auth.deps import AuthUser, get_current_user
from app.db.models import Database, get_db
from app.schemas.dto import (
    ClaimReportRequest,
    ClaimReportResponse,
    MyLaptopSummary,
    MyLaptopsResponse,
    RenameDeviceRequest,
    ScanLinkTokenResponse,
    SendReportEmailRequest,
)
from app.services.account_service import AccountService

router = APIRouter(prefix="/api/account", tags=["account"])
_service = AccountService()


@router.get("/laptops", response_model=MyLaptopsResponse)
def list_my_laptops(
    db: Database = Depends(get_db),
    user: AuthUser = Depends(get_current_user),
):
    return _service.list_my_laptops(db, user.id)


@router.post("/scan-link-token", response_model=ScanLinkTokenResponse)
def create_scan_link_token(
    db: Database = Depends(get_db),
    user: AuthUser = Depends(get_current_user),
):
    return _service.create_scan_link_token(db, user.id)


@router.patch("/devices/{device_id}", response_model=MyLaptopSummary)
def rename_device(
    device_id: str,
    body: RenameDeviceRequest,
    db: Database = Depends(get_db),
    user: AuthUser = Depends(get_current_user),
):
    try:
        return _service.rename_device(db, user.id, device_id, body)
    except ValueError as exc:
        raise HTTPException(status_code=404, detail=str(exc)) from exc


@router.post("/reports/claim", response_model=ClaimReportResponse)
def claim_report(
    body: ClaimReportRequest,
    db: Database = Depends(get_db),
    user: AuthUser = Depends(get_current_user),
):
    try:
        return _service.claim_report(db, user.id, body)
    except ValueError as exc:
        raise HTTPException(status_code=422, detail=str(exc)) from exc


@router.post("/reports/email")
def email_report(body: SendReportEmailRequest, db: Database = Depends(get_db)):
    try:
        sent = _service.send_report_email(db, body)
    except ValueError as exc:
        raise HTTPException(status_code=404, detail=str(exc)) from exc
    return {"sent": sent}
