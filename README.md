# CivDle

2D top-down idle city-builder (C# / .NET 8 / MonoGame). Návrh hry je popsán
v design dokumentech v kořeni repozitáře (`tech-stack.md`, `mvp-roadmap.md`, …),
pravidla vývoje v `CLAUDE.md`.

Aktuální stav: **generátor světa + základní herní smyčka** — menu, nová hra
(seed, velikost, typ světa), mapa biomů, stavění budov (těžba, farma, domy),
výroba a růst populace s jídlem jako soft pressure, pauza, nastavení
(jazyk CZ/EN + grafika), vše data-driven.

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
| Posun mapy | WASD / šipky / tažení pravým či prostředním tlačítkem |
| Zoom | kolečko myši (k pozici kurzoru) |
| Stavění | vybrat budovu dole → levé tlačítko postaví, pravý klik / Esc zruší |
| Pauza / návrat | Esc |

## Herní smyčka (MVP)

Dřevorubecký tábor (les) a kamenolom (hory) těží suroviny, farma (louka) živí
populaci, domy zvedají kapacitu bydlení. Výroba jede podle obsazenosti
(populace vs. pracovní místa); došlé jídlo růst jen zastaví, nikdy nic neničí.
Simulace tiká 10× za sekundu, render 60 FPS a do simulace nikdy nezapisuje.

## Struktura

```
data/                  JSON definice obsahu (biomy, suroviny, budovy, jazyky, …)
src/CivDle.Core/       jádro: content loader, generátor, simulace, nastavení (bez MonoGame)
src/CivDle/            hra: MonoGame render, kamera, obrazovky, Myra UI
tests/CivDle.Core.Tests/  unit testy jádra
```

Architektura drží sim/render split z `tech-stack.md`: simulace je čistá C# knihovna
tikající pevným krokem, render ji jen čte; příkazy hráče (stavba) vstupují přes
metody simulace. Obsah je data-driven (`data-driven-content.md`): definice v JSON
se při startu fail-fast zvalidují a simulace na ně odkazuje přes indexy.

## Data-driven obsah

- `data/biomes.json` — biomy: barva, variace jasu, rozsahy hloubky/výšky/vlhkosti.
- `data/worldgen.json` — velikosti světa a terénní presety (hladina moře, šum;
  `frequency` = počet vln na 100 dlaždic).
- `data/resources.json` — suroviny: barva ikony, počáteční zásoba.
- `data/buildings.json` — budovy: půdorys, cena, recept (vstupy → výstupy za N tiků),
  pracovní místa, kapacita bydlení, povolené biomy.
- `data/gameplay.json` — balanc smyčky: startovní populace, růst, spotřeba jídla.
- `data/lang/*.json` — jazyky (cs, en): všechny texty hry včetně jmen obsahu
  (`biome.*`, `building.*`, …). Loader hlídá, že jazyky mají shodné klíče a nic nechybí.

Stejný seed + stejná data = vždy stejná mapa (vlastní deterministický šum i hash).
Uživatelská nastavení se ukládají do `%APPDATA%/CivDle/settings.json`
(na Linuxu `~/.config/CivDle/settings.json`).
