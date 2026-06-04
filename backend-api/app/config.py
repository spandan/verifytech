from pathlib import Path

from pydantic import AliasChoices, Field, model_validator
from pydantic_settings import BaseSettings, SettingsConfigDict

_BACKEND_ENV = Path(__file__).resolve().parent.parent / ".env"


class Settings(BaseSettings):
    model_config = SettingsConfigDict(
        env_file=str(_BACKEND_ENV),
        env_file_encoding="utf-8",
        extra="ignore",
    )

    app_name: str = "DevicePassport API"
    debug: bool = True
    public_base_url: str = "http://localhost:3000"
    api_base_url: str = "http://localhost:8000"
    certificate_ttl_days: int = 365
    cors_origins: str = "http://localhost:3000"

    supabase_url: str = ""
    supabase_service_role_key: str = Field(
        default="",
        validation_alias=AliasChoices("SUPABASE_SERVICE_ROLE_KEY", "SUPABASE_SERVICE_KEY"),
    )
    supabase_anon_key: str = Field(
        default="",
        validation_alias=AliasChoices("SUPABASE_ANON_KEY", "SUPABASE_KEY"),
    )

    # Supabase Storage — agent executables (private bucket, signed download URLs)
    supabase_agent_bucket: str = "agent-releases"
    supabase_agent_filename: str = "DeviceCertAgent.exe"
    supabase_agent_signed_url_ttl_seconds: int = 3600

    scan_session_ttl_minutes: int = 20
    scan_session_min_duration_seconds: int = 5
    scan_session_max_duration_hours: int = 2
    allowed_agent_versions: str = "0.1.0,0.2.0,1.0.0"

    @property
    def allowed_agent_version_list(self) -> list[str]:
        return [v.strip() for v in self.allowed_agent_versions.split(",") if v.strip()]

    @property
    def cors_origin_list(self) -> list[str]:
        return [o.strip() for o in self.cors_origins.split(",") if o.strip()]

    @model_validator(mode="after")
    def validate_supabase_config(self) -> "Settings":
        if not self.supabase_url.strip():
            raise ValueError("SUPABASE_URL is required")
        if not self.supabase_service_role_key:
            raise ValueError("SUPABASE_SERVICE_ROLE_KEY is required")
        if not self.supabase_anon_key:
            raise ValueError("SUPABASE_ANON_KEY is required")
        if not self.supabase_agent_bucket.strip():
            raise ValueError("SUPABASE_AGENT_BUCKET is required")
        if self.supabase_agent_signed_url_ttl_seconds < 60:
            raise ValueError("SUPABASE_AGENT_SIGNED_URL_TTL_SECONDS must be at least 60")
        return self


settings = Settings()
