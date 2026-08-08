#!/usr/bin/env python3
"""Složí grafiku do Steamu: herní podklad + značka = kapsle v přesném rozměru.

Podklady vyrábí sama hra (``CivDle.exe --capsules <složka>``) — jsou to skutečné
snímky herního světa, ne kreslená grafika. Tenhle skript na ně posadí značku
z :mod:`civdle_logo` a uloží každý soubor přesně v rozměru, který Steamworks
vyžaduje. Špatná velikost se do Steamworks nenahraje, takže rozměry jsou tady
vypsané jako data a kontroluje je test.

Použití::

    python3 tools/make_store_assets.py <složka-s-podklady> <výstupní-složka>

Pravidla, podle kterých je to složené:

* **Žádný podtitul.** Na kapsli patří jméno hry. „An idle city that grows…"
  na 462 px široké kapsli stejně nikdo nepřečte a jen ubírá místo nápisu.
* **Nápis musí být čitelný nad čímkoli.** Proto má vlastní obrys a stín a pod
  ním je měkký spád do tmy — ne tvrdý pruh, který vypadá jako popiska.
* **Malá kapsle nese nápis skoro přes celou plochu**, jak Steam výslovně chce.
* **Hero zůstává bez nápisu**: Steam přes něj v knihovně kreslí logo sám, takže
  by se překrývaly dvě značky.
"""

from __future__ import annotations

import sys
from dataclasses import dataclass
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))

from civdle_logo import emblem, lockup  # noqa: E402  (po úpravě sys.path)


@dataclass(frozen=True)
class Asset:
    """Jeden soubor do Steamworks."""

    #: Jméno výstupního souboru (včetně přípony).
    name: str
    #: Přesný rozměr, který Steamworks vyžaduje.
    size: tuple[int, int]
    #: Podklad ze hry; ``None`` = průhledné pozadí.
    backdrop: str | None
    #: Jak velká je značka vůči šířce plátna; 0 = bez značky.
    logo_width: float = 0.0
    #: Skládat emblém nad nápis (pro vysoké formáty)?
    stacked: bool = False
    #: Svislá pozice středu značky (0 = nahoře, 1 = dole).
    logo_y: float = 0.5
    #: Oříznout plátno těsně na značku? (Pro volně použitelné logo — u kapslí
    #: rozměr diktuje Steam, tady ho diktuje sama grafika.)
    trim: bool = False
    #: Kam soubor v Steamworks patří — jen pro výpis a README.
    note: str = ""


ASSETS: tuple[Asset, ...] = (
    Asset("header-capsule-920x430.png", (920, 430), "bg-header-920x430",
          logo_width=0.80, logo_y=0.50, note="Header Capsule"),
    Asset("small-capsule-462x174.png", (462, 174), "bg-small-462x174",
          logo_width=0.93, logo_y=0.50, note="Small Capsule"),
    Asset("main-capsule-1232x706.png", (1232, 706), "bg-main-1232x706",
          logo_width=0.72, logo_y=0.46, note="Main Capsule"),
    Asset("vertical-capsule-748x896.png", (748, 896), "bg-vertical-748x896",
          logo_width=0.78, stacked=True, logo_y=0.40, note="Vertical Capsule"),
    Asset("page-background-1438x810.png", (1438, 810), "bg-page-1438x810",
          note="Page Background (Steam ho ztmaví a rozostří sám — proto bez značky)"),
    Asset("library-capsule-600x900.png", (600, 900), "bg-library-capsule-600x900",
          logo_width=0.80, stacked=True, logo_y=0.40, note="Library Capsule"),
    Asset("library-header-920x430.png", (920, 430), "bg-header-920x430",
          logo_width=0.80, logo_y=0.50, note="Library Header"),
    Asset("library-hero-3840x1240.png", (3840, 1240), "bg-library-hero-3840x1240",
          note="Library Hero (logo přes něj kreslí Steam sám)"),
    Asset("library-logo-1280x720.png", (1280, 720), None,
          logo_width=0.94, stacked=True, note="Library Logo — MUSÍ být průhledné"),
    Asset("community-icon-184x184.png", (184, 184), None, note="Community Icon"),
    Asset("logo-transparent.png", (2400, 700), None, logo_width=1.0, trim=True,
          note="Značka na šířku, průhledná — na volné použití (web, tisk, video)"),
    Asset("logo-transparent-stacked.png", (1400, 1400), None,
          logo_width=1.0, stacked=True, trim=True, note="Značka na výšku, průhledná"),
    Asset("emblem-transparent-1024x1024.png", (1024, 1024), None,
          note="Samotný emblém bez nápisu, průhledný"),
)


def scrim(size: tuple[int, int], center_y: float) -> Image.Image:
    """
    Měkký spád do tmy pod značkou.

    Tvrdý pruh (co tam byl dřív) je na kapsli poznat na první pohled a dělá
    ze snímku popisku. Spád udrží nápis čitelný a přitom není vidět.
    """
    width, height = size
    column = Image.new("L", (1, height))
    for y in range(height):
        # Vzdálenost od středu značky v podílu výšky; kolem něj je nejtmavěji.
        distance = abs(y / max(1, height - 1) - center_y)
        strength = max(0.0, 1.0 - (distance / 0.42) ** 1.6)
        column.putpixel((0, y), int(150 * strength))

    veil = Image.new("RGBA", size, (6, 10, 16, 255))
    veil.putalpha(column.resize(size, Image.BILINEAR))
    return veil


def cover(image: Image.Image, size: tuple[int, int]) -> Image.Image:
    """Vyplní rozměr beze změny poměru stran (přebytek se ořízne ze středu)."""
    target_w, target_h = size
    scale = max(target_w / image.width, target_h / image.height)
    resized = image.resize(
        (max(1, round(image.width * scale)), max(1, round(image.height * scale))),
        Image.LANCZOS)
    left = (resized.width - target_w) // 2
    top = (resized.height - target_h) // 2
    return resized.crop((left, top, left + target_w, top + target_h))


def build(asset: Asset, backdrops: Path) -> Image.Image:
    """Složí jeden soubor."""
    canvas = Image.new("RGBA", asset.size, (0, 0, 0, 0))

    if asset.backdrop is not None:
        source = backdrops / f"{asset.backdrop}.png"
        if not source.exists():
            raise FileNotFoundError(
                f"chybí podklad {source} — spusť napřed hru s --capsules")
        canvas = cover(Image.open(source).convert("RGBA"), asset.size)

    # Emblém sám o sobě: ikona komunity a průhledný emblém.
    if asset.logo_width == 0.0 and asset.backdrop is None:
        mark = emblem(min(asset.size))
        canvas.alpha_composite(
            mark, ((asset.size[0] - mark.width) // 2, (asset.size[1] - mark.height) // 2))
        return canvas

    if asset.logo_width == 0.0:
        return canvas

    mark = lockup(int(asset.size[0] * asset.logo_width), stacked=asset.stacked)

    # Volné logo: plátno má být přesně tak velké jako grafika. Pevný rozměr by
    # kolem něj nechal průhledný okraj, se kterým se pak nedá zarovnávat.
    if asset.trim:
        return mark.crop(mark.getbbox())

    # Značka se nesmí dotknout okrajů ani přetéct na výšku — na úzkých formátech
    # (small capsule) rozhoduje výška, ne šířka.
    max_height = int(asset.size[1] * (0.82 if asset.stacked else 0.62))
    if mark.height > max_height:
        scale = max_height / mark.height
        mark = mark.resize((max(1, int(mark.width * scale)), max_height), Image.LANCZOS)

    if asset.backdrop is not None:
        canvas.alpha_composite(scrim(asset.size, asset.logo_y))

    x = (asset.size[0] - mark.width) // 2
    y = int(asset.size[1] * asset.logo_y - mark.height / 2)
    canvas.alpha_composite(mark, (x, max(0, y)))
    return canvas


def main(argv: list[str]) -> int:
    if len(argv) != 3:
        print(__doc__)
        return 2

    backdrops = Path(argv[1])
    output = Path(argv[2])
    output.mkdir(parents=True, exist_ok=True)

    for asset in ASSETS:
        image = build(asset, backdrops)
        if not asset.trim and image.size != asset.size:
            raise AssertionError(f"{asset.name}: {image.size} != {asset.size}")

        path = output / asset.name
        image.save(path)
        print(f"{path}  {image.width}×{image.height}  {asset.note}")

    # Ikona klienta jako .ico se všemi velikostmi, které Windows chce.
    icon = emblem(256)
    icon.save(output / "client-icon.ico",
              sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
    print(f"{output / 'client-icon.ico'}  16–256 px  Client Icon (.ico)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
