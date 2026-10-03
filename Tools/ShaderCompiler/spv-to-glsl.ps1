# 把 SDL shadercross 产出的 SPIR-V 转成 Foster.Web 可用的 GLSL ES 3.00。
# 用法: spv-to-glsl.ps1 -Spv X.vertex.spv -Output X.vertex.glsl [-SpirvCross <spirv-cross.exe>]
# 绑定约定见 ../README.md「着色器」:
#   SDL GPU 资源集 0/1 = 顶点 sampler/uniform，2/3 = 片元 sampler/uniform，binding 即槽位；
#   uniform block 改名为 VertexUniformN/FragmentUniformN，合并采样器改名为 u_vertex_texN/u_fragment_texN；
#   阶段间变量按 location 改名为 v_locN(GLSL ES 3.00 按名字链接)；顶点着色器末尾乘 u_target_flip。
#   SDL GPU 默认 depth clamp(不按深度裁剪)，WebGL 没有；顶点末尾把 z 夹到 [0, w] 再映射到 GL 的 [-w, w]，
#   否则 CreateOrthographicOffCenter(…, 0.1, 1000) 下 z=0 的图元(clip z 略小于 0)会被整个裁掉。
param(
    [Parameter(Mandatory)][string]$Spv,
    [Parameter(Mandatory)][string]$Output,
    [string]$SpirvCross
)
$ErrorActionPreference = 'Stop'
if (!$SpirvCross) {
    $command = Get-Command spirv-cross -ErrorAction SilentlyContinue
    $SpirvCross = if ($command) { $command.Source } elseif ($env:VULKAN_SDK) { Join-Path $env:VULKAN_SDK 'Bin/spirv-cross.exe' } else { '' }
}
if (!$SpirvCross -or !(Test-Path -LiteralPath $SpirvCross)) { throw 'spirv-cross not found. Install the Vulkan SDK or pass -SpirvCross <path>.' }

$reflection = (& $SpirvCross $Spv --reflect) -join "`n" | ConvertFrom-Json
if ($LASTEXITCODE -ne 0) { throw "spirv-cross reflection failed: $Spv" }
$mode = $reflection.entryPoints[0].mode
$isVertex = switch ($mode) { 'vert' { $true } 'frag' { $false } default { throw "Unsupported shader stage '$mode': $Spv" } }
$stageName = if ($isVertex) { 'vertex' } else { 'fragment' }

$arguments = @($Spv, '--es', '--version', '300')
$varyings = if ($isVertex) { @($reflection.outputs) } else { @($reflection.inputs) }
$direction = if ($isVertex) { 'out' } else { 'in' }
foreach ($varying in $varyings) {
    if ($varying) { $arguments += @('--rename-interface-variable', $direction, $varying.location, "v_loc$($varying.location)") }
}
$code = (& $SpirvCross @arguments) -join "`n"
if ($LASTEXITCODE -ne 0) { throw "spirv-cross failed: $Spv" }

foreach ($ubo in @($reflection.ubos)) {
    if (!$ubo) { continue }
    $prefix = if ($ubo.set -eq 1) { 'VertexUniform' } elseif ($ubo.set -eq 3) { 'FragmentUniform' } else { throw "Uniform '$($ubo.name)' uses set $($ubo.set); expected 1 or 3." }
    $blockName = $ubo.name -replace '\.', '_'
    $code = [regex]::Replace($code, "\buniform\s+$([regex]::Escape($blockName))\b", "uniform $prefix$($ubo.binding)")
}
$images = @($reflection.separate_images) + @($reflection.textures) | Where-Object { $_ }
foreach ($image in $images) {
    $samplerName = "u_${stageName}_tex$($image.binding)"
    # 分离的 Texture2D + SamplerState 被合并成 SPIRV_Cross_Combined<Image><Sampler>。
    $code = [regex]::Replace($code, "\bSPIRV_Cross_Combined$([regex]::Escape($image.name))\w*", $samplerName)
    $code = [regex]::Replace($code, "\buniform\s+(highp\s+|mediump\s+|lowp\s+)?sampler2D\s+$([regex]::Escape($image.name))\b", "uniform `$1sampler2D $samplerName")
}
if ($code -match 'SPIRV_Cross_Combined') { throw "Unmapped combined sampler remains in $Spv." }
# 同一纹理既被采样又被 Load/GetDimensions 时会多出一个 DummySampler 组合，改名后声明重复，只保留一份。
$seenSamplers = @{}
$code = (($code -split "`n") | Where-Object {
    if ($_ -notmatch '^uniform\s.*\bsampler2D\s+(u_\w+);') { return $true }
    if ($seenSamplers.ContainsKey($Matches[1])) { return $false }
    $seenSamplers[$Matches[1]] = $true; return $true
}) -join "`n"
$code = $code -replace 'precision mediump float;', 'precision highp float;'
if ($isVertex) {
    $end = $code.LastIndexOf('}')
    $epilogue = "    gl_Position.z = 2.0 * clamp(gl_Position.z, 0.0, gl_Position.w) - gl_Position.w;`n    gl_Position.y *= u_target_flip;`n"
    $code = $code.Substring(0, $end) + $epilogue + $code.Substring($end)
    $code = $code -replace '(#version 300 es\n)', "`$1uniform float u_target_flip;`n"
}
[System.IO.File]::WriteAllText([System.IO.Path]::GetFullPath($Output), $code.TrimEnd() + "`n", [System.Text.UTF8Encoding]::new($false))
