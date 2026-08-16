#!/usr/bin/env bash
# Natočí záběry do traileru jako sekvence PNG. Použití:
#   ./trailer.sh            → ostrá verze 1920×1080, 60 fps
#   ./trailer.sh nahled     → náhled 960×540, 30 fps, dvě městečka
#
# Video se z toho nedělá tady: kodek by znamenal další závislost (viz „no
# balast" v CLAUDE.md). Příkaz pro ffmpeg hra na konci vypíše hotový.
#
# Na Windows je vedle tohohle trailer.cmd a dva .bat soubory na dvojklik.
set -euo pipefail
cd "$(dirname "$0")"

REZIM="${1:-ostra}"
VYSTUP="trailer"

case "$REZIM" in
    ostra)  REZIM_ARG=() ;;
    nahled) REZIM_ARG=(--nahled) ;;
    *)
        echo "Neznámý režim '$REZIM'. Použij 'ostra' nebo 'nahled'." >&2
        exit 1
        ;;
esac

# Stará sekvence se maže: ffmpeg čte číslovanou řadu a snímky z minulého
# natáčení by se do ní zamíchaly.
rm -rf "$VYSTUP"

dotnet run --project src/CivDle/CivDle.csproj -c Release -- --trailer "$VYSTUP" "${REZIM_ARG[@]}"
