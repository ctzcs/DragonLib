#!/usr/bin/env bash
# Convert every FBX under Tests/Game0/Resources Models into a .glb next to it
# (converted only when the .glb is missing or older than the .fbx).
# The engine loads .glb/.gltf only; this is the supported path for FBX assets.
# Usage: ./convert.sh [path/to/blender]
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MODELS_DIR="$SCRIPT_DIR/../../Tests/Game0/Resources/Models"

if [[ $# -gt 0 ]]; then
    BLENDER_EXE="$1"
elif [[ -n "${BLENDER:-}" ]]; then
    BLENDER_EXE="$BLENDER"
elif command -v blender >/dev/null 2>&1; then
    BLENDER_EXE="blender"
else
    echo "Blender was not found. Install Blender (3.6+), add it to PATH, set BLENDER, or pass the exe path:" >&2
    echo "  ./convert.sh /path/to/blender" >&2
    exit 1
fi

if [[ ! -d "$MODELS_DIR" ]]; then
    echo "Models directory not found: $MODELS_DIR" >&2
    exit 1
fi

converted=0
skipped=0
while IFS= read -r -d '' fbx; do
    glb="${fbx%.fbx}.glb"
    if [[ -f "$glb" && "$glb" -nt "$fbx" ]]; then
        skipped=$((skipped + 1))
        continue
    fi

    echo "Converting \"$fbx\" ..."
    "$BLENDER_EXE" --background --factory-startup --python "$SCRIPT_DIR/fbx_to_gltf.py" -- "$fbx" "$glb"
    if [[ ! -f "$glb" ]]; then
        echo "Blender reported success but no .glb was produced for \"$fbx\"" >&2
        exit 1
    fi
    converted=$((converted + 1))
done < <(find "$MODELS_DIR" -type f -name '*.fbx' -print0)

echo "Done. Converted $converted, up-to-date $skipped."
