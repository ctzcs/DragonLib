param([string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$buildDir = Join-Path $PSScriptRoot '../../../.codex-build/msdfgen-native'
cmake -S $PSScriptRoot -B $buildDir -A x64
if ($LASTEXITCODE -ne 0) { throw 'MSDF CMake configuration failed.' }
cmake --build $buildDir --config $Configuration --parallel
if ($LASTEXITCODE -ne 0) { throw 'MSDF native build failed.' }
$destination = Join-Path $PSScriptRoot 'runtimes/win-x64/native'
New-Item -ItemType Directory -Force $destination | Out-Null
Copy-Item -LiteralPath (Join-Path $buildDir "$Configuration/DragonLib.Msdfgen.dll") -Destination $destination
