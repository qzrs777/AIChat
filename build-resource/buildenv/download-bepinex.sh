#!/bin/bash
# Download BepInEx for building AIChat

set -e

# BepInEx version can be overridden with environment variable
BEPINEX_VERSION="${BEPINEX_VERSION:-5.4.23.4}"
BEPINEX_URL="https://github.com/BepInEx/BepInEx/releases/download/v${BEPINEX_VERSION}/BepInEx_win_x64_${BEPINEX_VERSION}.zip"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "${SCRIPT_DIR}"
ASSETS_DIR="${SCRIPT_DIR}/../assets"
MOKGAME_DIR="${SCRIPT_DIR}/mokgamedir"

BEPINEX_DIR="${ASSETS_DIR}/BepInEx_win_x64"

###############################################
echo "Downloading BepInEx v${BEPINEX_VERSION}..."

mkdir -p ${BEPINEX_DIR}
cd ${BEPINEX_DIR}
curl -L -f -o bepinex.zip "${BEPINEX_URL}"
if ! unzip -t bepinex.zip > /dev/null 2>&1; then
    echo "Error: Downloaded file is not a valid zip archive" >&2
    rm bepinex.zip
    exit 1
fi
unzip -q bepinex.zip
rm bepinex.zip
echo "Extracted \"bepinex.zip\"."

echo "BepInEx setup complete!"
