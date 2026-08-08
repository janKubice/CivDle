"""Značka CivDle: emblém a nápis, kreslené jako grafika — ne jako popisek.

Proč vlastní modul a ne text vykreslený hrou: herní font je stavěný na HUD.
Na obálce z něj byl proužek s textem přes screenshot, a to je přesně to, podle
čeho se v seznamu her pozná projekt, kterému nikdo nedělal grafiku.

Značka má dvě části, které jdou použít zvlášť:

* **emblém** — izometrická věž ze tří dílů rostoucí zleva doprava (to je celá
  hra: malé se mění ve velké) na kruhovém štítu s prstencem;
* **nápis** — „CivDle" těžkým řezem, se zlatým přechodem, tmavým obrysem
  a stínem, takže drží nad libovolně světlou i tmavou scénou.

Vše se kreslí ve čtyřnásobku a teprve pak zmenšuje (SSAA) — Pillow neumí
antialiasing tvarů a bez toho by hrany kostek a prstence byly zubaté.

Bez podtitulu. Na kapsli má být jméno hry, nic víc.
"""

from __future__ import annotations

from dataclasses import dataclass

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont

#: Kolikrát větší se kreslí, než se ukládá. Čtyřka stačí; osmička už jen žere paměť.
SS = 4

#: Písmo nápisu. V kontejneru nejsou žádné hezké display fonty, takže charakter
#: nese kompozice a povrch, ne řez — DejaVu Sans Bold je aspoň široký a čitelný
#: a má kompletní češtinu (kdyby se jméno někdy lokalizovalo).
WORDMARK_FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"

GOLD_TOP = (255, 226, 150)
GOLD_BOTTOM = (214, 150, 46)
INK = (14, 18, 26)
TEAL = (96, 196, 220)
STONE_LIGHT = (232, 226, 208)


@dataclass(frozen=True)
class Palette:
    """Barvy emblému. Držené pohromadě, ať jde značka přebarvit jedním místem."""

    shield: tuple[int, int, int] = (20, 27, 38)
    ring: tuple[int, int, int] = GOLD_BOTTOM
    roof: tuple[int, int, int] = (206, 92, 68)
    wall_light: tuple[int, int, int] = STONE_LIGHT
    wall_dark: tuple[int, int, int] = (150, 146, 134)
    ground: tuple[int, int, int] = (92, 138, 92)


def vertical_gradient(size: tuple[int, int], top: tuple[int, int, int],
                      bottom: tuple[int, int, int]) -> Image.Image:
    """Svislý přechod. Zlato bez přechodu vypadá jako žlutá — teprve spád z něj udělá kov."""
    width, height = size
    gradient = Image.new("RGB", (1, height))
    for y in range(height):
        t = y / max(1, height - 1)
        gradient.putpixel((0, y), tuple(
            round(top[i] + (bottom[i] - top[i]) * t) for i in range(3)))
    return gradient.resize((width, height), Image.NEAREST)


def _iso_box(draw: ImageDraw.ImageDraw, cx: int, base_y: int, half_w: int,
             height: int, palette: Palette, roof: bool) -> None:
    """Jedna izometrická kostka: horní kosočtverec a dvě boční stěny."""
    half_h = half_w // 2

    top_y = base_y - height
    top = [(cx, top_y - half_h), (cx + half_w, top_y), (cx, top_y + half_h), (cx - half_w, top_y)]
    left = [(cx - half_w, top_y), (cx, top_y + half_h), (cx, base_y + half_h), (cx - half_w, base_y)]
    right = [(cx + half_w, top_y), (cx, top_y + half_h), (cx, base_y + half_h), (cx + half_w, base_y)]

    # Pořadí: nejdřív stěny, pak střecha — jinak by ji stěny překryly.
    draw.polygon(left, fill=palette.wall_light)
    draw.polygon(right, fill=palette.wall_dark)
    draw.polygon(top, fill=palette.roof if roof else palette.wall_light)


def emblem(size: int, palette: Palette | None = None) -> Image.Image:
    """Kulatý emblém se třemi rostoucími domy. Vrací RGBA s průhledným pozadím."""
    palette = palette or Palette()
    # Strop jako u nápisu: nad 1200 px už zmenšení nic nepřidá.
    s = min(size * SS, 1200)
    image = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    ring = max(2, s // 22)

    # Štít: tmavý kruh. Kreslí se celý a teprve na něj přijde všechno ostatní —
    # dřív se sem přes masku vkládala i průhledná část trávy, což ze štítu
    # udělalo díru.
    draw.ellipse([0, 0, s - 1, s - 1], fill=palette.shield + (255,))

    # Kruh vnitřku (bez prstence) — používá se jako maska na trávu.
    inside = Image.new("L", (s, s), 0)
    ImageDraw.Draw(inside).ellipse([ring, ring, s - 1 - ring, s - 1 - ring], fill=255)

    # Zem: pruh trávy přes spodní třetinu, oříznutý vnitřkem štítu.
    ground = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    ImageDraw.Draw(ground).rectangle([0, int(s * 0.62), s, s], fill=palette.ground + (255,))
    ground.putalpha(Image.composite(ground.getchannel("A"), Image.new("L", (s, s), 0), inside))
    image.alpha_composite(ground)

    # Tři domy rostoucí zleva doprava — to je celá hra v jednom obrázku:
    # z chalupy se stane věž. Nejvyšší dostane zlatou střechu, aby to byl
    # dominantní bod a ne jen třetí kvádr.
    base = int(s * 0.66)
    half = int(s * 0.115)
    tower = Palette(**{**palette.__dict__, "roof": GOLD_TOP})
    for index, (offset, height) in enumerate(((-2.0, 0.10), (0.0, 0.20), (2.0, 0.32))):
        _iso_box(draw, int(s / 2 + offset * half), base, half,
                 int(s * height), tower if index == 2 else palette, roof=True)

    # Prstenec až nakonec: překryje případný přesah trávy i domů a udělá
    # z obrázku odznak — bez něj se emblém na světlém pozadí rozteče.
    draw.ellipse([ring // 2, ring // 2, s - 1 - ring // 2, s - 1 - ring // 2],
                 outline=palette.ring + (255,), width=ring)

    return image.resize((size, size), Image.LANCZOS)


def wordmark(height: int, text: str = "CivDle") -> Image.Image:
    """Nápis se zlatým přechodem, tmavým obrysem a stínem. RGBA, těsně oříznuté."""
    # Vnitřní velikost je zastropovaná: nad tisíc pixelů už zmenšení nic
    # nepřidá, ale rozostření a kresba rostou s plochou. Bez stropu trvala
    # jedna značka 2048 px minuty.
    s = min(height * SS, 1100)
    scale_back = height / s

    font = ImageFont.truetype(WORDMARK_FONT, s)

    # Nejdřív změř, pak teprve kresli: obrys i stín přetékají přes rámec textu
    # a bez rezervy by se ořízly.
    probe = ImageDraw.Draw(Image.new("L", (1, 1)))
    left, top, right, bottom = probe.textbbox((0, 0), text, font=font)
    pad = int(s * 0.22)
    canvas = (right - left + pad * 2, bottom - top + pad * 2)
    origin = (pad - left, pad - top)

    # Maska písmen a maska písmen s obrysem. Obrys kreslí přímo Pillow
    # (``stroke_width``) — morfologická dilatace přes MaxFilter dělá totéž,
    # ale u velkého jádra je o několik řádů pomalejší.
    outline_width = max(2, s // 26)
    glyphs = Image.new("L", canvas, 0)
    ImageDraw.Draw(glyphs).text(origin, text, font=font, fill=255)

    outlined = Image.new("L", canvas, 0)
    ImageDraw.Draw(outlined).text(
        origin, text, font=font, fill=255, stroke_width=outline_width, stroke_fill=255)

    image = Image.new("RGBA", canvas, (0, 0, 0, 0))

    # Stín: rozostřený obrys posunutý dolů. Drží nápis nad scénou, aby
    # nevypadal jako nálepka.
    shadow = outlined.filter(ImageFilter.GaussianBlur(max(1, s // 28))).point(lambda v: int(v * 0.75))
    image.paste(Image.new("RGBA", canvas, (0, 0, 0, 255)), (0, max(1, int(s * 0.055))), shadow)

    # Tmavý obrys pod písmeny — bez něj se zlato ztratí nad pouští i nad polem.
    image.paste(Image.new("RGBA", canvas, INK + (255,)), (0, 0), outlined)

    # Výplň přechodem.
    fill = vertical_gradient(canvas, GOLD_TOP, GOLD_BOTTOM).convert("RGBA")
    image.paste(fill, (0, 0), glyphs)

    # Světlá horní hrana: rozdíl masky a téže masky posunuté dolů dá proužek
    # po horním obrysu písmen. Levné a vypadá to jako ražené.
    shift = max(1, s // 34)
    lowered = ImageChops.offset(glyphs, 0, shift)
    bevel = ImageChops.subtract(glyphs, lowered).point(lambda v: int(v * 0.62))
    image.paste(Image.new("RGBA", canvas, (255, 248, 220, 255)), (0, 0), bevel)

    box = image.getbbox()
    cropped = image.crop(box)
    return cropped.resize(
        (max(1, round(cropped.width * scale_back)), max(1, round(cropped.height * scale_back))),
        Image.LANCZOS)


def lockup(width: int, stacked: bool = False) -> Image.Image:
    """
    Celá značka: emblém a nápis pohromadě.

    ``stacked`` skládá emblém nad nápis (pro vysoké formáty), jinak vedle sebe.
    """
    if stacked:
        mark = emblem(int(width * 0.42))
        text = wordmark(int(width * 0.20))
        text = text.resize(
            (min(int(width * 0.92), text.width), int(text.height * min(int(width * 0.92), text.width) / text.width)),
            Image.LANCZOS)

        gap = int(width * 0.05)
        canvas = Image.new("RGBA", (width, mark.height + gap + text.height), (0, 0, 0, 0))
        canvas.paste(mark, ((width - mark.width) // 2, 0), mark)
        canvas.paste(text, ((width - text.width) // 2, mark.height + gap), text)
        return canvas

    text = wordmark(int(width * 0.22))
    mark_size = int(text.height * 1.42)
    mark = emblem(mark_size)

    gap = int(width * 0.035)
    total = mark.width + gap + text.width
    scale = min(1.0, width / total)
    if scale < 1.0:
        mark = mark.resize((int(mark.width * scale), int(mark.height * scale)), Image.LANCZOS)
        text = text.resize((int(text.width * scale), int(text.height * scale)), Image.LANCZOS)
        gap = int(gap * scale)

    height = max(mark.height, text.height)
    canvas = Image.new("RGBA", (mark.width + gap + text.width, height), (0, 0, 0, 0))
    canvas.paste(mark, (0, (height - mark.height) // 2), mark)
    canvas.paste(text, (mark.width + gap, (height - text.height) // 2), text)
    return canvas
