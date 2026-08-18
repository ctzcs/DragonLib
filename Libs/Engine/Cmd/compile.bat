@echo off
setlocal EnableExtensions DisableDelayedExpansion

rem Compile all HLSL shaders in this directory to SPIR-V, MSL, and DXIL.
rem Usage: compile.bat [path\to\shadercross.exe]

for %%I in ("%~dp0..\..\..\Tools\ShaderCross\shadercross.exe") do set "DEFAULT_SHADERCROSS=%%~fI"

if not "%~1"=="" (
    set "SHADERCROSS=%~1"
) else if defined SHADERCROSS (
    rem Keep the SHADERCROSS environment variable.
) else (
    set "SHADERCROSS=%DEFAULT_SHADERCROSS%"
)

if exist "%SHADERCROSS%" goto :shadercross_ok
where "%SHADERCROSS%" >nul 2>&1
if not errorlevel 1 goto :shadercross_ok

    echo shadercross was not found: "%SHADERCROSS%" 1>&2
    echo Usage: compile.bat [path\to\shadercross.exe] 1>&2
    exit /b 1

:shadercross_ok

set "SCRIPT_DIR=%~dp0"
set "OUTPUT_DIR=%SCRIPT_DIR%Compiled"
if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"
if errorlevel 1 exit /b 1

if not exist "%SCRIPT_DIR%*.hlsl" (
    echo No HLSL files found in "%SCRIPT_DIR%".
    exit /b 0
)

for %%F in ("%SCRIPT_DIR%*.hlsl") do (
    findstr /L /C:"vertex_main" "%%~fF" >nul
    if not errorlevel 1 call :compile "%%~fF" vertex
    if errorlevel 1 exit /b 1

    findstr /L /C:"fragment_main" "%%~fF" >nul
    if not errorlevel 1 call :compile "%%~fF" fragment
    if errorlevel 1 exit /b 1
)

echo Shader compilation completed.
exit /b 0

:compile
setlocal
set "INPUT=%~1"
set "STAGE=%~2"
set "NAME=%~n1.%~2"

echo Compiling "%INPUT%" (%STAGE%) ...

"%SHADERCROSS%" "%INPUT%" -e "%STAGE%_main" -t "%STAGE%" -s HLSL -o "%OUTPUT_DIR%\%NAME%.spv"
if errorlevel 1 exit /b 1

"%SHADERCROSS%" "%OUTPUT_DIR%\%NAME%.spv" -e "%STAGE%_main" -t "%STAGE%" -s SPIRV -o "%OUTPUT_DIR%\%NAME%.msl"
if errorlevel 1 exit /b 1

"%SHADERCROSS%" "%OUTPUT_DIR%\%NAME%.spv" -e "%STAGE%_main" -t "%STAGE%" -s SPIRV -o "%OUTPUT_DIR%\%NAME%.dxil"
if errorlevel 1 exit /b 1

endlocal
exit /b 0
