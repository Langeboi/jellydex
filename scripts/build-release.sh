#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 3 ]]; then
  echo "Usage: $0 <version> <jellyfin-version> <package-suffix>" >&2
  exit 2
fi

version="$1"
jellyfin_version="$2"
suffix="$3"
project="src/Jellyfin.Plugin.KommerSnart/Jellyfin.Plugin.KommerSnart.csproj"
output="artifacts/publish-$suffix"
archive="artifacts/kommer-snart-$version-jellyfin-$suffix.zip"
archive_path="$(pwd)/$archive"

rm -rf "$output"
mkdir -p "$output" artifacts

dotnet publish "$project" \
  --configuration Release \
  --output "$output" \
  -p:JellyfinVersion="$jellyfin_version" \
  -p:Version="$version"

test -f "$output/Jellyfin.Plugin.KommerSnart.dll"
if command -v zip >/dev/null 2>&1; then
  (
    cd "$output"
    zip -9 -j "$archive_path" Jellyfin.Plugin.KommerSnart.dll
  )
elif command -v 7z >/dev/null 2>&1; then
  (
    cd "$output"
    7z a -tzip -mx=9 "$archive_path" Jellyfin.Plugin.KommerSnart.dll
  )
else
  echo "A ZIP writer (zip or 7z) is required." >&2
  exit 1
fi
