#!/usr/bin/env bash
set -euo pipefail

CONFIGURATION="${1:-Release}"
RUNTIME="${2:-linux-x64}"
APP_VERSION="${APP_VERSION:-}"

if [[ -z "${APP_VERSION//[[:space:]]/}" ]]; then
  echo "APP_VERSION is required. Example: APP_VERSION=1.0.1 ./build/publish-linux.sh Release linux-x64" >&2
  exit 1
fi
if [[ ! $APP_VERSION =~ ^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$ ]]; then
  echo "Invalid APP_VERSION: $APP_VERSION. Use N.N.N without a v prefix or leading zeroes." >&2
  exit 1
fi
case "$RUNTIME" in
  linux-x64|linux-arm64) ;;
  *)
    echo "Unsupported Linux runtime: $RUNTIME" >&2
    exit 1
    ;;
esac
if ! command -v dotnet >/dev/null 2>&1; then
  echo "dotnet SDK 10 is required." >&2
  exit 1
fi
if ! command -v tar >/dev/null 2>&1 ||
   ! command -v sha256sum >/dev/null 2>&1 ||
   ! command -v file >/dev/null 2>&1; then
  echo "tar, sha256sum and file are required." >&2
  exit 1
fi

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd -P)"
ARTIFACTS_ROOT="$ROOT/artifacts"
PUBLISH_DIR="$ARTIFACTS_ROOT/publish/$RUNTIME"
STAGING_ROOT="$ARTIFACTS_ROOT/staging/linux/$RUNTIME"
PACKAGE_ROOT="$STAGING_ROOT/IGoLibrary-Ex"
DIST_DIR="$ARTIFACTS_ROOT/linux/$RUNTIME"
PACKAGE_NAME="IGoLibrary-Ex-v$APP_VERSION-$RUNTIME.tar.gz"
PACKAGE_PATH="$DIST_DIR/$PACKAGE_NAME"
PROJECT="$ROOT/src/IGoLibrary.Ex.Desktop/IGoLibrary.Ex.Desktop.csproj"

remove_build_directory() {
  local target="$1"
  case "$target" in
    "$ARTIFACTS_ROOT"/*) ;;
    *)
      echo "Refusing to clean a directory outside artifacts: $target" >&2
      exit 1
      ;;
  esac
  [[ "$target" != "$ARTIFACTS_ROOT" ]]
  rm -rf -- "$target"
}

remove_build_directory "$PUBLISH_DIR"
remove_build_directory "$STAGING_ROOT"
mkdir -p "$PUBLISH_DIR" "$PACKAGE_ROOT" "$DIST_DIR"

dotnet publish "$PROJECT" \
  --configuration "$CONFIGURATION" \
  --runtime "$RUNTIME" \
  --self-contained true \
  --output "$PUBLISH_DIR" \
  -p:Version="$APP_VERSION" \
  -p:InformationalVersion="$APP_VERSION"

cp -a "$PUBLISH_DIR/." "$PACKAGE_ROOT/"
cp "$ROOT/docs/linux-installation.md" "$PACKAGE_ROOT/README-LINUX.md"
mkdir -p "$PACKAGE_ROOT/licenses"
cp "$ROOT/build/third-party/cloudflared-LICENSE.txt" "$PACKAGE_ROOT/licenses/cloudflared-LICENSE.txt"
cp "$ROOT/build/third-party/THIRD-PARTY-NOTICES.txt" "$PACKAGE_ROOT/licenses/THIRD-PARTY-NOTICES.txt"
chmod 0755 "$PACKAGE_ROOT/IGoLibrary.Ex.Desktop"
if [[ -f "$PACKAGE_ROOT/tools/cloudflared/cloudflared" ]]; then
  chmod 0755 "$PACKAGE_ROOT/tools/cloudflared/cloudflared"
fi

rm -f -- "$PACKAGE_PATH" "$PACKAGE_PATH.sha256"
tar -C "$STAGING_ROOT" -czf "$PACKAGE_PATH" IGoLibrary-Ex
(
  cd "$DIST_DIR"
  sha256sum "$PACKAGE_NAME" > "$PACKAGE_NAME.sha256"
)

"$ROOT/build/verify-linux-package.sh" "$PACKAGE_PATH" "$RUNTIME" "$APP_VERSION"
echo "Linux package: $PACKAGE_PATH"
echo "SHA256 file: $PACKAGE_PATH.sha256"
