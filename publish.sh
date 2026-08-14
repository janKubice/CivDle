#!/usr/bin/env bash
# Vytvoří distribuci hry jako jedno spustitelné exe (self-contained, bez nutnosti
# instalovat .NET). Použití:
#   ./publish.sh                 → Windows x64, plná hra (výchozí)
#   ./publish.sh linux-x64       → Linux x64, plná hra
#   ./publish.sh osx-arm64       → macOS Apple Silicon, plná hra
#   ./publish.sh win-x64 demo    → Windows x64, DEMOVERZE
#
# Grafický backend se řídí cílem, ne strojem: win-* dostane DirectX, ostatní
# OpenGL (viz UseDirectX v src/CivDle/CivDle.csproj). Windows verze jde přeložit
# i odsud, ale SPUSTIT a otestovat se musí na Windows.
#
# DEMO je samostatný build, ne přepínač za běhu: o edici rozhoduje překladová
# konstanta (viz src/CivDle/Edition.cs), takže se plná hra nemůže omylem tvářit
# jako demo ani naopak. Demo jde do dist/<rid>-demo, aby si obě distribuce
# nepřepsaly složku.
set -euo pipefail
cd "$(dirname "$0")"

RID="${1:-win-x64}"
EDITION="${2:-full}"

case "$EDITION" in
  full)
    OUT="dist/$RID"
    EDITION_ARGS=()
    ;;
  demo)
    OUT="dist/$RID-demo"
    EDITION_ARGS=(-p:GameEdition=Demo)
    ;;
  *)
    echo "Neznámá edice '$EDITION'. Použij 'full' nebo 'demo'." >&2
    exit 1
    ;;
esac

# Složka se maže: po přepnutí edice by v ní jinak zůstaly soubory z minulého
# buildu a nikdo by nepoznal, která verze se vlastně veze.
rm -rf "$OUT"

dotnet publish src/CivDle/CivDle.csproj \
  -c Release \
  -r "$RID" \
  --self-contained \
  -p:PublishSingleFile=true \
  "${EDITION_ARGS[@]}" \
  -o "$OUT"

echo
if [ "$EDITION" = "demo" ]; then
  echo "Hotovo → $OUT (DEMOVERZE: strop obyvatel, oříznutý strom výzkumu,"
  echo "         bez modů, achievementů a žebříčků)"
else
  echo "Hotovo → $OUT (plná hra: spustitelný soubor CivDle + složka data/)"
fi
