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

## Build channels

| Channel | Command | Endpoint override |
|---------|---------|-------------------|
| **production** | `VERIFYTECH_BUILD_CHANNEL=production ./scripts/build-agent.sh` | Env var only with `VERIFYTECH_ALLOW_ENDPOINT_OVERRIDE=1` |
| **staging** | `VERIFYTECH_BUILD_CHANNEL=staging ./scripts/build-agent.sh` | Same as production |
| **development** | Default on Windows without channel | `appsettings.local.json`, `VERIFYTECH_API_BASE_URL`, `--api-url` |

WPF must be built on **Windows** with .NET 8 SDK.

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

Mock offline UI:

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
