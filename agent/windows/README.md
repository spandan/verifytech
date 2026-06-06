# VerifyTech Agent — Windows Desktop

Professional WPF wizard for device certification with short-lived scan sessions (no embedded API keys).

## Architecture

```
agent/windows/
├── DeviceCertAgent.App/      # WPF UI (VerifyTech branded)
├── DeviceCertAgent.Core/     # Collectors, scan sessions, API client
├── DeviceCertAgent.Tests/
└── publish/                  # DeviceCertAgent.exe after Windows build
```

## Default production API

`https://verifytech-production.up.railway.app`

Production builds use this endpoint automatically. Endpoint overrides are disabled unless built for development/QA.

## Build & run (Windows)

The published agent is **framework-dependent** (~25 MB exe). It does **not** bundle .NET — users need the [.NET 8 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/8.0).

### Build (from repo root)

```cmd
.\scripts\build-agent.cmd -Channel production
```

Other channels:

```cmd
.\scripts\build-agent.cmd -Channel staging
.\scripts\build-agent.cmd -Channel development
```

PowerShell equivalent:

```powershell
.\scripts\build-agent.ps1 -Channel production
```

Output folder:

```
agent\windows\publish\
├── DeviceCertAgent.exe          # main app (~25 MB)
├── Launch-VerifyTechAgent.cmd   # checks runtime, then starts the app
└── REQUIREMENTS.txt             # runtime install notes
```

Close any running `DeviceCertAgent.exe` before rebuilding.

### Run locally after build

**Recommended** — checks for .NET 8 Desktop Runtime and opens the download page if missing:

```cmd
agent\windows\publish\Launch-VerifyTechAgent.cmd
```

Or run the exe directly (Windows shows its own “.NET required” dialog if runtime is missing):

```cmd
agent\windows\publish\DeviceCertAgent.exe
```

Enhanced scan (admin relaunch):

```cmd
agent\windows\publish\DeviceCertAgent.exe --enhanced-scan
```

Mock API (offline UI):

```cmd
agent\windows\publish\DeviceCertAgent.exe --mock-api
```

### Distribute to users

Ship the whole `publish` folder (or at minimum `DeviceCertAgent.exe` + `Launch-VerifyTechAgent.cmd` + `REQUIREMENTS.txt`).

## Build channels

| Channel | Command | Endpoint override |
|---------|---------|-------------------|
| **production** | `VERIFYTECH_BUILD_CHANNEL=production ./scripts/build-agent.sh` | Env var only with `VERIFYTECH_ALLOW_ENDPOINT_OVERRIDE=1` |
| **staging** | `VERIFYTECH_BUILD_CHANNEL=staging ./scripts/build-agent.sh` | Same as production |
| **development** | Default on Windows without channel | `appsettings.local.json`, `VERIFYTECH_API_BASE_URL`, `--api-url` |

WPF must be built on **Windows** with .NET 8 **SDK** (not just the runtime).

```cmd
.\scripts\build-agent.cmd -Channel production
```

```bash
VERIFYTECH_BUILD_CHANNEL=production ./scripts/build-agent.sh
```

## Local development

Copy `appsettings.local.json.example` to `DeviceCertAgent.App/appsettings.local.json`:

```json
{
  "apiBaseUrl": "http://localhost:8000",
  "environment": "development",
  "mockApi": false
}
```

Or set:

```bash
set VERIFYTECH_API_BASE_URL=http://localhost:8000
```

Or run from `agent\windows\publish\`:

```cmd
DeviceCertAgent.exe --mock-api
```

## Secure scan flow

1. `POST /api/scan-sessions/start` → `sessionId`, `nonce`, `expiresAt`
2. Local diagnostic scan (basic or enhanced admin)
3. `POST /api/scan-sessions/{sessionId}/submit` → certificate + report URL

The server validates nonce, expiry, single-use session, scan timing, and hardware fingerprint.

## Promote release

```bash
python scripts/upload_agent.py --version 1.0.0 --notes "VerifyTech Agent WPF + scan sessions"
```

Apply `database/schema.sql` (includes `scan_sessions` table) in Supabase before deploying the API.

## Code signing

Sign the published executable before distribution:

```powershell
signtool sign /fd SHA256 /a publish\DeviceCertAgent.exe
```
