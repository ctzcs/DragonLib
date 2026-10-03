# 校验着色器源码与已提交的产物是否一致(Build-Shaders.ps1 写的 <Name>.sha256)。不一致时抛出并提示重编译。
#   & "<DragonLib>/Tools/ShaderCompiler/Verify-Shaders.ps1" -Names WorldSdf,TowerSdf -ManifestDir <清单目录> -SourceDir <HLSL 目录> -Output <编译目录> -Rebuild '<重编译命令>'
param(
    [Parameter(Mandatory)][string[]]$Names,
    [Parameter(Mandatory)][string]$ManifestDir,
    [Parameter(Mandatory)][string]$SourceDir,
    [Parameter(Mandatory)][string]$Output,
    [string]$Rebuild = 'Build-Shaders.ps1'
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'ShaderCommon.ps1')
foreach ($name in $Names) {
    $manifest = Join-Path $ManifestDir "$name.sha256"
    if (!(Test-Path -LiteralPath $manifest)) { throw "Missing $name shader manifest. Run $Rebuild." }
    $entries = @(Get-Content -LiteralPath $manifest)
    $sources = @(Get-ShaderSources (Join-Path $SourceDir "$name.hlsl"))
    $expected = $sources.Count + $ShaderStages.Count * $ShaderFormats.Count
    if ($entries.Count -ne $expected) { throw "Incomplete $name shader manifest ($($entries.Count)/$expected). Run $Rebuild." }
    foreach ($entry in $entries) {
        $file, $hash = $entry -split ' ', 2
        $path = if ($file -match '\.hlsli?$') { Join-Path $SourceDir $file } else { Join-Path $Output $file }
        if (!(Test-Path -LiteralPath $path) -or (Get-ShaderHash $path) -ne $hash) { throw "Stale shader: $file. Run $Rebuild." }
    }
}
