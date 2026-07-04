cd /d %~dp0
set WORKSPACE=..
set PROJECT_DIR=%WORKSPACE%\..\..\Tests\Game0\
set LUBAN_DLL=%WORKSPACE%\Tools\Luban\Luban.dll
set CONF_ROOT=.
set JSON_DATA_DIR=%CONF_ROOT%\output
set CODE_DIR=%PROJECT_DIR%\Content\Luban\GenCode

dotnet %LUBAN_DLL% ^
    -t client ^
    -c cs-dotnet-json ^
    -d json ^
    --conf %CONF_ROOT%\luban.conf ^
    -x outputDataDir=%JSON_DATA_DIR% ^
    -x outputCodeDir=%CODE_DIR% 
pause