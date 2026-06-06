"""Supabase-backed data access for the backend API."""

from __future__ import annotations

from datetime import datetime, timezone
from typing import Any
from uuid import uuid4

from supabase import Client

from app.db.records import (
    AgentVersion,
    AuditLog,
    Certificate,
    CertificateEvent,
    Device,
    DeviceReport,
    IntakeResponse,
    Profile,
    Tenant,
    TenantUser,
    ScanSession,
    ScanReport,
    VerificationAttempt,
)
from app.db.supabase_client import get_supabase_admin


class Database:
    def __init__(self, client: Client | None = None):
        self.client = client or get_supabase_admin()

    def _insert(self, table: str, payload: dict[str, Any]) -> dict[str, Any]:
        result = self.client.table(table).insert(payload).execute()
        if not result.data:
            raise RuntimeError(f"Insert into {table} returned no rows")
        return result.data[0]

    def _first(self, table: str, **filters: Any) -> dict[str, Any] | None:
        query = self.client.table(table).select("*")
        for key, value in filters.items():
            query = query.eq(key, value)
        result = query.limit(1).execute()
        return result.data[0] if result.data else None

    # ── Profiles ─────────────────────────────────────────────────────────────

    def get_profile(self, user_id: str) -> Profile | None:
        row = self._first("profiles", id=user_id)
        return Profile.from_row(row) if row else None

    def upsert_profile(self, user_id: str, email: str | None = None) -> Profile:
        existing = self.get_profile(user_id)
        if existing:
            return existing
        row = self._insert("profiles", {"id": user_id, "email": email})
        return Profile.from_row(row)

    # ── Tenants ──────────────────────────────────────────────────────────────

    def get_user_tenants(self, user_id: str) -> list[tuple[Tenant, TenantUser]]:
        memberships = (
            self.client.table("tenant_users")
            .select("*, tenants(*)")
            .eq("user_id", user_id)
            .execute()
            .data
            or []
        )
        rows: list[tuple[Tenant, TenantUser]] = []
        for row in memberships:
            tenant_data = row.get("tenants")
            if not tenant_data or not tenant_data.get("is_active", True):
                continue
            rows.append((Tenant.from_row(tenant_data), TenantUser.from_row(row)))
        return rows

    def get_tenant(self, tenant_id: str) -> Tenant | None:
        row = self._first("tenants", id=tenant_id, is_active=True)
        return Tenant.from_row(row) if row else None

    def get_tenant_membership(self, tenant_id: str, user_id: str) -> TenantUser | None:
        row = self._first("tenant_users", tenant_id=tenant_id, user_id=user_id)
        return TenantUser.from_row(row) if row else None

    # ── Devices ──────────────────────────────────────────────────────────────

    def find_device_by_identity_hash(self, identity_hash: str) -> Device | None:
        row = self._first("devices", identity_hash=identity_hash)
        return Device.from_row(row) if row else None

    def create_device(
        self,
        identity_hash: str,
        tenant_id: str | None,
        owner_user_id: str | None,
        tier1: dict[str, Any],
    ) -> Device:
        row = self._insert(
            "devices",
            {
                "id": str(uuid4()),
                "tenant_id": tenant_id,
                "owner_user_id": owner_user_id,
                "identity_hash": identity_hash,
                "manufacturer": tier1.get("manufacturer"),
                "model": tier1.get("model"),
                "device_type": tier1.get("device_type"),
                "platform": tier1.get("platform"),
            },
        )
        return Device.from_row(row)

    def update_device(self, device_id: str, updates: dict[str, Any]) -> None:
        self.client.table("devices").update(updates).eq("id", device_id).execute()

    def get_device(self, device_id: str) -> Device | None:
        row = self._first("devices", id=device_id)
        return Device.from_row(row) if row else None

    def get_devices_by_owner(self, user_id: str) -> list[Device]:
        result = self.client.table("devices").select("*").eq("owner_user_id", user_id).execute()
        return [Device.from_row(row) for row in result.data or []]

    # ── Reports ──────────────────────────────────────────────────────────────

    def create_device_report(self, payload: dict[str, Any]) -> DeviceReport:
        payload = {"id": str(uuid4()), **payload}
        row = self._insert("device_reports", payload)
        return DeviceReport.from_row(row)

    def get_device_report(self, report_id: str) -> DeviceReport | None:
        row = self._first("device_reports", id=report_id)
        return DeviceReport.from_row(row) if row else None

    # ── Certificates ─────────────────────────────────────────────────────────

    def create_certificate(self, payload: dict[str, Any]) -> Certificate:
        payload = {"id": str(uuid4()), **payload}
        row = self._insert("certificates", payload)
        return Certificate.from_row(row)

    def get_certificate_by_code(
        self,
        certificate_code: str,
        include_report: bool = False,
    ) -> Certificate | None:
        row = self._first("certificates", certificate_code=certificate_code)
        if not row:
            return None
        cert = Certificate.from_row(row)
        if include_report and cert.initial_report_id:
            cert.initial_report = self.get_device_report(cert.initial_report_id)
        return cert

    def get_certificate_by_id(self, certificate_id: str) -> Certificate | None:
        row = self._first("certificates", id=certificate_id)
        return Certificate.from_row(row) if row else None

    def get_certificates_for_devices(self, device_ids: list[str]) -> list[Certificate]:
        if not device_ids:
            return []
        result = (
            self.client.table("certificates")
            .select("*")
            .in_("device_id", device_ids)
            .order("issued_at", desc=True)
            .execute()
        )
        return [Certificate.from_row(row) for row in result.data or []]

    # ── Certificate events ───────────────────────────────────────────────────

    def create_certificate_event(self, payload: dict[str, Any]) -> CertificateEvent:
        payload = {"id": str(uuid4()), **payload}
        row = self._insert("certificate_events", payload)
        return CertificateEvent.from_row(row)

    # ── Verification ─────────────────────────────────────────────────────────

    def create_verification_attempt(self, payload: dict[str, Any]) -> VerificationAttempt:
        payload = {"id": str(uuid4()), **payload}
        row = self._insert("verification_attempts", payload)
        return VerificationAttempt.from_row(row)

    def get_verification_attempt(self, attempt_id: str) -> VerificationAttempt | None:
        row = self._first("verification_attempts", id=attempt_id)
        return VerificationAttempt.from_row(row) if row else None

    def count_verifications_for_certificate_ids(self, certificate_ids: list[str]) -> int:
        if not certificate_ids:
            return 0
        result = (
            self.client.table("verification_attempts")
            .select("id", count="exact")
            .in_("certificate_id", certificate_ids)
            .execute()
        )
        return result.count or 0

    # ── Agents ───────────────────────────────────────────────────────────────

    def get_active_agent(self, platform: str) -> AgentVersion | None:
        result = (
            self.client.table("agent_versions")
            .select("*")
            .eq("platform", platform)
            .eq("is_active", True)
            .order("created_at", desc=True)
            .limit(1)
            .execute()
        )
        if not result.data:
            return None
        return AgentVersion.from_row(result.data[0])

    def list_active_agents(self) -> list[AgentVersion]:
        result = (
            self.client.table("agent_versions")
            .select("*")
            .eq("is_active", True)
            .order("platform")
            .order("created_at", desc=True)
            .execute()
        )
        return [AgentVersion.from_row(row) for row in result.data or []]

    def seed_agent_version_if_missing(self) -> None:
        if self.get_active_agent("windows"):
            return
        self._insert(
            "agent_versions",
            {
                "id": str(uuid4()),
                "platform": "windows",
                "version": "0.1.0",
                "download_url": "windows/0.1.0/DeviceCertAgent.exe",
                "checksum": "sha256:placeholder-checksum-replace-on-release",
                "is_active": True,
                "release_notes": "Initial POC Windows agent placeholder",
            },
        )

    # ── Intake ───────────────────────────────────────────────────────────────

    def create_intake(self, payload: dict[str, Any]) -> IntakeResponse:
        payload = {"id": str(uuid4()), **payload}
        row = self._insert("intake_responses", payload)
        return IntakeResponse.from_row(row)

    def get_intake(self, intake_id: str) -> IntakeResponse | None:
        row = self._first("intake_responses", id=intake_id)
        return IntakeResponse.from_row(row) if row else None

    # ── Scan sessions ────────────────────────────────────────────────────────

    def create_scan_session(self, payload: dict[str, Any]) -> ScanSession:
        payload = {"id": str(uuid4()), **payload}
        row = self._insert("scan_sessions", payload)
        return ScanSession.from_row(row)

    def get_scan_session(self, session_id: str) -> ScanSession | None:
        row = self._first("scan_sessions", session_id=session_id)
        return ScanSession.from_row(row) if row else None

    def update_scan_session(self, session_id: str, updates: dict[str, Any]) -> None:
        self.client.table("scan_sessions").update(updates).eq("session_id", session_id).execute()

    def create_scan_pairing_session(self, payload: dict[str, Any]) -> dict[str, Any]:
        payload = {"id": str(uuid4()), **payload}
        return self._insert("scan_pairing_sessions", payload)

    def get_scan_pairing_session(self, pairing_code: str) -> dict[str, Any] | None:
        return self._first("scan_pairing_sessions", pairing_code=pairing_code)

    def get_scan_pairing_session_by_id(self, pairing_id: str) -> dict[str, Any] | None:
        return self._first("scan_pairing_sessions", id=pairing_id)

    def update_scan_pairing_session(self, pairing_code: str, updates: dict[str, Any]) -> None:
        self.client.table("scan_pairing_sessions").update(updates).eq("pairing_code", pairing_code).execute()

    # ── Scan reports & account ───────────────────────────────────────────────

    def create_scan_report(self, payload: dict[str, Any]) -> ScanReport:
        payload = {"id": str(uuid4()), **payload}
        row = self._insert("scan_reports", payload)
        return ScanReport.from_row(row)

    def get_scan_report(self, report_id: str) -> ScanReport | None:
        row = self._first("scan_reports", id=report_id)
        return ScanReport.from_row(row) if row else None

    def get_scan_report_by_verification_code(self, code: str) -> ScanReport | None:
        row = self._first("scan_reports", verification_code=code)
        return ScanReport.from_row(row) if row else None

    def get_scan_report_by_public_token(self, token: str) -> ScanReport | None:
        row = self._first("scan_reports", public_report_token=token)
        return ScanReport.from_row(row) if row else None

    def get_scan_report_by_certificate_id(self, certificate_id: str) -> ScanReport | None:
        row = self._first("scan_reports", certificate_id=certificate_id)
        return ScanReport.from_row(row) if row else None

    def get_scan_reports_for_user(self, user_id: str) -> list[ScanReport]:
        result = (
            self.client.table("scan_reports")
            .select("*")
            .eq("user_id", user_id)
            .order("created_at", desc=True)
            .execute()
        )
        return [ScanReport.from_row(row) for row in result.data or []]

    def update_scan_report(self, report_id: str, updates: dict[str, Any]) -> None:
        self.client.table("scan_reports").update(updates).eq("id", report_id).execute()

    def create_report_claim(self, payload: dict[str, Any]) -> dict[str, Any]:
        payload = {"id": str(uuid4()), **payload}
        return self._insert("report_claims", payload)

    def create_account_scan_link_token(self, payload: dict[str, Any]) -> dict[str, Any]:
        return self._insert("account_scan_link_tokens", payload)

    def get_account_scan_link_token(self, token: str) -> dict[str, Any] | None:
        return self._first("account_scan_link_tokens", token=token)

    def mark_account_scan_link_token_used(self, token: str, session_id: str) -> None:
        self.client.table("account_scan_link_tokens").update(
            {"used_at": datetime.now(timezone.utc).isoformat(), "scan_session_id": session_id}
        ).eq("token", token).execute()

    # ── Audit ────────────────────────────────────────────────────────────────

    def create_audit_log(self, payload: dict[str, Any]) -> AuditLog:
        payload = {"id": str(uuid4()), **payload}
        row = self._insert("audit_logs", payload)
        return AuditLog.from_row(row)


def get_db():
    db = Database()
    try:
        yield db
    finally:
        pass


def init_db() -> None:
    Database().seed_agent_version_if_missing()
