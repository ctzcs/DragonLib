$ErrorActionPreference = 'Stop'
$compiler = Join-Path $PSScriptRoot '../../../../Tools/ShaderCross/shadercross.exe'
foreach ($stage in @('vertex', 'fragment')) {
    foreach ($format in @('dxil', 'spv', 'msl')) {
        & $compiler (Join-Path $PSScriptRoot 'QuillCanvas.hlsl') -s HLSL -t $stage -e "${stage}_main" -o (Join-Path $PSScriptRoot "Compiled/QuillCanvas.$stage.$format")
        if ($LASTEXITCODE -ne 0) { throw "Shader compilation failed: $stage.$format" }
    }
}
