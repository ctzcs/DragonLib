@echo off
setlocal EnableExtensions DisableDelayedExpansion

rem Convert every FBX under Tests\Game0\Resources\Models into a .glb next to it
rem (reconverted only when the .glb is missing or older than the .fbx).
rem The engine loads .glb/.gltf only; this is the supported path for FBX assets.
rem Usage: convert.bat [path\to\blender.exe]
rem Blender resolution: %1 > BLENDER env > PATH > Program Files\Blender Foundation\*\blender.exe
rem NOTE: keep this file ASCII-only; cmd parses it with the OEM codepage.

set "SCRIPT_DIR=%~dp0"
set "MODELS_DIR=%SCRIPT_DIR%..\..\Tests\Game0\Resources\Models"

if not "%~1"=="" (
    set "BLENDER_EXE=%~1"
) else if defined BLENDER (
    set "BLENDER_EXE=%BLENDER%"
) else (
    set "BLENDER_EXE="
    where blender >nul 2>&1
    if not errorlevel 1 set "BLENDER_EXE=blender"
)

if not defined BLENDER_EXE (
    for /d %%D in ("%ProgramFiles%\Blender Foundation\*") do (
        if exist "%%D\blender.exe" if not defined BLENDER_EXE set "BLENDER_EXE=%%D\blender.exe"
    )
)

if not defined BLENDER_EXE (
    echo Blender was not found. Install Blender ^(3.6+^), add it to PATH, set the BLENDER 1>&2
    echo environment variable, or pass the exe path: 1>&2
    echo   convert.bat "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe" 1>&2
    exit /b 1
)

if not exist "%MODELS_DIR%" (
    echo Models directory not found: "%MODELS_DIR%" 1>&2
    exit /b 1
)

set "CONVERTED=0"
set "SKIPPED=0"
for /r "%MODELS_DIR%" %%F in (*.fbx) do call :convert "%%F"
echo Done. Converted %CONVERTED%, up-to-date %SKIPPED%.
exit /b 0

:convert
setlocal
set "FBX=%~1"
set "GLB=%~dpn1.glb"

if exist "%GLB%" (
    rem %%~t timestamps have minute precision; lexicographic compare works
    rem for the same locale format, which is good enough for an asset tool.
    for %%G in ("%GLB%") do if "%%~tG" GEQ "%~t1" (
        echo Up-to-date "%GLB%"
        endlocal & set /a SKIPPED+=1 >nul
        exit /b 0
    )
)

echo Converting "%FBX%" ...
"%BLENDER_EXE%" --background --factory-startup --python "%SCRIPT_DIR%fbx_to_gltf.py" -- "%FBX%" "%GLB%"
if errorlevel 1 (
    echo Conversion failed for "%FBX%" 1>&2
    endlocal & exit /b 1
)
if not exist "%GLB%" (
    echo Blender reported success but no .glb was produced for "%FBX%" 1>&2
    endlocal & exit /b 1
)
endlocal & set /a CONVERTED+=1 >nul
exit /b 0
