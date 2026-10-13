#!/usr/bin/env bash
# Export a family logo into an app's icon set.
#   tools/export-icons.sh <nxttask|makdous|radio|courses|carousel> <output-dir> [prefix]
# Writes: logo.png (512, rounded tile with transparent corners), favicon.png (64), icon-192.png, icon-512.png,
# apple-touch-icon.png (180) and icon-maskable-512.png (full-bleed, for Android adaptive icons).
# The master logos in wwwroot/logos/ were cut from design/family-icons-source.webp. Needs ImageMagick.
set -euo pipefail
here="$(cd "$(dirname "$0")/.." && pwd)"
src="$here/wwwroot/logos/$1.png"
out="$2"
prefix="${3:-}"
[ -f "$src" ] || { echo "No logo named '$1' in wwwroot/logos" >&2; exit 1; }
mkdir -p "$out"
tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

cp "$src" "$out/${prefix}logo.png"
# Full-bleed square (no transparent corners) for maskable icons; iOS rounds the corners itself.
magick "$src" -gravity center -crop 82%x82%+0+0 +repage -alpha off -resize 512x512 "$tmp/bleed.png"

magick "$src" -resize 64x64 "$out/${prefix}favicon.png"
magick "$src" -resize 192x192 "$out/${prefix}icon-192.png"
magick "$src" -resize 512x512 "$out/${prefix}icon-512.png"
magick "$tmp/bleed.png" -resize 180x180 "$out/${prefix}apple-touch-icon.png"
cp "$tmp/bleed.png" "$out/${prefix}icon-maskable-512.png"
echo "Exported $1 icons to $out"
