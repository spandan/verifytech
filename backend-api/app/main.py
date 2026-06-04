from contextlib import asynccontextmanager
from pathlib import Path

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import FileResponse, PlainTextResponse

from app.api.routes import agents, auth_profile, certificates, dashboard, intake, reports, tenants, verify
from app.config import settings
from app.db.models import init_db

AGENT_DIR = Path(__file__).resolve().parent.parent.parent / "agent" / "windows"
PUBLISH_DIR = AGENT_DIR / "publish"


@asynccontextmanager
async def lifespan(app: FastAPI):
    init_db()
    yield


app = FastAPI(
    title=settings.app_name,
    version="0.1.0",
    lifespan=lifespan,
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=settings.cors_origin_list,
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

app.include_router(intake.router)
app.include_router(agents.router)
app.include_router(reports.router)
app.include_router(certificates.router)
app.include_router(verify.router)
app.include_router(dashboard.router)
app.include_router(tenants.router)
app.include_router(auth_profile.router)


@app.get("/")
def root():
    return {
        "service": settings.app_name,
        "message": "This is the API server. The web app runs separately.",
        "links": {
            "docs": "/docs",
            "health": "/health",
            "public_site": settings.public_base_url,
        },
    }


@app.get("/health")
def health():
    return {"status": "ok", "service": settings.app_name}


@app.get("/agents/{filename}")
def download_agent(filename: str):
    """Serve Windows agent executable or placeholder."""
    exe_candidates = [
        PUBLISH_DIR / "DeviceCertAgent.exe",
        AGENT_DIR / "publish" / "DeviceCertAgent.exe",
    ]
    for exe in exe_candidates:
        if filename in ("DeviceCertAgent.exe", "devicepassport-agent-windows-0.1.0.exe") and exe.exists():
            return FileResponse(exe, filename="DeviceCertAgent.exe", media_type="application/octet-stream")

    readme = AGENT_DIR / "README.md"
    if filename.endswith(".exe"):
        return PlainTextResponse(
            content=(
                "DeviceCertAgent.exe not built yet. Run scripts/build-agent.sh on Windows to publish.\n\n"
                + (readme.read_text() if readme.exists() else "")
            ),
            media_type="text/plain",
            headers={"Content-Disposition": f'attachment; filename="{filename}"'},
        )
    agent_file = AGENT_DIR / filename
    if agent_file.exists():
        return FileResponse(agent_file)
    return PlainTextResponse("Not found", status_code=404)
