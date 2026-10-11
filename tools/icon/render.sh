#!/bin/sh
# Erzeugt die Rastergrafiken des App-Symbols aus icon.svg (braucht rsvg-convert und ImageMagick).
set -e
cd "$(dirname "$0")"
src=../../src
rsvg-convert -w 256 -h 256 icon.svg -o "$src/DSACharacterCreator.App/Assets/Icon.png"
# Android vor 8.0 (ohne adaptives Symbol): xxxhdpi-Größe
rsvg-convert -w 192 -h 192 icon.svg -o "$src/DSACharacterCreator.Android/Icon.png"
tmp=$(mktemp -d)
for s in 16 24 32 48 64 128 256; do rsvg-convert -w $s -h $s icon.svg -o "$tmp/$s.png"; done
magick "$tmp"/16.png "$tmp"/24.png "$tmp"/32.png "$tmp"/48.png "$tmp"/64.png "$tmp"/128.png "$tmp"/256.png \
  "$src/DSACharacterCreator.Desktop/Icon.ico"
rm -r "$tmp"
