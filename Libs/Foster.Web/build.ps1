param(
    [string]$Project = "$PSScriptRoot/Samples/WebDemo/WebDemo.csproj",
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [string]$Output = "$PSScriptRoot/artifacts/web",
    [switch]$Aot
)
$ErrorActionPreference = 'Stop'
$taskProjectPath = (Resolve-Path -LiteralPath $Project).Path
$taskOutputPath = [System.IO.Path]::GetFullPath($Output)
# Publish never prunes its output, so files from earlier publishes (hashed runtime files, renamed assets) pile up.
# Clear generated site folders only: a wwwroot that contains _framework.
Get-ChildItem -LiteralPath $taskOutputPath -Directory -Recurse -Filter wwwroot -ErrorAction SilentlyContinue |
    Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName '_framework') } |
    ForEach-Object { Remove-Item -LiteralPath $_.FullName -Recurse -Force }
$taskArguments = @('publish', $taskProjectPath, '-c', $Configuration, '-o', $taskOutputPath, '--nologo')
# AOT and interpreter publishes must not share intermediates: the incremental native link does not notice the
# switch and reuses objects from the other mode, and the page then fails while loading the runtime.
if ($Aot) { $taskArguments += @('-p:RunAOTCompilation=true', "-p:IntermediateOutputPath=obj/$Configuration-aot/") }
& dotnet @taskArguments
if ($LASTEXITCODE -ne 0) { throw 'Web publish failed. Install the matching .NET wasm-tools workload if it is missing.' }
$taskIndexFiles = @(Get-ChildItem -LiteralPath $taskOutputPath -Filter index.html -Recurse)
if ($taskIndexFiles.Count -ne 1) { throw "Expected one deployable index.html under $taskOutputPath, found $($taskIndexFiles.Count)." }
Write-Host "Web root: $($taskIndexFiles[0].DirectoryName)"
Write-Host 'Serve that directory over HTTP(S); do not open index.html through file://.'
