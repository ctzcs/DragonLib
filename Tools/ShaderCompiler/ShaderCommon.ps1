# Build-Shaders.ps1 与 Verify-Shaders.ps1 共用：产物格式与哈希清单格式。
$ShaderStages = @('vertex', 'fragment')
$ShadercrossFormats = @('dxil', 'spv', 'msl')
$ShaderFormats = $ShadercrossFormats + @('glsl')

function Get-ShaderHash([string]$Path) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($sha.ComputeHash([System.IO.File]::ReadAllBytes($Path))).Replace('-', '') }
    finally { $sha.Dispose() }
}

# include 的变化也会改变编译结果，清单必须递归记录依赖，不能只锁住入口文件。
function Get-ShaderSources([string]$Source) {
    $pending = [System.Collections.Generic.Stack[string]]::new()
    $seen = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $pending.Push((Resolve-Path -LiteralPath $Source).Path)
    while ($pending.Count -gt 0) {
        $path = $pending.Pop()
        if (!$seen.Add($path)) { continue }
        $path
        foreach ($line in Get-Content -LiteralPath $path) {
            if ($line -match '^\s*#include\s+"([^"]+)"') {
                $pending.Push((Resolve-Path -LiteralPath (Join-Path (Split-Path $path) $Matches[1])).Path)
            }
        }
    }
}

# 清单：第一行 HLSL 源，其后按文件名排序的全部产物；每行 "<文件名> <SHA256>"。
function Write-ShaderManifest([string]$Name, [string]$Source, [string]$Output, [string]$ManifestDir) {
    $compiled = foreach ($stage in $ShaderStages) { foreach ($format in $ShaderFormats) { Join-Path $Output "$Name.$stage.$format" } }
    $lines = @(foreach ($file in @(Get-ShaderSources $Source)) {
        $relative = [System.IO.Path]::GetRelativePath((Split-Path $Source), $file).Replace('\', '/')
        "$relative $(Get-ShaderHash $file)"
    })
    $lines += foreach ($file in @($compiled | Sort-Object { Split-Path $_ -Leaf })) {
        "$(Split-Path $file -Leaf) $(Get-ShaderHash $file)"
    }
    $lines | Set-Content -LiteralPath (Join-Path $ManifestDir "$Name.sha256") -Encoding ASCII
}
