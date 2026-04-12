#!/usr/bin/env bash
# Creates publicized stub assemblies from KSP managed DLLs for CI builds.
# Usage: ./scripts/create-stubs.sh [KSP_MANAGED_DIR]
#
# Default KSP managed dir (macOS Steam):
#   ~/Library/Application Support/Steam/steamapps/common/Kerbal Space Program/KSP.app/Contents/Resources/Data/Managed

set -euo pipefail

DEFAULT_MANAGED="$HOME/Library/Application Support/Steam/steamapps/common/Kerbal Space Program/KSP.app/Contents/Resources/Data/Managed"
MANAGED_DIR="${1:-$DEFAULT_MANAGED}"
OUTPUT_DIR="$(cd "$(dirname "$0")/.." && pwd)/libs/managed"

DLLS=(
  Assembly-CSharp.dll
  UnityEngine.dll
  UnityEngine.CoreModule.dll
  UnityEngine.UI.dll
  UnityEngine.UIModule.dll
  UnityEngine.TextRenderingModule.dll
  UnityEngine.ImageConversionModule.dll
  UnityEngine.IMGUIModule.dll
  UnityEngine.InputLegacyModule.dll
  UnityEngine.AnimationModule.dll
  UnityEngine.UnityWebRequestModule.dll
)

if [ ! -d "$MANAGED_DIR" ]; then
  echo "Error: KSP Managed directory not found: $MANAGED_DIR"
  echo "Usage: $0 [KSP_MANAGED_DIR]"
  exit 1
fi

mkdir -p "$OUTPUT_DIR"

echo "Copying reference assemblies from: $MANAGED_DIR"
for dll in "${DLLS[@]}"; do
  src="$MANAGED_DIR/$dll"
  if [ -f "$src" ]; then
    cp "$src" "$OUTPUT_DIR/$dll"
    echo "  Copied $dll"
  else
    echo "  Warning: $dll not found, skipping"
  fi
done

echo ""
echo "Reference assemblies copied to: $OUTPUT_DIR"
echo "The libs/ directory structure expected by CI:"
echo "  libs/managed/*.dll"
echo "  (KSPRoot is set to libs/, ManagedDir resolves to libs/managed/)"
echo ""
echo "Note: Do NOT commit these DLLs to the repository."
echo "Upload them to the GitHub Actions cache by running CI once after populating libs/."
