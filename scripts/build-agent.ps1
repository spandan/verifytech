# Build and publish DeviceCertAgent for Windows x64
param(
    [ValidateSet('production', 'staging', 'development')]
    [string]$Channel = $(if ($env:VERIFYTECH_BUILD_CHANNEL) { $env:VERIFYTECH_BUILD_CHANNEL } else { 'production' })
)

$ErrorActionPreference = 'Stop'

function Resolve-DotNetCli {
    $cmd = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    $candidates = @(
        "$env:ProgramFiles\dotnet\dotnet.exe",
        "${env:ProgramFiles(x86)}\dotnet\dotnet.exe",
        "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe"
    )

    foreach ($path in $candidates) {
        if (Test-Path $path) {
            $dotnetDir = Split-Path $path -Parent
            $env:PATH = "$dotnetDir;$env:PATH"
            return $path
        }
    }

    return $null
}

function Assert-AgentNotRunning {
    $running = Get-Process -Name DeviceCertAgent -ErrorAction SilentlyContinue
    if ($running) {
        Write-Host 'Error: DeviceCertAgent.exe is still running.' -ForegroundColor Red
        Write-Host 'Close the app (or end the process in Task Manager), then rebuild.'
        exit 1
    }

    $publishExe = Join-Path (Get-Location) 'publish\DeviceCertAgent.exe'
    if (-not (Test-Path $publishExe)) { return }

    try {
        $stream = [System.IO.File]::Open($publishExe, 'Open', 'Read', 'None')
        $stream.Close()
    } catch {
        Write-Host 'Error: publish\DeviceCertAgent.exe is locked by another process.' -ForegroundColor Red
        Write-Host 'Close the app and any editor preview of the exe, then rebuild.'
        exit 1
    }
}

$DotNet = Resolve-DotNetCli
if (-not $DotNet) {
    Write-Host 'Error: .NET 8 SDK not found.' -ForegroundColor Red
    Write-Host ''
    Write-Host 'Install the SDK, then open a new terminal and rerun the build:'
    Write-Host '  winget install Microsoft.DotNet.SDK.8'
    Write-Host ''
    Write-Host 'Or download from https://dotnet.microsoft.com/download/dotnet/8.0'
    Write-Host '(Choose "SDK x64" — not just the runtime.)'
    exit 1
}

$Root = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location (Join-Path $Root 'agent/windows')

Write-Host "Using dotnet: $(& $DotNet --version) ($DotNet)"

Write-Host 'Restoring packages...'
& $DotNet restore DeviceCertAgent.sln /p:VerifyTechBuildChannel="$Channel"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host 'Running Core tests...'
& $DotNet test DeviceCertAgent.Core/DeviceCertAgent.Core.csproj -c Release /p:VerifyTechBuildChannel="$Channel" 2>$null
if ($LASTEXITCODE -ne 0) {
    & $DotNet test DeviceCertAgent.Tests/DeviceCertAgent.Tests.csproj -c Release /p:VerifyTechBuildChannel="$Channel"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

Write-Host "Publishing WPF desktop app (win-x64, channel=$Channel)..."
Assert-AgentNotRunning

$publishDir = Join-Path (Get-Location) 'publish'
if (Test-Path $publishDir) {
    Remove-Item (Join-Path $publishDir 'appsettings.local.json') -Force -ErrorAction SilentlyContinue
}

& $DotNet publish DeviceCertAgent.App/DeviceCertAgent.App.csproj `
    -c Release `
    -r win-x64 `
    --self-contained false `
    /p:PublishSingleFile=true `
    /p:VerifyTechBuildChannel="$Channel" `
    -o publish
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Get-ChildItem (Join-Path $publishDir '*.pdb') -ErrorAction SilentlyContinue | Remove-Item -Force

$exePath = Join-Path $publishDir 'DeviceCertAgent.exe'
$exeMb = if (Test-Path $exePath) { [math]::Round((Get-Item $exePath).Length / 1MB, 2) } else { 0 }

Write-Host ''
Write-Host "Built: agent/windows/publish/DeviceCertAgent.exe ($exeMb MB, framework-dependent)"
Write-Host 'Requires .NET 8 Desktop Runtime: https://dotnet.microsoft.com/download/dotnet/8.0'
Write-Host 'Use Launch-VerifyTechAgent.cmd to check runtime before starting.'
if ($exeMb -gt 50) {
    Write-Host "Warning: executable is above 50 MB ($exeMb MB)." -ForegroundColor Yellow
}
