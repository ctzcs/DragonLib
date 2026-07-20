#!/usr/bin/env bash
set -euo pipefail

# Default compiler path. Use a WSL path when this script runs inside WSL.
SHADERCROSS_PATH="/mnt/d/MySpace/Github/DragonLib/Tools/ShaderCross/shadercross.exe"

# A command-line argument or SHADERCROSS environment variable can override the default.
SHADERCROSS="${1:-${SHADERCROSS:-$SHADERCROSS_PATH}}"

if [[ "$SHADERCROSS" == */* ]]; then
    if [[ ! -f "$SHADERCROSS" ]]; then
        echo "shadercross does not exist: '$SHADERCROSS'" >&2
        exit 1
    fi
elif ! command -v "$SHADERCROSS" >/dev/null 2>&1; then
    echo "shadercross was not found: '$SHADERCROSS'" >&2
    echo "Usage: bash compile.sh /path/to/shadercross.exe" >&2
    exit 1
fi

IS_WSL=false
if [[ -n "${WSL_INTEROP:-}" ]] || grep -qi microsoft /proc/version 2>/dev/null; then
    IS_WSL=true
fi

run_shadercross() {
    if [[ "$IS_WSL" == true && "$SHADERCROSS" == *.exe ]]; then
        local converted=()
        local argument
        for argument in "$@"; do
            if [[ "$argument" == /* ]]; then
                converted+=("$(wslpath -w "$argument")")
            else
                converted+=("$argument")
            fi
        done
        "$SHADERCROSS" "${converted[@]}"
    else
        "$SHADERCROSS" "$@"
    fi
}

compile() {
    local input="$1"
    local stage="$2"
    local outdir="$3"

    local filename
    filename="$(basename -- "$input")"
    filename="${filename%.*}.${stage}"

    echo "Compiling '$input' ($stage) ..."

    run_shadercross "$input" \
        -e "${stage}_main" \
        -t "$stage" \
        -s HLSL \
        -o "$outdir/$filename.spv"

    run_shadercross "$outdir/$filename.spv" \
        -e "${stage}_main" \
        -t "$stage" \
        -s SPIRV \
        -o "$outdir/$filename.msl"

    run_shadercross "$outdir/$filename.spv" \
        -e "${stage}_main" \
        -t "$stage" \
        -s SPIRV \
        -o "$outdir/$filename.dxil"
}

SCRIPT_DIR="$(
    cd -- "$(dirname -- "${BASH_SOURCE[0]}")"
    pwd
)"

OUTPUT_DIR="$SCRIPT_DIR/Compiled"
mkdir -p "$OUTPUT_DIR"

shopt -s nullglob

for file in "$SCRIPT_DIR"/*.hlsl; do
    if grep -q "vertex_main" "$file"; then
        compile "$file" vertex "$OUTPUT_DIR"
    fi

    if grep -q "fragment_main" "$file"; then
        compile "$file" fragment "$OUTPUT_DIR"
    fi
done
