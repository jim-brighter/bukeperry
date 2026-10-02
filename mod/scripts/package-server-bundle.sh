#!/usr/bin/env bash
set -euo pipefail

# ==============================================================================
# Package Linux Dedicated Server Bundle for Bukeperry Mod
#
# Downloads BepInEx 5.4.2351 and Jötunn 2.30.2, bundles them with BukeperryMod.dll,
# and outputs bukeperry-server-bundle-v<version>.zip into mod/dist/.
# ==============================================================================

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MOD_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
DIST_DIR="$MOD_DIR/dist"
mkdir -p "$DIST_DIR"

PLUGIN_FILE="$MOD_DIR/BukeperryMod/BukeperryPlugin.cs"
if [[ ! -f "$PLUGIN_FILE" ]]; then
  echo "Error: Could not find $PLUGIN_FILE"
  exit 1
fi

MOD_VERSION=$(grep -o 'PluginVersion = "[^"]*"' "$PLUGIN_FILE" | cut -d'"' -f2)
DEFAULT_ZIP="$DIST_DIR/bukeperry-server-bundle-v${MOD_VERSION}.zip"
OUTPUT_ZIP="${1:-$DEFAULT_ZIP}"

BEPINEX_VERSION="5.4.2351"
JOTUNN_VERSION="2.30.2"

echo "=== Assembling Linux Dedicated Server Bundle (v$MOD_VERSION) ==="

TMP_DIR=$(mktemp -d /tmp/bukeperry-bundle.XXXXXX)
trap 'rm -rf "$TMP_DIR"' EXIT

# 1. Download BepInExPack_Valheim
echo "Downloading BepInExPack_Valheim $BEPINEX_VERSION..."
curl -sL "https://thunderstore.io/package/download/denikson/BepInExPack_Valheim/$BEPINEX_VERSION/" -o "$TMP_DIR/bepinex.zip"
unzip -q "$TMP_DIR/bepinex.zip" -d "$TMP_DIR/bepinex-raw" || true

mkdir -p "$TMP_DIR/staging"
cp -r "$TMP_DIR/bepinex-raw/BepInExPack_Valheim/"* "$TMP_DIR/staging/"

# 2. Download Jötunn
echo "Downloading Jötunn $JOTUNN_VERSION..."
curl -sL "https://thunderstore.io/package/download/ValheimModding/Jotunn/$JOTUNN_VERSION/" -o "$TMP_DIR/jotunn.zip"
mkdir -p "$TMP_DIR/jotunn-raw"
unzip -q "$TMP_DIR/jotunn.zip" -d "$TMP_DIR/jotunn-raw" || true

mkdir -p "$TMP_DIR/staging/BepInEx/plugins"
cp "$TMP_DIR/jotunn-raw/plugins/Jotunn.dll" "$TMP_DIR/staging/BepInEx/plugins/"
if [[ -f "$TMP_DIR/jotunn-raw/plugins/Jotunn.xml" ]]; then
  cp "$TMP_DIR/jotunn-raw/plugins/Jotunn.xml" "$TMP_DIR/staging/BepInEx/plugins/"
fi

# 3. Copy compiled BukeperryMod.dll
if [[ ! -f "$DIST_DIR/BukeperryMod.dll" ]]; then
  echo "mod/dist/BukeperryMod.dll not found, building now..."
  "$SCRIPT_DIR/macos-deploy.sh"
fi

echo "Copying BukeperryMod.dll..."
cp "$DIST_DIR/BukeperryMod.dll" "$TMP_DIR/staging/BepInEx/plugins/"

# 4. Create server bundle zip
echo "Compressing server bundle to: $OUTPUT_ZIP..."
(cd "$TMP_DIR/staging" && zip -q -r "$OUTPUT_ZIP" .)

echo "✓ Successfully created: $(ls -lh "$OUTPUT_ZIP")"
