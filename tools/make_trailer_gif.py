#!/usr/bin/env python3
"""Udělá z natočené sekvence PNG animovaný GIF na rychlé prohlédnutí.

Není to náhrada videa — GIF má 256 barev a je velký. Je to náhled: člověk se
podívá, jestli záběr sedí, ještě než ho pustí do editoru. Na hotové video
použij ffmpeg (příkaz hra vypíše po natáčení).

Použití:
    python3 tools/make_trailer_gif.py trailer/01-prehlidka nahled.gif [--sirka 640] [--fps 20]

Pixel art se zmenšuje s NEAREST: bilineární filtr by z ostrých hran udělal
kaši a náhled by lhal o tom, jak hra vypadá.
"""
import argparse
import pathlib
import sys

try:
    from PIL import Image
except ImportError:  # pragma: no cover - nástroj, ne herní kód
    sys.exit("Chybí Pillow. Nainstaluj ho: pip install pillow")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("sekvence", help="složka se snímky frame-%06d.png")
    parser.add_argument("vystup", help="cílový .gif")
    parser.add_argument("--sirka", type=int, default=640, help="šířka náhledu v pixelech")
    parser.add_argument("--fps", type=int, default=20, help="snímků za sekundu v GIFu")
    args = parser.parse_args()

    directory = pathlib.Path(args.sekvence)
    frames = sorted(directory.glob("frame-*.png"))
    if not frames:
        print(f"Ve složce {directory} nejsou žádné snímky.", file=sys.stderr)
        return 1

    # Zdroj je 60 fps; do GIFu se bere každý n-tý snímek, ať soubor nenaroste
    # do stovek megabajtů.
    step = max(1, round(60 / max(1, args.fps)))
    picked = frames[::step]

    first = Image.open(picked[0])
    height = round(first.height * args.sirka / first.width)

    images = [
        Image.open(path).convert("RGB").resize((args.sirka, height), Image.NEAREST)
        for path in picked
    ]

    images[0].save(
        args.vystup,
        save_all=True,
        append_images=images[1:],
        duration=round(1000 / args.fps),
        loop=0,
        optimize=True,
    )

    print(f"{args.vystup}: {len(images)} snímků, {args.sirka}×{height}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
