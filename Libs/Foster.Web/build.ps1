param(
    [string]$Project = "$PSScriptRoot/Samples/WebDemo/WebDemo.csproj",
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [string]$Output = "$PSScriptRoot/artifacts/web",
    [switch]$Aot
)
$ErrorActionPreference = 'Stop'
$taskProjectPath = (Resolve-Path -LiteralPath $Project).Path
$taskOutputPath = [System.IO.Path]::GetFullPath($Output)
$taskArguments = @('publish', $taskProjectPath, '-c', $Configuration, '-o', $taskOutputPath, '--nologo')
if ($Aot) { $taskArguments += '-p:RunAOTCompilation=true' }
& dotnet @taskArguments
if ($LASTEXITCODE -ne 0) { throw 'Web publish failed. Install the matching .NET wasm-tools workload if it is missing.' }
$taskIndexFiles = @(Get-ChildItem -LiteralPath $taskOutputPath -Filter index.html -Recurse)
if ($taskIndexFiles.Count -ne 1) { throw "Expected one deployable index.html under $taskOutputPath, found $($taskIndexFiles.Count)." }
Write-Host "Web root: $($taskIndexFiles[0].DirectoryName)"
Write-Host 'Serve that directory over HTTP(S); do not open index.html through file://.'
