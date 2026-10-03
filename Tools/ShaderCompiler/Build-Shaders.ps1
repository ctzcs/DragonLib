# DragonLib 统一着色器构建：一份 HLSL 生成全部后端，并可写哈希清单供 Verify-Shaders.ps1 校验。
#   dxil / spv / msl —— SDL shadercross(桌面 D3D12 / Vulkan / Metal)
#   glsl             —— 由 spv 经 spirv-cross 转出(Foster.Web 的 WebGL2，约定见 spv-to-glsl.ps1)
# 用法(在调用方脚本里):
#   & "<DragonLib>/Tools/ShaderCompiler/Build-Shaders.ps1" -Output <编译目录> -ManifestDir <清单目录> -Shaders @(
#       @{ Name = 'WorldSdf'; Source = '<路径>/WorldSdf.hlsl'; Vertex = 'vertex_main'; Fragment = 'fragment_main' })
# 产物命名 <Name>.<vertex|fragment>.<格式>；同一 HLSL 可用不同 Name/入口生成多组。
# 需要 spirv-cross(Vulkan SDK，自动从 PATH 或 VULKAN_SDK 查找，或传 -SpirvCross)。
param(
    [Parameter(Mandatory)][object[]]$Shaders,
    [Parameter(Mandatory)][string]$Output,
    # 给出时为每组着色器写 <Name>.sha256：HLSL 源与全部产物的哈希。
    [string]$ManifestDir,
    [string]$Compiler = (Join-Path $PSScriptRoot '../ShaderCross/shadercross.exe'),
    [string]$SpirvCross
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ShaderCommon.ps1')
if (!(Test-Path -LiteralPath $Compiler)) { throw "SDL shadercross not found: $Compiler. Pass -Compiler <path>." }
New-Item -ItemType Directory -Path $Output -Force | Out-Null
$spvToGlsl = Join-Path $PSScriptRoot 'spv-to-glsl.ps1'
foreach ($shader in $Shaders) {
    $name = $shader.Name; $source = (Resolve-Path -LiteralPath $shader.Source).Path
    foreach ($stage in $ShaderStages) {
        $entry = if ($stage -eq 'vertex') { $shader.Vertex } else { $shader.Fragment }
        foreach ($format in $ShadercrossFormats) {
            $target = Join-Path $Output "$name.$stage.$format"
            & $Compiler $source -s HLSL -t $stage -e $entry -o $target
            if ($LASTEXITCODE -ne 0) { throw "Shader compilation failed: $name.$stage.$format" }
            if ($format -eq 'msl') {
                $code = [System.IO.File]::ReadAllText($target).Replace("`r`n", "`n").TrimEnd() + "`n"
                [System.IO.File]::WriteAllText($target, $code, [System.Text.UTF8Encoding]::new($false))
            }
        }
        $glslArgs = @{ Spv = (Join-Path $Output "$name.$stage.spv"); Output = (Join-Path $Output "$name.$stage.glsl") }
        if ($SpirvCross) { $glslArgs.SpirvCross = $SpirvCross }
        & $spvToGlsl @glslArgs
    }
    if ($ManifestDir) { Write-ShaderManifest -Name $name -Source $source -Output $Output -ManifestDir $ManifestDir }
}
