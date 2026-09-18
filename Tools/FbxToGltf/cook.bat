@echo off
setlocal EnableExtensions DisableDelayedExpansion

rem Full model pipeline: FBX -> .glb (Blender, convert.bat) -> .dasset (DassetCompiler).
rem The glb->dasset step is pure C# and does not need Blender; if convert.bat fails
rem (e.g. Blender missing) we still cook whatever .glb files exist.
rem Usage: cook.bat [path\to\blender.exe]
rem NOTE: keep this file ASCII-only; cmd parses it with the OEM codepage.

set "SCRIPT_DIR=%~dp0"
set "MODELS_DIR=%SCRIPT_DIR%..\..\Tests\Game0\Resources\Models"

call "%SCRIPT_DIR%convert.bat" "%~1"
if errorlevel 1 (
    echo convert.bat failed; continuing with glb -^> dasset anyway 1>&2
)

dotnet run --project "%SCRIPT_DIR%Program\DassetCompiler.csproj" -c Release -- --scan "%MODELS_DIR%"
if errorlevel 1 (
    echo dasset compile failed 1>&2
    exit /b 1
)
exit /b 0
