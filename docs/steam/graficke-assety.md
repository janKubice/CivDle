# Grafika do Steamworks — co kam nahrát a jak ji vyrobit znovu

Všechny soubory mají **přesně** rozměr, který Steamworks vyžaduje. Špatná
velikost se nenahraje, takže rozměry hlídá test (`CapsuleSpecTests`) a sám
kompozitor je před uložením kontroluje.

## Co kam patří

Store Page Admin → **Graphical Assets**:

| Pole ve Steamworks | Soubor | Rozměr |
| --- | --- | --- |
| Header Capsule | `header-capsule-920x430.png` | 920 × 430 |
| Small Capsule | `small-capsule-462x174.png` | 462 × 174 |
| Main Capsule | `main-capsule-1232x706.png` | 1232 × 706 |
| Vertical Capsule | `vertical-capsule-748x896.png` | 748 × 896 |
| Page Background | `page-background-1438x810.png` | 1438 × 810 |

Library assets:

| Pole ve Steamworks | Soubor | Rozměr |
| --- | --- | --- |
| Library Capsule | `library-capsule-600x900.png` | 600 × 900 |
| Library Header | `library-header-920x430.png` | 920 × 430 |
| Library Hero | `library-hero-3840x1240.png` | 3840 × 1240 |
| Library Logo | `library-logo-1280x720.png` | 1280 × 720, **průhledné** |

Ostatní:

| K čemu | Soubor | Rozměr |
| --- | --- | --- |
| Community Icon | `community-icon-184x184.png` | 184 × 184 |
| Ikona klienta / okna hry | `client-icon.ico` | 16–256 px v jednom souboru |
| Značka na volné použití | `logo-transparent.png` | průhledná, oříznutá na grafiku |
| Značka na výšku | `logo-transparent-stacked.png` | průhledná, oříznutá na grafiku |
| Samotný emblém | `emblem-transparent-1024x1024.png` | 1024 × 1024, průhledný |

## Rozhodnutí, která za tím jsou

**Žádný podtitul.** Na kapsli patří jméno hry a nic víc. „An idle city that
grows while you rest" na 462 px široké kapsli stejně nikdo nepřečte a jen ubírá
místo nápisu, který přečíst má.

**Značka není text vykreslený hrou.** Herní font je stavěný na HUD; přes
screenshot z něj byl proužek s popiskem. Značku kreslí `tools/civdle_logo.py`
jako grafiku: emblém (tři domy rostoucí zleva doprava — z chalupy věž, což je
celá hra) a nápis se zlatým přechodem, tmavým obrysem a stínem, aby držel nad
libovolně světlou i tmavou scénou.

**Podklad je skutečná hra.** Kapsle je slib: když na ní bude něco, co ve hře
není, hráč to pozná na první screenshot. Podklady proto renderuje sama hra ze
skutečného světa, jen bez HUD.

**Hero a pozadí stránky jsou bez nápisu.** Přes hero kreslí Steam v knihovně
logo sám (dvě značky přes sebe vypadají jako chyba) a pozadí stránky si Steam
ztmaví a rozostří — nápis by z něj stejně nezbyl.

**Malá kapsle nese nápis skoro přes celou plochu**, jak Steam výslovně chce:
v seznamu se na ni kouká dvě vteřiny.

## Jak to vyrobit znovu

```
# 1) podklady ze hry (bez HUD, přesné rozměry)
dotnet run --project src/CivDle -- --capsules out/backdrops

# 2) značka + kompozice do finálních souborů
python3 tools/make_store_assets.py out/backdrops out/steam
```

Kompozitor kontroluje rozměr každého souboru před uložením a spadne, když
nesedí. Změna rozměru se tedy nedá provést omylem — jen v `CapsuleSpec.All`
(podklady) a v `ASSETS` v `tools/make_store_assets.py` (výstupy), a test
`CapsuleSpecTests` pak řekne, jestli to pořád odpovídá Steamworks.

## Co tady schválně není

**Broadcast assets.** Steamworks je označuje jako doporučené, ne povinné. Jejich
rozměry se navíc liší podle typu panelu, a tipnout si špatně by znamenalo
soubor, který se nenahraje — což je přesně to, čemu se tenhle celý postup vyhýbá.
Až budou potřeba, doplní se do `ASSETS` jako další řádek.
