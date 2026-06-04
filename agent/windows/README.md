# DeviceCertAgent — Windows Desktop Agent

Professional WPF desktop app + shared Core library for device certification and verification.

## Architecture

```
agent/windows/
├── DeviceCertAgent.App/      # WPF UI (MVVM) — main executable
├── DeviceCertAgent.Core/     # Collectors, hashing, schema, API client
├── DeviceCertAgent.Tests/    # Unit tests
└── publish/                  # Published DeviceCertAgent.exe (after build)
```

## API endpoint configuration

1. `--api-url http://localhost:8000` (CLI)
2. `appsettings.local.json` (beside exe)
3. Default: `https://api.yourdomain.com`

See `appsettings.local.json.example`. Set `"mockApi": true` for offline UI testing.

## Build

**WPF must be built on Windows** with .NET 8 SDK.

Mac/Linux — Core + tests only:

```bash
dotnet build DeviceCertAgent.Core/DeviceCertAgent.Core.csproj
dotnet test
```

Windows publish:

```bash
dotnet publish DeviceCertAgent.App/DeviceCertAgent.App.csproj \
  -c Release -r win-x64 --self-contained true \
  /p:PublishSingleFile=true -o publish
```

## Usage

```cmd
DeviceCertAgent.exe
DeviceCertAgent.exe --mode certify --api-url http://localhost:8000
DeviceCertAgent.exe --mode verify --certificate-code XXXX-XXXX-XXXX --api-url http://localhost:8000
DeviceCertAgent.exe --mock-api
```
