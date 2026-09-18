#!/usr/bin/env bash
# Full model pipeline: FBX -> .glb (Blender, convert.sh) -> .dasset (DassetCompiler).
# The glb->dasset step is pure C# and does not need Blender; if convert.sh fails
# (e.g. Blender missing) we still cook whatever .glb files exist.
# Usage: ./cook.sh [path/to/blender]
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MODELS_DIR="$SCRIPT_DIR/../../Tests/Game0/Resources/Models"

if ! "$SCRIPT_DIR/convert.sh" ${1:+"$1"}; then
    echo "convert.sh failed; continuing with glb -> dasset anyway" >&2
fi

dotnet run --project "$SCRIPT_DIR/Program/DassetCompiler.csproj" -c Release -- --scan "$MODELS_DIR"
