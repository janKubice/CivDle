#!/usr/bin/env bash
# Vytvoří distribuci hry jako jedno spustitelné exe (self-contained, bez nutnosti
# instalovat .NET). Použití:
#   ./publish.sh            → Windows x64 (výchozí)
#   ./publish.sh linux-x64  → Linux x64
#   ./publish.sh osx-arm64  → macOS Apple Silicon
#
# Grafický backend se řídí cílem, ne strojem: win-* dostane DirectX, ostatní
# OpenGL (viz UseDirectX v src/CivDle/CivDle.csproj). Windows verze jde přeložit
# i odsud, ale SPUSTIT a otestovat se musí na Windows.
set -euo pipefail
cd "$(dirname "$0")"

RID="${1:-win-x64}"
OUT="dist/$RID"

dotnet publish src/CivDle/CivDle.csproj \
  -c Release \
  -r "$RID" \
  --self-contained \
  -p:PublishSingleFile=true \
  -o "$OUT"

echo
echo "Hotovo → $OUT (spustitelný soubor CivDle + složka data/)"
