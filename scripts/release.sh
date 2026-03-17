#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

if command -v git >/dev/null 2>&1 && git -C "$REPO_ROOT" rev-parse --show-toplevel >/dev/null 2>&1; then
  REPO_ROOT="$(git -C "$REPO_ROOT" rev-parse --show-toplevel)"
fi

cd "$REPO_ROOT"

MODINFO_PATH="$REPO_ROOT/modinfo.json"
PROJECT_PATH="$REPO_ROOT/StenchMod.csproj"
BUILD_CONFIG="${BUILD_CONFIG:-Release}"
OUTPUT_DIR="$REPO_ROOT/releases"
ASSEMBLY_NAME="StenchMod.dll"

if [[ ! -f "$MODINFO_PATH" ]]; then
  echo "Brak modinfo.json w $MODINFO_PATH" >&2
  exit 1
fi

if [[ ! -f "$PROJECT_PATH" ]]; then
  echo "Brak StenchMod.csproj w $PROJECT_PATH" >&2
  exit 1
fi

json_value() {
  local key="$1"
  sed -nE "s/^[[:space:]]*\"${key}\"[[:space:]]*:[[:space:]]*\"([^\"]+)\".*/\\1/p" "$MODINFO_PATH" | head -n 1
}

MOD_NAME="$(json_value name)"
MOD_VERSION="$(json_value version)"
MOD_ID="$(json_value modid)"

if [[ -z "$MOD_NAME" || -z "$MOD_VERSION" || -z "$MOD_ID" ]]; then
  echo "Nie udało się odczytać name/version/modid z modinfo.json" >&2
  exit 1
fi

slugify() {
  local value="$1"
  value="${value,,}"
  value="$(printf '%s' "$value" | sed -E 's/[^a-z0-9]+/_/g; s/^_+//; s/_+$//; s/_+/_/g')"
  printf '%s' "$value"
}

ARCHIVE_BASENAME="$(slugify "$MOD_NAME")_${MOD_VERSION}"
BUILD_OUTPUT_DIR="$REPO_ROOT/bin/$BUILD_CONFIG"
BUILD_DLL_PATH="$BUILD_OUTPUT_DIR/$ASSEMBLY_NAME"
SKIP_BUILD="${SKIP_BUILD:-0}"

find_dotnet() {
  if command -v dotnet >/dev/null 2>&1; then
    command -v dotnet
    return 0
  fi

  if command -v dotnet.exe >/dev/null 2>&1; then
    command -v dotnet.exe
    return 0
  fi

  return 1
}

DOTNET_BIN="$(find_dotnet || true)"
CMD_BIN="$(command -v cmd.exe || true)"

to_windows_path() {
  local path="$1"

  if command -v wslpath >/dev/null 2>&1; then
    wslpath -w "$path"
    return 0
  fi

  printf '%s' "$path"
}

next_archive_path() {
  local base_name="$1"
  local output_dir="$2"
  local candidate="$output_dir/${base_name}.zip"
  local suffix=1

  while [[ -e "$candidate" ]]; do
    candidate="$output_dir/${base_name}_${suffix}.zip"
    suffix=$((suffix + 1))
  done

  printf '%s' "$candidate"
}

mkdir -p "$OUTPUT_DIR"
ARCHIVE_PATH="$(next_archive_path "$ARCHIVE_BASENAME" "$OUTPUT_DIR")"

if command -v git >/dev/null 2>&1 && git -C "$REPO_ROOT" rev-parse --show-toplevel >/dev/null 2>&1; then
  if [[ -n "$(git -C "$REPO_ROOT" status --short)" ]]; then
    echo "Uwaga: git worktree ma niezacommitowane zmiany." >&2
  fi

  HEAD_TAG="$(git -C "$REPO_ROOT" tag --points-at HEAD | grep -E "^(v)?${MOD_VERSION}$" | head -n 1 || true)"
  if [[ -z "$HEAD_TAG" ]]; then
    echo "Uwaga: HEAD nie ma taga zgodnego z wersją ${MOD_VERSION}." >&2
  else
    echo "Git tag dla release: $HEAD_TAG"
  fi
fi

if [[ "$SKIP_BUILD" != "1" ]]; then
  echo "Buduję projekt: $PROJECT_PATH"

  if [[ -n "$DOTNET_BIN" && "$DOTNET_BIN" != *.exe ]]; then
    "$DOTNET_BIN" build "$PROJECT_PATH" -c "$BUILD_CONFIG"
  elif [[ -n "$DOTNET_BIN" && "$DOTNET_BIN" == *.exe ]]; then
    "$DOTNET_BIN" build "$(to_windows_path "$PROJECT_PATH")" -c "$BUILD_CONFIG"
  elif [[ -n "$CMD_BIN" ]]; then
    "$CMD_BIN" /c dotnet build "$(to_windows_path "$PROJECT_PATH")" -c "$BUILD_CONFIG"
  else
    echo "Nie znaleziono dotnet, dotnet.exe ani cmd.exe do wykonania builda." >&2
    exit 1
  fi
else
  echo "Pomijam build, używam istniejącego artefaktu: $BUILD_DLL_PATH"
fi

if [[ ! -f "$BUILD_DLL_PATH" ]]; then
  echo "Brak zbudowanego DLL: $BUILD_DLL_PATH" >&2
  exit 1
fi

STAGING_DIR="$(mktemp -d)"
cleanup() {
  rm -rf "$STAGING_DIR"
}
trap cleanup EXIT

install -m 0644 "$MODINFO_PATH" "$STAGING_DIR/modinfo.json"
install -m 0644 "$REPO_ROOT/README.md" "$STAGING_DIR/README.md"
install -m 0644 "$REPO_ROOT/CHANGELOG.md" "$STAGING_DIR/CHANGELOG.md"
install -m 0644 "$REPO_ROOT/LICENSE" "$STAGING_DIR/LICENSE"
install -m 0644 "$REPO_ROOT/THIRD_PARTY_NOTICES.md" "$STAGING_DIR/THIRD_PARTY_NOTICES.md"
install -m 0644 "$BUILD_DLL_PATH" "$STAGING_DIR/$ASSEMBLY_NAME"
cp -R "$REPO_ROOT/assets" "$STAGING_DIR/assets"

python3 - "$STAGING_DIR" "$ARCHIVE_PATH" <<'PY'
import os
import sys
import zipfile

staging_dir = sys.argv[1]
archive_path = sys.argv[2]

with zipfile.ZipFile(archive_path, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=9) as zf:
    for root, dirs, files in os.walk(staging_dir):
        dirs.sort()
        files.sort()
        for name in files:
            full_path = os.path.join(root, name)
            rel_path = os.path.relpath(full_path, staging_dir)
            zf.write(full_path, rel_path)
PY

echo "Gotowe:"
echo "  Mod ID: $MOD_ID"
echo "  Wersja: $MOD_VERSION"
echo "  Paczka: $ARCHIVE_PATH"
