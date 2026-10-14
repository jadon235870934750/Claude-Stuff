#!/usr/bin/env bash
# Builds dist/Robloxcraft-<version>-windows.zip: portable Prism Launcher with a Fabric 1.21.1
# instance holding the Robloxcraft mod. No Minecraft or Roblox files go in it.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PRISM_VERSION=11.1.1
PRISM_SHA256=ab35a770fb06d89d2ccc098079db5db329fb4e68f42b72babd8b095efde3d2d7
PRISM_URL="https://github.com/PrismLauncher/PrismLauncher/releases/download/${PRISM_VERSION}/PrismLauncher-Windows-MSVC-Portable-${PRISM_VERSION}.zip"
VERSION=$(sed -n 's/^mod_version=//p' "$ROOT/mod/gradle.properties")
CACHE="${PRISM_CACHE:-$ROOT/build-cache}"
STAGE="$ROOT/dist/stage"
OUT="$ROOT/dist/Robloxcraft-${VERSION}-windows.zip"

python3 -I "$ROOT/tools/sheets.py" preflight-full
(cd "$ROOT/mod" && ./gradlew build -q)

mkdir -p "$CACHE"
PRISM_ZIP="$CACHE/prism-${PRISM_VERSION}.zip"
[ -f "$PRISM_ZIP" ] || curl -sSL -o "$PRISM_ZIP" "$PRISM_URL"
echo "${PRISM_SHA256}  ${PRISM_ZIP}" | sha256sum -c -

rm -rf "$STAGE" "$OUT"
mkdir -p "$STAGE/Robloxcraft"
unzip -q "$PRISM_ZIP" -d "$STAGE/Robloxcraft"
INST="$STAGE/Robloxcraft/instances/Robloxcraft"
mkdir -p "$INST/minecraft/mods"
cp "$ROOT/packaging/instance/mmc-pack.json" "$ROOT/packaging/instance/instance.cfg" "$INST/"
cp "$ROOT/mod/build/libs/robloxcraft-${VERSION}.jar" "$INST/minecraft/mods/"
cp "$ROOT/packaging/CREDITS.txt" "$STAGE/Robloxcraft/ROBLOXCRAFT-CREDITS.txt"
(cd "$STAGE" && zip -qr -X "$OUT" Robloxcraft)
rm -rf "$STAGE"
ls -l "$OUT"
sha256sum "$OUT"
