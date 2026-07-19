# CivDle

2D top-down idle city-builder (C# / .NET 8 / MonoGame). Návrh hry je popsán
v design dokumentech v kořeni repozitáře (`tech-stack.md`, `mvp-roadmap.md`, …),
pravidla vývoje v `CLAUDE.md`.

Aktuální stav: **generátor světa + herní kostra** — menu, nová hra (seed, velikost,
typ světa), vygenerovaná mapa biomů, kamera (pan + zoom), pauza, data-driven definice.

## Spuštění (vývoj)

Vyžaduje [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
dotnet run --project src/CivDle
```

## Vytvoření distribučního exe

Jedno self-contained exe (hráč nepotřebuje instalovat .NET):

```bash
./publish.sh           # Windows x64 → dist/win-x64/CivDle.exe
./publish.sh linux-x64 # Linux x64  → dist/linux-x64/CivDle
```

Na Windows totéž udělá `publish.cmd`. Vedle exe se přibalí složka `data/`
s JSON definicemi — obsah je záměrně editovatelný (modding, ladění balance).

## Testy

```bash
dotnet test
```

## Ovládání

| Akce | Ovládání |
|---|---|
| Posun mapy | WASD / šipky / tažení myší |
| Zoom | kolečko myši (k pozici kurzoru) |
| Pauza / návrat | Esc |

## Struktura

```
data/                  JSON definice obsahu (biomy, nastavení generátoru)
src/CivDle.Core/       jádro: content loader, generátor světa, simulace (bez MonoGame)
src/CivDle/            hra: MonoGame render, kamera, obrazovky, Myra UI
tests/CivDle.Core.Tests/  unit testy jádra (obsah, generátor, šum, simulace)
```

Architektura drží sim/render split z `tech-stack.md`: simulace je čistá C# knihovna
tikající pevným krokem, render ji jen čte. Obsah je data-driven (`data-driven-content.md`):
definice v JSON se při startu fail-fast zvalidují a simulace na ně odkazuje přes indexy.

## Data-driven obsah

- `data/biomes.json` — biomy: barva na mapě, variace jasu, a rozsahy
  (hloubka pro vodu, výška × vlhkost pro pevninu), podle kterých generátor vybírá biom.
- `data/worldgen.json` — velikosti světa a terénní presety (Kontinenty / Ostrovy /
  Pangea): hladina moře a parametry šumu. `frequency` = počet vln na 100 dlaždic.

Stejný seed + stejná data = vždy stejná mapa (vlastní deterministický šum i hash).
