@echo off
setlocal EnableExtensions

set "WORKSPACE=%~dp0..\.."
set "CONF_ROOT=%~dp0"
set "LUBAN_SRC=%WORKSPACE%\ThirdParty\luban-4.11.0\src\Luban"
set "LUBAN_DLL=%WORKSPACE%\ThirdParty\luban-4.11.0\Tools\Luban\Luban.dll"
set "OUTPUT_CODE=%WORKSPACE%\Assets\Generated\Luban\Scripts"
set "OUTPUT_BIN=%WORKSPACE%\Assets\Bundles\Configs"
set "OUTPUT_JSON=%CONF_ROOT%Output\json"
set "IGNORE_CODE=%CONF_ROOT%_gen_ignore"

if not exist "%LUBAN_SRC%\Luban.csproj" (
    echo [ERROR] Luban source not found: %LUBAN_SRC%
    echo Please place luban source at ThirdParty\luban-4.11.0
    exit /b 1
)

if not exist "%LUBAN_DLL%" (
    echo [Luban] Building CLI from ThirdParty\luban-4.11.0 ...
    dotnet build "%LUBAN_SRC%\Luban.csproj" -c Release -o "%WORKSPACE%\ThirdParty\luban-4.11.0\Tools\Luban"
    if errorlevel 1 (
        echo [ERROR] Failed to build Luban.dll. Requires .NET 8 SDK.
        exit /b 1
    )
)

echo [0/3] Ensure Excel source files exist...
python "%CONF_ROOT%create_excel.py"
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] create_excel.py failed. Install Python + openpyxl.
    exit /b %ERRORLEVEL%
)

echo [1/2] Generate C# code + binary (.bytes) ...
dotnet "%LUBAN_DLL%" ^
    -t client ^
    -c cs-bin ^
    -d bin ^
    --conf "%CONF_ROOT%luban.conf" ^
    -x "outputCodeDir=%OUTPUT_CODE%" ^
    -x "outputDataDir=%OUTPUT_BIN%"
if errorlevel 1 exit /b 1

echo [1b] Validate generated code file prefix (Config/Luban/codegen.json) ...
python "%CONF_ROOT%validate_codegen_prefix.py"
if errorlevel 1 exit /b 1

echo [2/2] Export JSON for debug ...
dotnet "%LUBAN_DLL%" ^
    -t client ^
    -d json ^
    --conf "%CONF_ROOT%luban.conf" ^
    -x "outputCodeDir=%IGNORE_CODE%" ^
    -x "outputDataDir=%OUTPUT_JSON%"
if errorlevel 1 exit /b 1

echo.
echo [Luban] Done.
echo   Code   : %OUTPUT_CODE%
echo   Bin    : %OUTPUT_BIN%
echo   JSON   : %OUTPUT_JSON%
echo   Prefix : see Config/Luban/codegen.json

endlocal
exit /b 0
