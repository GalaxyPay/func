# FUNC installer for Windows.
# Detects your architecture, downloads the latest release, and installs it.
#
# Usage (in PowerShell):
#   irm https://raw.githubusercontent.com/GalaxyPay/func/main/install.ps1 | iex

$ErrorActionPreference = "Stop"
$repo = "GalaxyPay/func"

# --- Detect architecture ---
$arch = switch ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture) {
    "X64"   { "amd64" }
    "Arm64" { "arm64" }
    default { throw "Unsupported architecture: $_" }
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

# --- Install (elevates via UAC; runs silently) ---
Write-Host "Installing..."
Start-Process -FilePath $out -ArgumentList "/VERYSILENT","/SUPPRESSMSGBOXES","/NORESTART" -Verb RunAs -Wait

Remove-Item $out -Force
Write-Host ""
Write-Host "FUNC installed. Open http://localhost:3536 and bookmark it."
