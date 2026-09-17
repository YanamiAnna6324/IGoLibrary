#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 3 ]]; then
  echo "Usage: $0 <package.tar.gz> <linux-x64|linux-arm64> <version>" >&2
  exit 1
fi

PACKAGE_PATH="$1"
RUNTIME="$2"
APP_VERSION="$3"
EXPECTED_NAME="IGoLibrary-Ex-v$APP_VERSION-$RUNTIME.tar.gz"

case "$RUNTIME" in
  linux-x64|linux-arm64) ;;
  *)
    echo "Unsupported Linux runtime: $RUNTIME" >&2
    exit 1
    ;;
esac
if [[ ! $APP_VERSION =~ ^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$ ]]; then
  echo "Invalid version: $APP_VERSION" >&2
  exit 1
fi
if [[ ! -f "$PACKAGE_PATH" ]]; then
  echo "Package was not found: $PACKAGE_PATH" >&2
  exit 1
fi
if [[ "$(basename "$PACKAGE_PATH")" != "$EXPECTED_NAME" ]]; then
  echo "Unexpected package name. Expected: $EXPECTED_NAME" >&2
  exit 1
fi

while IFS= read -r entry; do
  normalized="${entry%/}"
  if [[ -z "$normalized" || "$normalized" == /* || "$normalized" == *\\* ]]; then
    echo "Package contains an invalid path: $entry" >&2
    exit 1
  fi
  IFS='/' read -r -a parts <<< "$normalized"
  if [[ "${parts[0]}" != "IGoLibrary-Ex" ]]; then
    echo "Package entry is outside the expected root directory: $entry" >&2
    exit 1
  fi
  for part in "${parts[@]}"; do
    if [[ "$part" == ".." ]]; then
      echo "Package contains a parent-directory path: $entry" >&2
      exit 1
    fi
  done
done < <(tar -tzf "$PACKAGE_PATH")

if tar -tvzf "$PACKAGE_PATH" | awk '$1 ~ /^l/ { found=1 } END { exit found ? 0 : 1 }'; then
  echo "Package must not contain symbolic links." >&2
  exit 1
fi

TEMP_ROOT="$(mktemp -d -t igolibrary-linux-package.XXXXXX)"
trap 'rm -rf -- "$TEMP_ROOT"' EXIT
tar -xzf "$PACKAGE_PATH" -C "$TEMP_ROOT"
PACKAGE_ROOT="$TEMP_ROOT/IGoLibrary-Ex"
EXECUTABLE="$PACKAGE_ROOT/IGoLibrary.Ex.Desktop"

[[ -f "$EXECUTABLE" ]] || { echo "Desktop executable is missing." >&2; exit 1; }
[[ -x "$EXECUTABLE" ]] || { echo "Desktop executable is not executable." >&2; exit 1; }
[[ -f "$PACKAGE_ROOT/IGoLibrary.Ex.Desktop.dll" ]] || { echo "Desktop assembly is missing." >&2; exit 1; }
[[ -f "$PACKAGE_ROOT/IGoLibrary.Ex.Desktop.runtimeconfig.json" ]] || { echo "Runtime config is missing." >&2; exit 1; }
[[ -f "$PACKAGE_ROOT/README-LINUX.md" ]] || { echo "Linux installation guide is missing." >&2; exit 1; }
[[ -f "$PACKAGE_ROOT/licenses/cloudflared-LICENSE.txt" ]] || { echo "cloudflared license is missing." >&2; exit 1; }
[[ -f "$PACKAGE_ROOT/licenses/THIRD-PARTY-NOTICES.txt" ]] || { echo "third-party notices are missing." >&2; exit 1; }
[[ -x "$PACKAGE_ROOT/install-windows-launcher.sh" ]] || { echo "WSL Windows launcher installer is missing or not executable." >&2; exit 1; }
[[ -f "$PACKAGE_ROOT/scripts/install-windows-wsl-launcher.ps1" ]] || { echo "WSL Windows launcher PowerShell installer is missing." >&2; exit 1; }
[[ -f "$PACKAGE_ROOT/icons/IGoLibrary-Ex.ico" ]] || { echo "Windows launcher icon is missing." >&2; exit 1; }
[[ "$(tr -d '\r\n' < "$PACKAGE_ROOT/VERSION")" == "$APP_VERSION" ]] || { echo "Package version marker is missing or invalid." >&2; exit 1; }

EXECUTABLE_DESCRIPTION="$(file -b "$EXECUTABLE")"
case "$RUNTIME" in
  linux-x64)
    [[ "$EXECUTABLE_DESCRIPTION" == *"x86-64"* ]] || {
      echo "Desktop executable is not x86-64: $EXECUTABLE_DESCRIPTION" >&2
      exit 1
    }
    ;;
  linux-arm64)
    [[ "$EXECUTABLE_DESCRIPTION" == *"ARM aarch64"* ]] || {
      echo "Desktop executable is not ARM64: $EXECUTABLE_DESCRIPTION" >&2
      exit 1
    }
    ;;
esac

if find "$PACKAGE_ROOT" -maxdepth 1 -type f \( -iname 'IGoLibrary.Ex.Launcher.exe' -o -iname 'IGoLibrary.Ex.Updater.exe' \) | grep -q .; then
  echo "Linux package contains a Windows launcher or updater executable." >&2
  exit 1
fi

echo "Linux package verification passed: $PACKAGE_PATH"
