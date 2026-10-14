#!/usr/bin/env bash
# Builds the release: dist/studcraft-<version>.jar. Melty installs it into the player's Fabric 1.21.1
# Minecraft instance and brings Prism Launcher, Java and Fabric Loader itself. No game files go in it.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
VERSION=$(sed -n 's/^mod_version=//p' "$ROOT/mod/gradle.properties")
python3 -I "$ROOT/tools/sheets.py" generate
(cd "$ROOT/mod" && ./gradlew build -q)
mkdir -p "$ROOT/dist"
cp "$ROOT/mod/build/libs/studcraft-${VERSION}.jar" "$ROOT/dist/"
ls -l "$ROOT/dist/studcraft-${VERSION}.jar"
sha256sum "$ROOT/dist/studcraft-${VERSION}.jar"
