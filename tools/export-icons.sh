#!/usr/bin/env bash
# Export a family logo into an app's PWA icon set.
#   tools/export-icons.sh <makdous|nxttask|radio> <output-dir> [prefix]
# Writes: logo.svg, favicon.png (64), apple-touch-icon.png (180), icon-192.png, icon-512.png,
# icon-maskable-512.png (full-bleed, for Android adaptive icons). Needs inkscape.
set -euo pipefail
here="$(cd "$(dirname "$0")/.." && pwd)"
src="$here/wwwroot/logos/$1.svg"
out="$2"
prefix="${3:-}"
mkdir -p "$out"
tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

cp "$src" "$out/${prefix}logo.svg"
# Full-bleed square for maskable icons: drop the corner radius; the glyph already sits in the safe zone.
sed 's/rx="114"/rx="0"/g' "$src" > "$tmp/maskable.svg"

render() { inkscape "$1" -o "$2" -w "$3" -h "$3" >/dev/null 2>&1; }
render "$src" "$out/${prefix}favicon.png" 64
render "$src" "$out/${prefix}icon-192.png" 192
render "$src" "$out/${prefix}icon-512.png" 512
render "$tmp/maskable.svg" "$out/${prefix}apple-touch-icon.png" 180   # iOS rounds the corners itself
render "$tmp/maskable.svg" "$out/${prefix}icon-maskable-512.png" 512
echo "Exported $1 icons to $out"
