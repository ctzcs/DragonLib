# Build-Shaders.ps1 与 Verify-Shaders.ps1 共用：产物格式与哈希清单格式。
$ShaderStages = @('vertex', 'fragment')
$ShadercrossFormats = @('dxil', 'spv', 'msl')
$ShaderFormats = $ShadercrossFormats + @('glsl')

function Get-ShaderHash([string]$Path) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($sha.ComputeHash([System.IO.File]::ReadAllBytes($Path))).Replace('-', '') }
    finally { $sha.Dispose() }
}

# 清单：第一行 HLSL 源，其后按文件名排序的全部产物；每行 "<文件名> <SHA256>"。
function Write-ShaderManifest([string]$Name, [string]$Source, [string]$Output, [string]$ManifestDir) {
    $compiled = foreach ($stage in $ShaderStages) { foreach ($format in $ShaderFormats) { Join-Path $Output "$Name.$stage.$format" } }
    $files = @($Source) + @($compiled | Sort-Object { Split-Path $_ -Leaf })
    $lines = foreach ($file in $files) { "$(Split-Path $file -Leaf) $(Get-ShaderHash $file)" }
    $lines | Set-Content -LiteralPath (Join-Path $ManifestDir "$Name.sha256") -Encoding ASCII
}
