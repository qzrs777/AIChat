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

echo "Downloading BepInEx v${BEPINEX_VERSION}..."

# Function to download and extract BepInEx
download_and_extract() {
    local url="$1"
    local tmp_dir=/tmp/bepinex-deps
    
    cd "${tmp_dir}" || return 1
    echo "Downloading from ${url}..." >&2
    
    if ! curl -L -f -o bepinex.zip "${url}"; then
        echo "Error: Failed to download BepInEx from ${url}" >&2
        rm -rf "${tmp_dir}"
        return 1
    fi
    
    # Verify the downloaded file is a valid zip
    if ! unzip -t bepinex.zip > /dev/null 2>&1; then
        echo "Error: Downloaded file is not a valid zip archive" >&2
        rm -rf "${tmp_dir}"
        return 1
    fi
    
    unzip -q bepinex.zip
    
    echo "${tmp_dir}"
    return 0
}

# Download BepInEx for build environment (core libraries for compilation)
if [ ! -d "${MOKGAME_DIR}/BepInEx" ]; then
    echo "Downloading BepInEx for build environment..."
    
    TMP_DIR=$(download_and_extract "${BEPINEX_URL}")
    if [ $? -ne 0 ] || [ -z "${TMP_DIR}" ]; then
        echo "Error: Failed to download BepInEx for build environment" >&2
        exit 1
    fi
    
    # Copy only BepInEx/core directory for compilation
    if [ -d "${TMP_DIR}/BepInEx/core" ]; then
        mkdir -p "${MOKGAME_DIR}/BepInEx"
        cp -r "${TMP_DIR}/BepInEx/core" "${MOKGAME_DIR}/BepInEx/"
        echo "BepInEx core libraries installed for build environment."
    else
        echo "Error: BepInEx/core directory not found in downloaded archive" >&2
        rm -rf "${TMP_DIR}"
        exit 1
    fi
    
    rm -rf "${TMP_DIR}"
else
    echo "BepInEx already exists in build environment."
fi

# Download BepInEx for release assets
if [ ! -d "${ASSETS_DIR}/BepInEx_win_x64_${BEPINEX_VERSION}" ]; then
    echo "Downloading BepInEx for release assets..."
    
    TMP_DIR=$(download_and_extract "${BEPINEX_URL}")
    if [ $? -ne 0 ] || [ -z "${TMP_DIR}" ]; then
        echo "Error: Failed to download BepInEx for release assets" >&2
        exit 1
    fi
    
    # Move all BepInEx files to assets
    mkdir -p "${ASSETS_DIR}/BepInEx_win_x64_${BEPINEX_VERSION}"
    
    # Check and move files
    for file in BepInEx winhttp.dll doorstop_config.ini changelog.txt .doorstop_version; do
        if [ -e "${TMP_DIR}/${file}" ]; then
            mv "${TMP_DIR}/${file}" "${ASSETS_DIR}/BepInEx_win_x64_${BEPINEX_VERSION}/"
        else
            echo "Warning: ${file} not found in downloaded archive" >&2
        fi
    done
    
    rm -rf "${TMP_DIR}"
    echo "BepInEx downloaded for release assets."
else
    echo "BepInEx already exists in release assets."
fi

echo "BepInEx setup complete!"
