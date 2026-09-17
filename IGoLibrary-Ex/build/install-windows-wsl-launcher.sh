#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
if [[ -n "${2:-}" ]]; then
  VERSION="$2"
elif [[ -f "$SCRIPT_DIR/VERSION" ]]; then
  VERSION="$(tr -d '\r\n' < "$SCRIPT_DIR/VERSION")"
else
  VERSION="0.0.0"
fi

if [[ -z "${WSL_DISTRO_NAME:-}" ]]; then
  echo "This installer must run inside WSL." >&2
  exit 1
fi
if ! command -v powershell.exe >/dev/null 2>&1 || ! command -v wslpath >/dev/null 2>&1; then
  echo "Windows PowerShell interop is unavailable in this WSL session." >&2
  exit 1
fi

if [[ -n "${1:-}" ]]; then
  LINUX_EXECUTABLE="$(realpath -- "$1")"
elif [[ -x "$SCRIPT_DIR/IGoLibrary.Ex.Desktop" ]]; then
  LINUX_EXECUTABLE="$SCRIPT_DIR/IGoLibrary.Ex.Desktop"
else
  MACHINE="$(uname -m)"
  case "$MACHINE" in
    x86_64|amd64) RUNTIME="linux-x64" ;;
    aarch64|arm64) RUNTIME="linux-arm64" ;;
    *)
      echo "Unsupported WSL architecture: $MACHINE" >&2
      exit 1
      ;;
  esac
  REPOSITORY_ROOT="$(cd "$SCRIPT_DIR/.." && pwd -P)"
  LINUX_EXECUTABLE="$REPOSITORY_ROOT/artifacts/publish/$RUNTIME/IGoLibrary.Ex.Desktop"
fi

if [[ ! -x "$LINUX_EXECUTABLE" ]]; then
  echo "Linux executable is missing or not executable: $LINUX_EXECUTABLE" >&2
  echo "Build it first with: make publish" >&2
  exit 1
fi

if [[ -f "$SCRIPT_DIR/install-windows-wsl-launcher.ps1" ]]; then
  POWERSHELL_INSTALLER="$SCRIPT_DIR/install-windows-wsl-launcher.ps1"
  ICON_SOURCE="$(cd "$SCRIPT_DIR/.." && pwd -P)/src/IGoLibrary.Ex.Desktop/Assets/main.ico"
elif [[ -f "$SCRIPT_DIR/scripts/install-windows-wsl-launcher.ps1" ]]; then
  POWERSHELL_INSTALLER="$SCRIPT_DIR/scripts/install-windows-wsl-launcher.ps1"
  ICON_SOURCE="$SCRIPT_DIR/icons/IGoLibrary-Ex.ico"
else
  echo "Windows launcher PowerShell installer was not found." >&2
  exit 1
fi

POWERSHELL_INSTALLER_WINDOWS="$(wslpath -w "$POWERSHELL_INSTALLER")"
ICON_SOURCE_WINDOWS="$(wslpath -w "$ICON_SOURCE")"

powershell.exe -NoProfile -ExecutionPolicy Bypass \
  -File "$POWERSHELL_INSTALLER_WINDOWS" \
  -Distribution "$WSL_DISTRO_NAME" \
  -LinuxExecutablePath "$LINUX_EXECUTABLE" \
  -IconSourcePath "$ICON_SOURCE_WINDOWS" \
  -Version "$VERSION"
