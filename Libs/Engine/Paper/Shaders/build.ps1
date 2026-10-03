# Paper UI(Quill 画布)着色器：生成 dxil/spv/msl/glsl 与哈希清单(实现见 Tools/ShaderCompiler)。
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot '../../../../Tools/ShaderCompiler/Build-Shaders.ps1') -Output (Join-Path $PSScriptRoot 'Compiled') -ManifestDir $PSScriptRoot -Shaders @(
    @{ Name = 'QuillCanvas'; Source = (Join-Path $PSScriptRoot 'QuillCanvas.hlsl'); Vertex = 'vertex_main'; Fragment = 'fragment_main' })
