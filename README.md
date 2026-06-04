# DevicePassport (VerifyTech)

A modular POC for device certification and verification — "Carfax for electronics."

## Architecture

```
VerifyTech/
├── frontend-public/          # Landing, intake, download, verify, certificate pages
├── frontend-dashboard/       # User dashboard (on hold — not in default dev stack)
├── backend-api/              # FastAPI — all application APIs
├── packages/
│   ├── shared/               # Hashing utilities
│   ├── schema-engine/        # Canonical device report schema & validation
│   ├── certificate-engine/   # Certificate generation
│   └── verification-engine/  # Report comparison logic
├── agent/windows/            # Windows agent placeholder
├── database/schema.sql         # Supabase Postgres + Storage schema
└── scripts/                  # POC test scripts
```

### Module Responsibilities

| Module | Purpose |
|--------|---------|
| **schema-engine** | Tier 1/2/3 canonical schema, platform-agnostic validation |
| **certificate-engine** | Certificate levels, codes, public payloads, QR data |
| **verification-engine** | Identity/value comparison, verification outcomes |
| **backend-api** | REST API, persistence, audit logs, tenant hooks |
| **frontend-public** | Public certification & verification UX |
| **frontend-dashboard** | User dashboard *(on hold)* |

Agents talk **only** to `backend-api` — never directly to Supabase.

## Quick Start

### Prerequisites

- Python 3.11+
- Node.js 20+
- Supabase project (Postgres + Auth)

### 1. Install Python packages

```bash
cd VerifyTech
python3 -m venv .venv
source .venv/bin/activate

pip install -e packages/shared
pip install -e packages/schema-engine
pip install -e packages/certificate-engine
pip install -e packages/verification-engine
pip install -r backend-api/requirements.txt
pip install httpx pytest
```

### 2. Run everything (recommended)

```bash
./scripts/dev.sh
```

Starts API (8000) and public site (3000). Press **Ctrl+C** to stop all.

```bash
./scripts/stop-dev.sh   # kill anything still on dev ports
./scripts/dev.sh --no-kill   # fail instead of freeing ports if busy
./scripts/dev.sh --with-dashboard   # also start dashboard on :3001 (optional)
```

### 3. Or start services separately

```bash
# API — copy .env.example and set Supabase credentials first
cd backend-api && cp .env.example .env
uvicorn app.main:app --reload --port 8000

# Public site
cd frontend-public && cp .env.example .env.local && npm run dev
```

- API docs: http://localhost:8000/docs
- Public site: http://localhost:3000

**frontend-public env** (`frontend-public/.env.local`):

| Variable | Local | Production (Vercel) |
|----------|-------|---------------------|
| `NEXT_PUBLIC_API_URL` | `http://localhost:8000` | Railway API URL |
| `NEXT_PUBLIC_SITE_URL` | `http://localhost:3000` | Vercel site URL or custom domain |

> **Note:** `frontend-dashboard` is on hold. To run it locally: `cd frontend-dashboard && npm install && npm run dev -- --port 3001` or `./scripts/dev.sh --with-dashboard`.

**frontend-dashboard env** (`frontend-dashboard/.env.local`) — when resuming work:

| Variable | Purpose |
|----------|---------|
| `NEXT_PUBLIC_API_URL` | Railway API URL |
| `NEXT_PUBLIC_PUBLIC_SITE_URL` | frontend-public URL |
| `NEXT_PUBLIC_SUPABASE_URL` | Supabase project URL (Auth) |
| `NEXT_PUBLIC_SUPABASE_ANON_KEY` | Supabase anon key only — **never** service role in frontend |

### 4. Test the full flow

```bash
# Submit a sample certification report
python scripts/submit_sample_report.py

# Verify the certificate (use code from output)
CERTIFICATE_CODE=XXXX-XXXX-XXXX python scripts/submit_verification.py
```

## User Flows

### Certification
1. Land on `/` → OS detected
2. `/start` — 3 required intake questions
3. `/download` — Windows agent download
4. Agent submits report → `POST /api/reports`
5. Certificate generated → `/c/{certificate_code}`

### Verification (no login)
1. `/verify` — enter certificate code
2. Download verifier agent
3. Agent submits live scan → `POST /api/verify/submit`
4. `/verification-result/{attempt_id}` — match result

## API Endpoints

| Group | Prefix | Key Routes |
|-------|--------|------------|
| Intake | `/api/intake` | POST intake form |
| Agents | `/api/agents` | GET platform agent, OS detect |
| Reports | `/api/reports` | POST device report |
| Certificates | `/api/certificates` | GET public certificate |
| Verify | `/api/verify` | POST lookup, submit, GET attempt |
| Dashboard | `/api/dashboard` | GET user certificates |
| Tenants | `/api/tenants` | GET tenant list |
| Auth Profile | `/api/auth-profile` | GET/create profile |

## Certificate Levels

- **Identity Verified** — Tier 1 complete
- **Condition Certified** — Tier 1 + Tier 2 complete
- **Enhanced Certified** — Tier 2 + 5+ hardware tests passed

## Database

The API uses the **Supabase client** (service role key) for all persistence. Configure in `backend-api/.env`:

```env
SUPABASE_URL=https://xxxx.supabase.co
SUPABASE_SERVICE_ROLE_KEY=your-service-role-key
SUPABASE_ANON_KEY=your-anon-key
SUPABASE_AGENT_BUCKET=agent-releases
SUPABASE_AGENT_FILENAME=DeviceCertAgent.exe
```

1. Create a Supabase project
2. Run `database/schema.sql` in the Supabase SQL editor
3. Copy project URL, anon key, and service role key from Supabase → Project Settings → API

### Agent downloads (Supabase Storage)

Windows agent binaries live in the private **`agent-releases`** bucket.

- `agent_versions.download_url` stores the object path, e.g. `windows/0.1.0/DeviceCertAgent.exe`
- `GET /api/agents/windows` returns a **short-lived signed URL** (default 1 hour) — direct bucket URLs do not work
- The frontend still only calls the API; no Supabase credentials in the browser
- Local dev fallback: paths starting with `/agents/` serve from the API

Run migration `database/schema.sql` if setting up a new project. If you previously used separate migration files, your database is already up to date — no need to re-run.

Upload a release after building on Windows:

```bash
source .venv/bin/activate
python scripts/upload_agent.py --version 0.1.0 --notes "Initial release"
```

All major tables support nullable `tenant_id` for consumer vs refurbisher flows.

## Deploy backend (Railway)

Config: `backend-api/railway.toml`

1. New Railway service from this repo — **leave Root Directory empty** (monorepo Docker build)
2. Settings → Config file path: `backend-api/railway.toml`
3. Add env vars from `backend-api/.env.example` (Supabase keys, `PUBLIC_BASE_URL`, `CORS_ORIGINS`, etc.)
4. After deploy, set `API_BASE_URL` to the Railway public URL

Health check: `GET /health`

## Security & Privacy

- Sensitive identifiers hashed before storage (SHA-256)
- Public certificates show masked identifiers only
- Raw reports stored server-side, not exposed publicly
- Audit logs for certificate creation, verification, report submission

## Tests

```bash
pytest packages/schema-engine/tests -v
pytest packages/verification-engine/tests -v
```

## What's NOT in this POC

- Blockchain, price prediction, PDF generation
- Full refurbisher dashboard, macOS/Android agents
- Marketplace integrations, AI cosmetic grading

All modules are structured for clean extension.

## Environment Variables

See `backend-api/.env.example` and frontend `.env.local` files.

## License

POC — internal use.
