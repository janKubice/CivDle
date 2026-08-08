#!/usr/bin/env python3
"""Ikona aplikace ze značky hry — jeden zdroj pravdy pro exe, okno i Steam.

Ikona bývala výřez herní scény: čtyři domky na zelené šachovnici. Ve 256 px to
šlo, ale na liště je ikona 16–32 px a z domků tam byla jen barevná kaše — což
je přesně ta velikost, ve které ji člověk vidí nejčastěji.

Emblém značky je na to stavěný: tmavý kruh se zlatým prstencem má i v 16 px
zřetelný obrys a odliší se od každé druhé ikony v liště.

Každá velikost se **kreslí zvlášť**, ne zmenšuje z jedné velké: tloušťka
prstence je podíl z průměru, takže malá ikona dostane proporčně silnější
prstenec a nerozpadne se.

Použití::

    python3 tools/make_app_icon.py src/CivDle
"""

from __future__ import annotations

import sys
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))

from civdle_logo import emblem  # noqa: E402  (po úpravě sys.path)

#: Velikosti, které Windows u ikony exe čte.
ICO_SIZES = (16, 24, 32, 48, 64, 128, 256)


def main(argv: list[str]) -> int:
    if len(argv) != 2:
        print(__doc__)
        return 2

    target = Path(argv[1])
    target.mkdir(parents=True, exist_ok=True)

    images = [emblem(size) for size in ICO_SIZES]
    largest = images[-1]

    # .ico pro Windows (ikona exe → plocha, lišta, Průzkumník).
    largest.save(target / "Icon.ico", format="ICO",
                 sizes=[(s, s) for s in ICO_SIZES], append_images=images[:-1])

    # .png pro SDL (okno na Linuxu a macOS).
    largest.save(target / "Icon.png")

    # .bmp pro okno MonoGame DesktopGL. BMP neumí průhlednost, takže se podloží
    # barvou pozadí hry — jinak by kolem kruhu zůstal černý čtverec.
    background = Image.new("RGB", largest.size, (24, 26, 32))
    background.paste(largest, (0, 0), largest)
    background.save(target / "Icon.bmp")

    for name in ("Icon.ico", "Icon.png", "Icon.bmp"):
        print(f"{target / name}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
