#!/usr/bin/env bash
# Build and publish DeviceCertAgent for Windows x64
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT/agent/windows"

# Homebrew dotnet@8 is keg-only — ensure it's on PATH
if ! command -v dotnet >/dev/null 2>&1; then
  if [ -d "/opt/homebrew/opt/dotnet@8/libexec" ]; then
    export DOTNET_ROOT="/opt/homebrew/opt/dotnet@8/libexec"
    export PATH="/opt/homebrew/opt/dotnet@8/bin:$PATH"
  elif [ -d "/usr/local/opt/dotnet@8/libexec" ]; then
    export DOTNET_ROOT="/usr/local/opt/dotnet@8/libexec"
    export PATH="/usr/local/opt/dotnet@8/bin:$PATH"
  fi
fi

if ! command -v dotnet >/dev/null 2>&1; then
  echo "Error: dotnet SDK not found."
  echo "Install with: brew install dotnet@8   (macOS)"
  echo "Or download .NET 8 SDK from https://dotnet.microsoft.com/download"
  exit 1
fi

echo "Using dotnet: $(dotnet --version)"

CHANNEL="${VERIFYTECH_BUILD_CHANNEL:-production}"

echo "Restoring packages..."
dotnet restore DeviceCertAgent.sln /p:VerifyTechBuildChannel="${CHANNEL}"

echo "Running Core tests..."
dotnet test DeviceCertAgent.Core/DeviceCertAgent.Core.csproj -c Release /p:VerifyTechBuildChannel="${CHANNEL}" 2>/dev/null || dotnet test DeviceCertAgent.Tests/DeviceCertAgent.Tests.csproj -c Release /p:VerifyTechBuildChannel="${CHANNEL}"

if [[ "$(uname -s)" != "MINGW"* && "$(uname -s)" != "MSYS"* && "$(uname -s)" != "CYGWIN"* && "$(uname -s)" != "Windows_NT" ]]; then
  echo ""
  echo "Note: WPF desktop app requires Windows to publish."
  echo "Core library built and tested. Run this script on Windows to produce DeviceCertAgent.exe"
  exit 0
fi

echo "Publishing WPF desktop app (win-x64, channel=${CHANNEL})..."
dotnet publish DeviceCertAgent.App/DeviceCertAgent.App.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  /p:PublishSingleFile=true \
  /p:IncludeNativeLibrariesForSelfExtract=true \
  /p:EnableCompressionInSingleFile=true \
  /p:VerifyTechBuildChannel="${CHANNEL}" \
  -o publish

echo ""
echo "Built: agent/windows/publish/DeviceCertAgent.exe"
echo "Copy to backend static path or distribute to users."
