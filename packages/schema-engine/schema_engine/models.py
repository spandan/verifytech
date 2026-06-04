"""Canonical device report schema — platform-agnostic."""

from __future__ import annotations

from datetime import datetime
from enum import Enum
from typing import Any, Optional

from pydantic import BaseModel, Field


CURRENT_SCHEMA_VERSION = "1.0.0"


class Platform(str, Enum):
    WINDOWS = "windows"
    MACOS = "macos"
    ANDROID = "android"
    LINUX = "linux"
    IOS = "ios"
    UNKNOWN = "unknown"


class DeviceType(str, Enum):
    LAPTOP = "laptop"
    DESKTOP = "desktop"
    TABLET = "tablet"
    PHONE = "phone"
    CHROMEBOOK = "chromebook"
    OTHER = "other"


class OSFamily(str, Enum):
    WINDOWS = "windows"
    MACOS = "macos"
    ANDROID = "android"
    IOS = "ios"
    LINUX = "linux"
    CHROMEOS = "chromeos"
    OTHER = "other"


# ─── Tier 1: Certification Identity ─────────────────────────────────────────

class Tier1Identity(BaseModel):
    """Required for identity certification. Platform-agnostic."""

    manufacturer: str
    model: str
    device_type: DeviceType
    platform: Platform
    os_family: OSFamily
    os_version: str
    serial_number_hash: str
    hardware_uuid_hash: str
    motherboard_serial_hash: Optional[str] = None
    primary_storage_serial_hash: Optional[str] = None
    cpu_model: str
    ram_total_gb: float
    storage_total_gb: float
    collector_version: str
    collected_at: datetime


# ─── Tier 2: Value Determination ─────────────────────────────────────────────

class StorageDetail(BaseModel):
    drive_index: int = 0
    capacity_gb: float
    type: str  # SSD, HDD, NVMe
    health_percent: Optional[float] = None
    smart_status: Optional[str] = None


class Tier2Value(BaseModel):
    """Required for condition/value certificate."""

    cpu_details: Optional[str] = None
    ram_details: Optional[str] = None
    storage_details: list[StorageDetail] = Field(default_factory=list)
    battery_health_percent: Optional[float] = None
    battery_cycle_count: Optional[int] = None
    display_resolution: Optional[str] = None
    display_status: Optional[str] = None  # ok, dead_pixels, cracked, etc.
    gpu_model: Optional[str] = None
    camera_test_passed: Optional[bool] = None
    microphone_test_passed: Optional[bool] = None
    speaker_test_passed: Optional[bool] = None
    keyboard_test_passed: Optional[bool] = None
    touchpad_test_passed: Optional[bool] = None
    wifi_test_passed: Optional[bool] = None
    charging_test_passed: Optional[bool] = None
    cosmetic_grade: Optional[str] = None  # A, B, C, D


# ─── Tier 3: Optional Intelligence ──────────────────────────────────────────

class Tier3Optional(BaseModel):
    """Nice-to-have. Never blocks certification."""

    tpm_present: Optional[bool] = None
    secure_boot_enabled: Optional[bool] = None
    disk_encryption: Optional[str] = None  # bitlocker, filevault, none
    bios_version: Optional[str] = None
    firmware_version: Optional[str] = None
    network_adapters: list[dict[str, Any]] = Field(default_factory=list)
    port_details: list[dict[str, Any]] = Field(default_factory=list)
    thermal_data: Optional[dict[str, Any]] = None
    benchmark_scores: Optional[dict[str, Any]] = None
    photo_urls: list[str] = Field(default_factory=list)
    driver_issues: list[str] = Field(default_factory=list)
    activation_status: Optional[str] = None


# ─── Full Device Report ───────────────────────────────────────────────────────

class DeviceReport(BaseModel):
    """Canonical device report submitted by agents."""

    schema_version: str = CURRENT_SCHEMA_VERSION
    tier1: Tier1Identity
    tier2: Tier2Value = Field(default_factory=Tier2Value)
    tier3: Tier3Optional = Field(default_factory=Tier3Optional)
    raw_extensions: dict[str, Any] = Field(default_factory=dict)


class ValidationResult(BaseModel):
    """Result of schema validation."""

    valid: bool
    schema_version: str
    tier1_complete: bool
    tier2_complete: bool
    tier1_errors: list[str] = Field(default_factory=list)
    tier2_errors: list[str] = Field(default_factory=list)
    warnings: list[str] = Field(default_factory=list)
