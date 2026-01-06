#!/bin/bash
# Download UnityEngine DLLs from nuget for building AIChat

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "${SCRIPT_DIR}"
MOKGAME_DIR="${SCRIPT_DIR}/mokgamedir"

UNITY_DLL=()
UNITY_DLL+=(UnityEngine.dll)
UNITY_DLL+=(UnityEngine.CoreModule.dll)
UNITY_DLL+=(UnityEngine.AnimationModule.dll)
UNITY_DLL+=(UnityEngine.AudioModule.dll)
UNITY_DLL+=(UnityEngine.IMGUIModule.dll)
UNITY_DLL+=(UnityEngine.InputLegacyModule.dll)
UNITY_DLL+=(UnityEngine.TextRenderingModule.dll)
UNITY_DLL+=(UnityEngine.UIModule.dll)
UNITY_DLL+=(UnityEngine.UnityWebRequestModule.dll)
UNITY_DLL+=(UnityEngine.UnityWebRequestAudioModule.dll)
UNITY_PKG_PATH="$HOME/.nuget/packages/unityengine.modules/2021.3.33/lib/net45"
TARGET_DIR="${MOKGAME_DIR}/Chill With You_Data/Managed"
mkdir -p "$TARGET_DIR"
mkdir -p /tmp/unity-deps
cd /tmp/unity-deps

# Create a project file to download UnityEngine.Modules
cat > UnityDeps.csproj << 'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="UnityEngine.Modules" Version="2021.3.33" />
  </ItemGroup>
</Project>
EOF
dotnet restore

for i in ${UNITY_DLL[@]}; do
  cp "$UNITY_PKG_PATH/$i" "$TARGET_DIR/"
done

echo "Successfully downloaded UnityEngine Dlls from NuGet"
