#!/bin/sh
# FUNC installer for Linux and macOS.
# Detects your OS/arch, downloads the latest release, and installs it.
#
# Usage:
#   curl -fsSL https://raw.githubusercontent.com/GalaxyPay/func/main/install.sh | sudo sh

set -eu

REPO="GalaxyPay/func"

# --- Detect OS ---
OS="$(uname -s)"
case "$OS" in
  Linux)  PLATFORM="linux";  EXT="deb" ;;
  Darwin) PLATFORM="darwin"; EXT="pkg" ;;
  *) echo "Unsupported operating system: $OS" >&2; exit 1 ;;
esac

# --- Detect architecture ---
ARCH="$(uname -m)"
case "$ARCH" in
  x86_64|amd64)   ARCH="amd64" ;;
  aarch64|arm64)  ARCH="arm64" ;;
  *) echo "Unsupported architecture: $ARCH" >&2; exit 1 ;;
esac

echo "Detected ${PLATFORM}-${ARCH}"

# --- Find the matching asset in the latest release ---
API="https://api.github.com/repos/${REPO}/releases/latest"
PATTERN="_${PLATFORM}-${ARCH}.${EXT}"

echo "Looking up the latest release..."
RESPONSE="$(curl -fsSL "$API")"
URL="$(printf '%s' "$RESPONSE" | grep 'browser_download_url' | grep "$PATTERN" | head -n1 | cut -d '"' -f4 || true)"

if [ -z "$URL" ]; then
  echo "Could not find a release asset matching *${PATTERN}" >&2
  echo "See https://github.com/${REPO}/releases" >&2
  exit 1
fi

# --- Download ---
TMPDIR="$(mktemp -d)"
FILE="${TMPDIR}/$(basename "$URL")"
trap 'rm -rf "$TMPDIR"' EXIT

echo "Downloading $(basename "$URL")..."
curl -fsSL "$URL" -o "$FILE"

# --- Elevate if not already root ---
SUDO=""
if [ "$(id -u)" -ne 0 ]; then
  SUDO="sudo"
fi

# --- Install ---
echo "Installing..."
if [ "$EXT" = "deb" ]; then
  $SUDO dpkg -i "$FILE"
else
  $SUDO installer -target / -pkg "$FILE"
fi

echo ""
echo "FUNC installed. Open http://localhost:3536 and bookmark it."
