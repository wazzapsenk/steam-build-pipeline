#!/usr/bin/env bash
#
# Builds SteamBuildPipeline.unitypackage from the package sources in this repo.
#
# A .unitypackage is just a gzip-compressed tar where each asset lives in a folder
# named by its GUID and holds:
#   - pathname   : the project-relative path the asset lands at on import
#   - asset.meta : the .meta (its guid MUST match the folder name)
#   - asset      : the raw file bytes (omitted for folder entries)
#
# On import the files land under Assets/Plugins/WazangGames/SteamBuildPipeline/ and
# behave like normal project assets (movable, editable). This is the alternative to
# the UPM git-package install (which lives read-only under Packages/).
#
# Re-run this whenever you change the Editor/ sources or bump the version.
#
# This script lives in Tools~/ rather than the repo root on purpose: Unity ignores any
# folder whose name ends in '~', so when the repo is consumed as a UPM git package the
# script never reaches the asset database. A file with no .meta inside Packages/ (an
# immutable folder Unity cannot write metas into) logs a warning on every import.
set -euo pipefail
cd "$(dirname "$0")/.."

DEST="Assets/Plugins/WazangGames/SteamBuildPipeline"
OUT="SteamBuildPipeline.unitypackage"
BUILD="$(mktemp -d)"
trap 'rm -rf "$BUILD"' EXIT

# Folder-entry GUIDs (stable so rebuilds produce consistent packages).
GUID_WAZANG="88131dca6fd843e09b5cdddfd5486562"
GUID_TOOL="d3e8a2e1b385462a8d1e80f4c0a6f04a"

folder_meta() {  # folder_meta <guid>
  printf 'fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n' "$1"
}

# add_folder <guid> <pathname> [meta-file]
# If a meta-file is given, its guid wins (so the folder keeps the repo's existing guid).
add_folder() {
  local guid="$1" pathname="$2" metafile="${3:-}"
  if [ -n "$metafile" ]; then guid="$(grep -m1 '^guid:' "$metafile" | awk '{print $2}')"; fi
  mkdir -p "$BUILD/$guid"
  printf '%s\n' "$pathname" > "$BUILD/$guid/pathname"
  if [ -n "$metafile" ]; then cp "$metafile" "$BUILD/$guid/asset.meta"
  else folder_meta "$guid" > "$BUILD/$guid/asset.meta"; fi
}

# add_file <src> <meta> <pathname>  (guid is read from the meta)
add_file() {
  local src="$1" meta="$2" pathname="$3"
  local guid; guid="$(grep -m1 '^guid:' "$meta" | awk '{print $2}')"
  mkdir -p "$BUILD/$guid"
  printf '%s\n' "$pathname" > "$BUILD/$guid/pathname"
  cp "$meta" "$BUILD/$guid/asset.meta"
  cp "$src"  "$BUILD/$guid/asset"
}

# --- folders (parents created top-down) ---
add_folder "$GUID_WAZANG" "Assets/Plugins/WazangGames"
add_folder "$GUID_TOOL"   "$DEST"
add_folder "" "$DEST/Editor" "Editor.meta"   # reuse the Editor folder's existing meta/guid

# --- Editor scripts + asmdef ---
add_file "Editor/SteamBuildConfig.cs"                       "Editor/SteamBuildConfig.cs.meta"                       "$DEST/Editor/SteamBuildConfig.cs"
add_file "Editor/SteamBuildPipelineRunner.cs"              "Editor/SteamBuildPipelineRunner.cs.meta"              "$DEST/Editor/SteamBuildPipelineRunner.cs"
add_file "Editor/SteamBuildPipelineWindow.cs"             "Editor/SteamBuildPipelineWindow.cs.meta"             "$DEST/Editor/SteamBuildPipelineWindow.cs"
add_file "Editor/SteamAppIdBuildPostProcessor.cs"        "Editor/SteamAppIdBuildPostProcessor.cs.meta"        "$DEST/Editor/SteamAppIdBuildPostProcessor.cs"
add_file "Editor/WazangGames.SteamBuildPipeline.Editor.asmdef" "Editor/WazangGames.SteamBuildPipeline.Editor.asmdef.meta" "$DEST/Editor/WazangGames.SteamBuildPipeline.Editor.asmdef"

# --- reference docs (handy to have next to the scripts; package.json is UPM-only, skipped) ---
add_file "README.md"  "README.md.meta"  "$DEST/README.md"
add_file "LICENSE.md" "LICENSE.md.meta" "$DEST/LICENSE.md"

tar -czf "$OUT" -C "$BUILD" .
echo "Wrote $OUT ($(du -h "$OUT" | cut -f1))"
