#!/usr/bin/env sh
set -eu
ROOT=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
PROJECT=${1:-"$ROOT/Samples/WebDemo/WebDemo.csproj"}
OUTPUT=${2:-"$ROOT/artifacts/web"}
dotnet publish "$PROJECT" -c Release -o "$OUTPUT" --nologo
echo "Published to $OUTPUT; serve the directory containing index.html over HTTP(S)."
