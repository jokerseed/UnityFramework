#!/usr/bin/env bash
# 命令行打表（CI / macOS / Linux）。Unity 内请用 Tools/Framework/Config/Generate Luban Config，
# 它还会按 codegen.json 校验生成代码前缀。Excel 源文件在 Datas/ 下，直接编辑后重新生成即可。
set -euo pipefail

CONF_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
WORKSPACE="$(cd "$CONF_ROOT/../.." && pwd)"
LUBAN_SRC="$WORKSPACE/ThirdParty/luban-4.11.0/src/Luban"
LUBAN_DLL="$WORKSPACE/ThirdParty/luban-4.11.0/Tools/Luban/Luban.dll"
OUTPUT_CODE="$WORKSPACE/Assets/Generated/Luban/Scripts"
OUTPUT_BIN="$WORKSPACE/Assets/Bundles/Configs"
OUTPUT_JSON="$CONF_ROOT/Output/json"
IGNORE_CODE="$CONF_ROOT/_gen_ignore"

if [ ! -f "$LUBAN_DLL" ]; then
  if [ ! -f "$LUBAN_SRC/Luban.csproj" ]; then
    echo "[ERROR] Luban source not found: $LUBAN_SRC" >&2
    exit 1
  fi
  echo "[Luban] Building CLI from ThirdParty/luban-4.11.0 ..."
  dotnet build "$LUBAN_SRC/Luban.csproj" -c Release -o "$(dirname "$LUBAN_DLL")"
fi

echo "[1/2] Generate C# code + binary (.bytes) ..."
dotnet "$LUBAN_DLL" -t client -c cs-bin -d bin --conf "$CONF_ROOT/luban.conf" \
  -x "outputCodeDir=$OUTPUT_CODE" -x "outputDataDir=$OUTPUT_BIN"

echo "[2/2] Export JSON for debug ..."
dotnet "$LUBAN_DLL" -t client -d json --conf "$CONF_ROOT/luban.conf" \
  -x "outputCodeDir=$IGNORE_CODE" -x "outputDataDir=$OUTPUT_JSON"

echo
echo "[Luban] Done."
echo "  Code : $OUTPUT_CODE"
echo "  Bin  : $OUTPUT_BIN"
echo "  JSON : $OUTPUT_JSON"
