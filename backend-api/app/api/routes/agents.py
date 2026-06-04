from fastapi import APIRouter, Depends, HTTPException, Request

from app.db.models import Database, get_db
from app.schemas.dto import AgentDownloadResponse, AgentVersionResponse
from app.services.agent_service import AgentService

router = APIRouter(prefix="/api/agents", tags=["agents"])
_service = AgentService()


@router.get("/detect")
def detect_platform(request: Request):
    ua = request.headers.get("user-agent", "")
    platform = _service.detect_platform_from_user_agent(ua)
    return {"platform": platform, "user_agent": ua}


@router.get("/{platform}", response_model=AgentDownloadResponse)
def get_agent(platform: str, db: Database = Depends(get_db)):
    agent = _service.get_active_agent(db, platform)
    if not agent:
        raise HTTPException(status_code=404, detail=f"No active agent for platform: {platform}")
    return agent


@router.get("", response_model=list[AgentVersionResponse])
def list_agents(db: Database = Depends(get_db)):
    return _service.list_active(db)
