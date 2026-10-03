# 相对 #include 已由最小 shadercross 测试确认支持；公共文件也纳入哈希清单。
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot '../../../../Tools/ShaderCompiler/Build-Shaders.ps1') -Output (Join-Path $PSScriptRoot 'Compiled') -ManifestDir $PSScriptRoot -Shaders @(
    foreach ($name in @('Standard3D', 'Standard3DSkinned', 'DepthOnly', 'DepthOnlySkinned')) {
        @{ Name = $name; Source = (Join-Path $PSScriptRoot "$name.hlsl"); Vertex = 'vertex_main'; Fragment = 'fragment_main' }
    })
