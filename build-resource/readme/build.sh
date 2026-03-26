#!/bin/bash

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ASSETS_DIR="${SCRIPT_DIR}/../assets"

echo "Building README.html..."

# github.css 来自 https://github.com/otsaloma/markdown-css
pandoc \
  --metadata title="Chill AI Mod 说明" \
  --standalone --embed-resources \
  --css=${SCRIPT_DIR}/github.css \
  -f gfm --to=html5 \
  --highlight-style=haddock \
  --output=${ASSETS_DIR}/请读我.html \
  README.md

echo "Build README.html complete!"
