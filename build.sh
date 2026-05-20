#!/bin/bash
# build.sh — Build script for OpenLMStudio
# Usage:
#   ./build.sh                          # Build for default platform
#   ./build.sh -RID win-x64            # Build for Windows x64
#   ./build.sh -RID osx-arm64          # Build for macOS Apple Silicon
#   ./build.sh -RID linux-x64          # Build for Linux x64
#   ./build.sh -RID android.35-arm64-v8a  # Build for Android ARM64

set -euo pipefail

CONFIGURATION="Publish"
RID=""
PROJECT="src/Desktop/OpenLMStudio.Desktop.csproj"
CLEAN=false

# Parse arguments
while [[ $# -gt 0 ]]; do
    case "$1" in
        -RID) RID="$2"; shift 2 ;;
        -RID=*) RID="${1#*=}"; shift ;;
        -Configuration) CONFIGURATION="$2"; shift 2 ;;
        -Configuration=*) CONFIGURATION="${1#*=}"; shift ;;
        --clean) CLEAN=true; shift ;;
        *) echo "Unknown option: $1"; exit 1 ;;
    esac
done

echo "========================================"
echo "  OpenLMStudio Build Script"
echo "========================================"
echo ""

# Clean if requested
if [ "$CLEAN" = true ]; then
    echo "[Clean] Removing previous build artifacts..."
    rm -rf publish
    rm -f OpenLMStudio
    echo "[Clean] Done."
    echo ""
fi

# Determine the output directory
if [ -z "$RID" ]; then
    RID="win-x64"
fi
OUTDIR="publish/$RID"

echo "[Publish] Configuration: $CONFIGURATION"
echo "[Publish] Runtime:        $RID"
echo "[Publish] Output:         $OUTDIR"
echo ""

# Restore dependencies
echo "[Restore] Restoring packages..."
dotnet restore "$PROJECT" || exit 1
echo ""

# Publish
echo "[Publish] Building..."
dotnet publish "$PROJECT" -c "$CONFIGURATION" -r "$RID" -o "$OUTDIR" /nowarn:AVLN3001 || exit 1

# Copy the main executable to root for Windows x64
if [ "$RID" = "win-x64" ]; then
    if [ -f "$OUTDIR/OpenLMStudio.exe" ]; then
        cp "$OUTDIR/OpenLMStudio.exe" "OpenLMStudio.exe"
        echo ""
        echo "[Done] OpenLMStudio.exe copied to project root."
    fi
fi

# Show output
echo ""
echo "[Done] Build complete!"
echo "Output: $OUTDIR"