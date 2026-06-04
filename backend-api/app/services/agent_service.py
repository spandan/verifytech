"""Agent management — platform agent downloads and versions."""

from __future__ import annotations

from app.db.models import Database
from app.schemas.dto import AgentDownloadResponse, AgentVersionResponse
from app.services.agent_storage_service import resolve_agent_download_url


class AgentService:
    SUPPORTED_PLATFORMS = {"windows", "macos", "android", "linux"}

    def get_active_agent(self, db: Database, platform: str) -> AgentDownloadResponse | None:
        platform = platform.lower()
        agent = db.get_active_agent(platform)
        if not agent:
            return None

        full_url = resolve_agent_download_url(agent.download_url)

        return AgentDownloadResponse(
            platform=agent.platform,
            version=agent.version,
            download_url=agent.download_url,
            checksum=agent.checksum,
            full_download_url=full_url,
        )

    def list_active(self, db: Database) -> list[AgentVersionResponse]:
        agents = db.list_active_agents()
        seen: set[str] = set()
        result = []
        for agent in agents:
            if agent.platform in seen:
                continue
            seen.add(agent.platform)
            result.append(
                AgentVersionResponse(
                    platform=agent.platform,
                    version=agent.version,
                    download_url=agent.download_url,
                    checksum=agent.checksum,
                    release_notes=agent.release_notes,
                    minimum_supported_schema_version=agent.minimum_supported_schema_version,
                )
            )
        return result

    def detect_platform_from_user_agent(self, user_agent: str) -> str:
        ua = user_agent.lower()
        if "windows" in ua:
            return "windows"
        if "mac os" in ua or "macintosh" in ua:
            return "macos"
        if "android" in ua:
            return "android"
        if "iphone" in ua or "ipad" in ua:
            return "ios"
        if "linux" in ua:
            return "linux"
        return "unknown"
