#!/usr/bin/env bash
# Sestřih teaseru z vyrenderovaných sekvencí.
#
# Hra vyrábí 37 s záběrů (9 s přehlídka spritů + 4x 7 s město). Na teaser do
# obchodu je to moc dlouhé a přehlídka spritů není hák — divák má v prvních
# vteřinách vidět město, ne katalog. Střih proto bere jen městské záběry,
# zkracuje je a na konec dává značku.
set -euo pipefail

# Cesty: skript žije v tools/, ale pracuje nad složkou, kterou dostane.
#
#   tools/make_teaser.sh <složka-se-sekvencemi>
#
# Ve složce se čekají podsložky, jak je vyrábí hra (CivDle --trailer <složka>):
# 01-prehlidka, 02-mesto, 03-mesto, … a soubor endcard.png se značkou.
S="${1:-$(dirname "$0")}"
T="$S/trailer"
OUT="$S/civdle-teaser.mp4"
SEG="$S/seg"
rm -rf "$SEG"; mkdir -p "$SEG"

FPS=60
CITY_SECONDS=5.4     # z každých sedmi vteřin města se vezme nejlepší kus
SKIP_FRAMES=42       # prvních 0,7 s je nájezd z černé — ten si udělá střih sám
END_SECONDS=4.2

i=0
for dir in "$T"/0[2-9]-*/; do
  [ -d "$dir" ] || continue
  i=$((i+1))
  frames=$(printf "%.0f" "$(echo "$CITY_SECONDS * $FPS" | bc -l)")
  ffmpeg -y -loglevel error \
    -start_number "$SKIP_FRAMES" -framerate $FPS -i "$dir/frame-%06d.png" \
    -frames:v "$frames" -c:v libx264 -preset medium -crf 18 -pix_fmt yuv420p \
    "$SEG/city-$i.mp4"
done

# Koncová karta ze statického obrázku.
ffmpeg -y -loglevel error -loop 1 -framerate $FPS -i "$S/endcard.png" \
  -t "$END_SECONDS" -c:v libx264 -preset medium -crf 18 -pix_fmt yuv420p \
  "$SEG/end.mp4"

# Prolínačky: tvrdý střih mezi městy vypadá jako přeskočené video, měkký
# přechod drží tempo a nechá oko dojet pohyb kamery.
XF=0.5
inputs=(); for f in "$SEG"/city-*.mp4 "$SEG/end.mp4"; do inputs+=(-i "$f"); done

n=${#inputs[@]}; n=$((n/2))
filter=""; prev="0:v"; offset=0
for ((k=1;k<n;k++)); do
  offset=$(echo "$offset + $CITY_SECONDS - $XF" | bc -l)
  out="x$k"
  filter+="[$prev][$k:v]xfade=transition=fade:duration=$XF:offset=$offset[$out];"
  prev="$out"
done
filter="${filter%;}"

ffmpeg -y -loglevel error "${inputs[@]}" -filter_complex "$filter" -map "[$prev]" \
  -c:v libx264 -preset slow -crf 18 -pix_fmt yuv420p -movflags +faststart "$OUT"

ffprobe -v error -show_entries format=duration -of csv=p=0 "$OUT"
ls -lh "$OUT"
