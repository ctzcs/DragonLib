$ErrorActionPreference = 'Stop'
$compiler = Join-Path $PSScriptRoot '../../../../Tools/ShaderCross/shadercross.exe'
foreach ($stage in @('vertex', 'fragment')) {
    foreach ($format in @('dxil', 'spv', 'msl')) {
        & $compiler (Join-Path $PSScriptRoot 'QuillCanvas.hlsl') -s HLSL -t $stage -e "${stage}_main" -o (Join-Path $PSScriptRoot "Compiled/QuillCanvas.$stage.$format")
        if ($LASTEXITCODE -ne 0) { throw "Shader compilation failed: $stage.$format" }
    }
    # Foster.Web (WebGL2): GLSL ES 3.00 from the SPIR-V output; needs spirv-cross (Vulkan SDK).
    & (Join-Path $PSScriptRoot '../../../Foster.Web/Tools/spv-to-glsl.ps1') -Spv (Join-Path $PSScriptRoot "Compiled/QuillCanvas.$stage.spv") -Output (Join-Path $PSScriptRoot "Compiled/QuillCanvas.$stage.glsl")
}
