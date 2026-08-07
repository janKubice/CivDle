#!/usr/bin/env python3
"""Vygeneruje ikony achievementů pro Steam.

Proč generátor a ne 192 ručně kreslených obrázků: Steam chce ke každému
achievementu dvě ikony (odemčenou a zamčenou), takže při 96 achievementech je to
192 souborů. Ručně je to týden práce a při každém přidaném achievementu další
odpoledne — a hlavně by nebyly konzistentní.

Ikona nese informaci ve třech vrstvách, aby šla poznat i v 64 px:

* **barva pozadí** = druh metriky (těžba, stavby, výzkum, populace…), takže
  jeden pohled na mřížku achievementů řekne, čeho se týkají;
* **symbol** = konkrétní metrika, nakreslený jako jednoduchý piktogram;
* **prstenec** = stupeň (bronz/stříbro/zlato) podle toho, jak vysoko je práh.

Zamčená varianta je tatáž ikona odbarvená a ztmavená — Steam ji ukazuje vedle
odemčené, takže musí být poznat, že jde o tentýž achievement.

Spuštění:
    python3 tools/make_achievement_icons.py
Výstup:
    docs/steam/generated/achievement-icons/<id>.png        (64x64, odemčená)
    docs/steam/generated/achievement-icons/<id>-locked.png (64x64, zamčená)
    docs/steam/generated/achievement-icons/src/<id>.png    (256x256, zdroj)
"""

from __future__ import annotations

import json
import pathlib

from PIL import Image, ImageDraw

ROOT = pathlib.Path(__file__).resolve().parents[1]
OUT = ROOT / "docs" / "steam" / "generated" / "achievement-icons"
SRC = OUT / "src"

# Velikost, ve které se kreslí. Kreslí se velké a zmenšuje se s LANCZOS —
# přímé kreslení do 64 px dává zubaté kruhy.
BIG = 256
SMALL = 64

# Barva podle druhu metriky. Odpovídá tomu, jak věci vypadají ve hře, aby ikona
# a herní prvek k sobě patřily i vizuálně.
METRIC_COLORS: dict[str, tuple[int, int, int]] = {
    "harvested": (140, 90, 40),      # dřevo/kámen — hnědá
    "resource": (170, 120, 55),      # zásoby — tmavší hnědá
    "building": (200, 140, 70),      # budovy — oranžová
    "buildings": (200, 140, 70),
    "population": (90, 150, 90),     # lidé — zelená
    "research": (80, 150, 190),      # věda — modrá
    "ascension": (150, 100, 200),    # Vzestup — fialová (jako v UI)
    "cities": (190, 110, 90),        # cizí města — cihlová
    "explored": (100, 160, 170),     # mapa — tyrkysová
    "day": (120, 130, 150),          # čas — šedomodrá
    "planted": (80, 160, 80),        # sázení — sytě zelená
    "merged": (170, 150, 90),        # slučování — okrová
    "terraformed": (150, 120, 80),   # terén — písková
    "wonders": (210, 175, 90),       # divy — zlatá
    "prayers": (200, 180, 220),      # víra — světle fialová
}
DEFAULT_COLOR = (120, 120, 130)

# Prahy, nad kterými achievement dostane vyšší stupeň prstence. Jsou hrubé
# schválně: jde o vizuální odstupňování, ne o přesnou obtížnost.
TIER_THRESHOLDS = ((10_000, "gold"), (500, "silver"))
TIER_COLORS = {
    "gold": (235, 200, 110),
    "silver": (205, 210, 220),
    "bronze": (190, 140, 95),
}


def tier_for(target: float) -> str:
    for threshold, name in TIER_THRESHOLDS:
        if target >= threshold:
            return name
    return "bronze"


def lerp(a: tuple[int, int, int], b: tuple[int, int, int], t: float) -> tuple[int, int, int]:
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


def draw_symbol(draw: ImageDraw.ImageDraw, metric: str, colour: tuple[int, int, int]) -> None:
    """Piktogram uprostřed. Tvary jsou úmyslně hrubé — v 64 px detail zanikne."""
    c = BIG // 2
    u = BIG // 16  # základní jednotka, ať se to škáluje s velikostí

    if metric == "harvested":
        # Hromada nasbíraného: tři kruhy do trojúhelníku.
        for dx, dy in ((-2.2, 1.4), (2.2, 1.4), (0, -1.6)):
            draw.ellipse(
                [c + dx * u - 2.4 * u, c + dy * u - 2.4 * u, c + dx * u + 2.4 * u, c + dy * u + 2.4 * u],
                fill=colour)
    elif metric == "resource":
        # Zásoba ve skladu: srovnané bedny. Schválně jiný tvar než „natěženo" —
        # ve stejné hnědé by se dvě hromady kruhů nedaly rozeznat.
        for x0, y0 in ((-3.6, -0.2), (0.4, -0.2), (-1.6, -4.2), (-3.6, 3.8), (0.4, 3.8)):
            draw.rectangle(
                [c + x0 * u, c + y0 * u, c + (x0 + 3.2) * u, c + (y0 + 3.2) * u],
                fill=colour)
    elif metric in ("building", "buildings"):
        # Domek: čtverec a sedlová střecha.
        draw.rectangle([c - 3.2 * u, c - 0.4 * u, c + 3.2 * u, c + 3.6 * u], fill=colour)
        draw.polygon([(c - 4.2 * u, c - 0.4 * u), (c, c - 4.2 * u), (c + 4.2 * u, c - 0.4 * u)], fill=colour)
    elif metric == "population":
        # Postava: hlava a ramena.
        draw.ellipse([c - 1.9 * u, c - 4.0 * u, c + 1.9 * u, c - 0.2 * u], fill=colour)
        draw.pieslice([c - 3.8 * u, c - 0.6 * u, c + 3.8 * u, c + 6.4 * u], 180, 360, fill=colour)
    elif metric == "research":
        # Atom: kruh a dvě dráhy.
        draw.ellipse([c - 1.3 * u, c - 1.3 * u, c + 1.3 * u, c + 1.3 * u], fill=colour)
        for box in ([c - 4.4 * u, c - 2.0 * u, c + 4.4 * u, c + 2.0 * u],
                    [c - 2.0 * u, c - 4.4 * u, c + 2.0 * u, c + 4.4 * u]):
            draw.ellipse(box, outline=colour, width=int(0.7 * u))
    elif metric == "ascension":
        # Šipka vzhůru.
        draw.polygon([(c, c - 4.4 * u), (c + 3.6 * u, c + 0.4 * u), (c + 1.5 * u, c + 0.4 * u),
                      (c + 1.5 * u, c + 4.2 * u), (c - 1.5 * u, c + 4.2 * u), (c - 1.5 * u, c + 0.4 * u),
                      (c - 3.6 * u, c + 0.4 * u)], fill=colour)
    elif metric == "cities":
        # Tři věže různé výšky = cizí město.
        for dx, h in ((-2.6, 2.6), (0, 4.2), (2.6, 3.2)):
            draw.rectangle([c + dx * u - 1.0 * u, c - h * u, c + dx * u + 1.0 * u, c + 3.8 * u], fill=colour)
    elif metric == "explored":
        # Kompasová růžice.
        draw.polygon([(c, c - 4.4 * u), (c + 1.3 * u, c - 1.3 * u), (c + 4.4 * u, c),
                      (c + 1.3 * u, c + 1.3 * u), (c, c + 4.4 * u), (c - 1.3 * u, c + 1.3 * u),
                      (c - 4.4 * u, c), (c - 1.3 * u, c - 1.3 * u)], fill=colour)
    elif metric == "day":
        # Ciferník.
        draw.ellipse([c - 4.2 * u, c - 4.2 * u, c + 4.2 * u, c + 4.2 * u], outline=colour, width=int(0.8 * u))
        draw.line([c, c, c, c - 2.8 * u], fill=colour, width=int(0.7 * u))
        draw.line([c, c, c + 2.0 * u, c], fill=colour, width=int(0.7 * u))
    elif metric == "planted":
        # Jehličnan ze tří pater. Jeden trojúhelník se v 64 px čte jako šipka
        # a pletl by se se Vzestupem — patra ho odliší na první pohled.
        draw.rectangle([c - 0.7 * u, c + 2.2 * u, c + 0.7 * u, c + 4.4 * u], fill=colour)
        for cy, half in ((-2.6, 2.2), (-0.4, 3.0), (1.6, 3.8)):
            draw.polygon(
                [(c, c + (cy - 2.2) * u), (c + half * u, c + (cy + 1.0) * u), (c - half * u, c + (cy + 1.0) * u)],
                fill=colour)
    elif metric == "merged":
        # Dva čtverce splývající v jeden.
        draw.rectangle([c - 4.0 * u, c - 4.0 * u, c - 0.3 * u, c - 0.3 * u], fill=colour)
        draw.rectangle([c + 0.3 * u, c + 0.3 * u, c + 4.0 * u, c + 4.0 * u], fill=colour)
        draw.rectangle([c - 1.6 * u, c - 1.6 * u, c + 1.6 * u, c + 1.6 * u], fill=colour)
    elif metric == "terraformed":
        # Vlna terénu.
        draw.polygon([(c - 4.4 * u, c + 3.0 * u), (c - 1.6 * u, c - 2.2 * u), (c + 0.6 * u, c + 1.0 * u),
                      (c + 2.4 * u, c - 3.2 * u), (c + 4.4 * u, c + 3.0 * u)], fill=colour)
    elif metric == "wonders":
        # Hvězda.
        pts = []
        import math
        for i in range(10):
            r = 4.4 * u if i % 2 == 0 else 1.9 * u
            a = math.pi / 2 * 3 + i * math.pi / 5
            pts.append((c + math.cos(a) * r, c + math.sin(a) * r))
        draw.polygon(pts, fill=colour)
    elif metric == "prayers":
        # Kapka / plamen.
        draw.polygon([(c, c - 4.4 * u), (c + 3.0 * u, c + 1.0 * u), (c, c + 4.2 * u), (c - 3.0 * u, c + 1.0 * u)],
                     fill=colour)
    else:
        draw.ellipse([c - 3.4 * u, c - 3.4 * u, c + 3.4 * u, c + 3.4 * u], fill=colour)


def render(metric: str, target: float, locked: bool) -> Image.Image:
    base = METRIC_COLORS.get(metric, DEFAULT_COLOR)
    ring = TIER_COLORS[tier_for(target)]
    symbol = (250, 248, 244)

    if locked:
        # Zamčená: odbarvit k šedé a ztmavit. Musí být poznat, že je to TENTÝŽ
        # achievement — proto odbarvení, ne jiný tvar.
        grey = (86, 88, 94)
        base = lerp(lerp(base, grey, 0.82), (0, 0, 0), 0.25)
        ring = lerp(lerp(ring, grey, 0.82), (0, 0, 0), 0.25)
        symbol = (128, 130, 136)

    img = Image.new("RGBA", (BIG, BIG), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)

    # Kruhové pozadí s jemným svislým přechodem — plochá barva vypadá mrtvě.
    top = lerp(base, (255, 255, 255), 0.18)
    bottom = lerp(base, (0, 0, 0), 0.28)
    grad = Image.new("RGB", (1, BIG))
    for y in range(BIG):
        grad.putpixel((0, y), lerp(top, bottom, y / (BIG - 1)))
    grad = grad.resize((BIG, BIG))

    mask = Image.new("L", (BIG, BIG), 0)
    ImageDraw.Draw(mask).ellipse([4, 4, BIG - 5, BIG - 5], fill=255)
    img.paste(grad, (0, 0), mask)

    draw_symbol(draw, metric, symbol)

    # Prstenec stupně až nakonec, ať překryje případný přesah symbolu.
    draw.ellipse([4, 4, BIG - 5, BIG - 5], outline=ring, width=10)
    return img


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    SRC.mkdir(parents=True, exist_ok=True)

    achievements = json.loads((ROOT / "data" / "achievements.json").read_text(encoding="utf-8"))["achievements"]

    for a in achievements:
        metric = a["condition"]["metric"]
        target = float(a["condition"].get("target", 1))

        for locked in (False, True):
            img = render(metric, target, locked)
            suffix = "-locked" if locked else ""
            img.resize((SMALL, SMALL), Image.LANCZOS).save(OUT / f"{a['id']}{suffix}.png")
            if not locked:
                img.save(SRC / f"{a['id']}.png")

    print(f"hotovo: {len(achievements)} achievementů → {len(achievements) * 2} ikon v {OUT}")


if __name__ == "__main__":
    main()
