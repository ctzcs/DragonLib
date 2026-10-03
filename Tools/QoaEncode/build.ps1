# Builds qoaenc.exe (checked in so Web builds need no C compiler). Requires gcc (e.g. w64devkit) on PATH.
$ErrorActionPreference = 'Stop'
$include = Join-Path $PSScriptRoot '../../Libs/Foster.Audio/Platform/src/third_party'
& gcc -O2 -std=c99 -I $include -o (Join-Path $PSScriptRoot 'qoaenc.exe') (Join-Path $PSScriptRoot 'qoaenc.c') -static -s
if ($LASTEXITCODE -ne 0) { throw 'qoaenc build failed.' }
