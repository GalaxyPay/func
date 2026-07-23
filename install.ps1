# FUNC installer for Windows.
# Detects your architecture, downloads the latest release, and installs it.
#
# Usage (in PowerShell):
#   irm https://raw.githubusercontent.com/GalaxyPay/func/main/install.ps1 | iex

$ErrorActionPreference = "Stop"
$repo = "GalaxyPay/func"

# --- Detect architecture ---
# PROCESSOR_ARCHITEW6432 is set when running a 32-bit process on 64-bit Windows;
# fall back to PROCESSOR_ARCHITECTURE otherwise.
$archRaw = $env:PROCESSOR_ARCHITEW6432
if (-not $archRaw) { $archRaw = $env:PROCESSOR_ARCHITECTURE }

$arch = switch ($archRaw) {
    "AMD64" { "amd64" }
    "ARM64" { "arm64" }
    default { throw "Unsupported architecture: '$archRaw'" }
}

Write-Host "Detected windows-$arch"

# --- Find the matching asset in the latest release ---
$pattern = "_windows-$arch.exe"
Write-Host "Looking up the latest release..."
$release = Invoke-RestMethod "https://api.github.com/repos/$repo/releases/latest"
$asset = $release.assets | Where-Object { $_.name -like "*$pattern" } | Select-Object -First 1

if (-not $asset) {
    throw "Could not find a release asset matching *$pattern. See https://github.com/$repo/releases"
}

# --- Download ---
$out = Join-Path $env:TEMP $asset.name
Write-Host "Downloading $($asset.name)..."
Invoke-WebRequest $asset.browser_download_url -OutFile $out

# --- Install (runs silently; elevates via UAC if not already admin) ---
Write-Host "Installing..."
$silentArgs = "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART"
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if ($isAdmin) {
    # Already elevated (e.g. run by the FUNC service for a self-upgrade);
    # -Verb RunAs would fail in a non-interactive session.
    Start-Process -FilePath $out -ArgumentList $silentArgs -Wait
}
else {
    Start-Process -FilePath $out -ArgumentList $silentArgs -Verb RunAs -Wait
}

Remove-Item $out -Force
Write-Host ""
Write-Host "FUNC installed. Open http://localhost:3536 and bookmark it."
