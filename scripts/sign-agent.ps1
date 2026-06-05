# Sign VerifyTech Agent executable (requires code signing certificate in Windows cert store)
param(
    [string]$ExePath = "$PSScriptRoot\..\agent\windows\publish\DeviceCertAgent.exe",
    [string]$Thumbprint = $env:VERIFYTECH_SIGNING_THUMBPRINT
)

if (-not (Test-Path $ExePath)) {
    Write-Error "Executable not found: $ExePath"
    exit 1
}

if (-not $Thumbprint) {
    Write-Host "VERIFYTECH_SIGNING_THUMBPRINT not set — skipping sign (artifact remains unsigned)."
    Write-Host "Set thumbprint of your Authenticode cert to enable signing."
    exit 0
}

$signtool = Get-Command signtool.exe -ErrorAction SilentlyContinue
if (-not $signtool) {
    Write-Error "signtool.exe not found. Install Windows SDK."
    exit 1
}

& signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /sha1 $Thumbprint $ExePath
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "Signed: $ExePath"
