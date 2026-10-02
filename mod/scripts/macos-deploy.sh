#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MOD_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
VALHEIM_PLUGINS="$HOME/Library/Application Support/Steam/steamapps/common/Valheim/BepInEx/plugins"

echo "Building BukeperryMod..."
cd "$MOD_DIR/BukeperryMod"
dotnet build

echo "Installing Jotunn mod to local Valheim..."
if [[ -f "$HOME/.nuget/packages/jotunnlib/2.30.2/lib/net462/Jotunn.dll" ]]; then
  mkdir -p "$VALHEIM_PLUGINS"
  cp "$HOME/.nuget/packages/jotunnlib/2.30.2/lib/net462/Jotunn.dll" "$VALHEIM_PLUGINS/"
fi

echo "Installing BukeperryMod.dll to local Valheim..."
mkdir -p "$VALHEIM_PLUGINS"
cp bin/Debug/netstandard2.1/BukeperryMod.dll "$VALHEIM_PLUGINS/"

echo "Copying BukeperryMod.dll to mod/dist for repository distribution..."
mkdir -p "$MOD_DIR/dist"
cp bin/Debug/netstandard2.1/BukeperryMod.dll "$MOD_DIR/dist/"

echo "Done"
