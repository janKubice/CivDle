# CivDle

2D top-down idle city-builder (C# / .NET 8 / MonoGame). Návrh hry je popsán
v design dokumentech v kořeni repozitáře (`tech-stack.md`, `mvp-roadmap.md`, …),
pravidla vývoje v `CLAUDE.md`.

Aktuální stav: **nekonečná mapa + živé město** — menu se živým městem na pozadí
a rolovacím devlogem, nová hra (seed, typ světa), **nekonečná procedurální mapa**
(generuje se za běhu, neukládá se), sprity a ikony místo barviček, klikací těžba
stromů/kamenů s animací kácení (strom se zmenšuje → spadne s efektem), stavění
budov, výrobní řetězec (dřevo → prkna) se sklady, auto-stavba domů a auto-silnice
(pojmenované osady), živá mapa (den/noc s rozsvíceným městem, dekorace, fauna) +
chodci a vozíky ve městě, uložit/pokračovat, pauza, nastavení (jazyk CZ/EN
+ grafika), vše data-driven.

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
| Posun mapy | WASD / šipky / tažení pravým či prostředním tlačítkem (nekonečná mapa) |
| Zoom | kolečko myši (k pozici kurzoru) |
| Stavění | vybrat budovu dole → levé tlačítko postaví, pravý klik / Esc zruší |
| Ruční těžba | levý klik na strom (les) nebo kámen (hory) — po pár klicích spadne |
| Pauza / návrat | Esc |

## Herní smyčka (MVP)

Klikáním na les/hory hráč sbírá první suroviny; dřevorubecký tábor (les)
a kamenolom (hory) pak těží samy, pila řeže dřevo na prkna, farma (louka)
živí populaci a domy zvedají kapacitu bydlení. Když se populace blíží
kapacitě, vesnice si sama staví domy poblíž zástavby — za normální cenu
(dřevo + prkna), takže růst táhne celý řetězec. Každá nová budova se sama
napojí pěšinou na síť (cesty vedou jen po suché zemi) a shluk od tří budov
dostane jméno osady, které zůstává, i když osada roste. Výroba jede podle obsazenosti
(populace vs. pracovní místa); vyschlý vstup výrobu viditelně zastaví
(červený roh budovy) a plný sklad ji zastropuje — sklad kapacitu zvedá.
Došlé jídlo růst jen zastaví, nikdy nic neničí. Světem běží denní cyklus —
v noci se budovy rozsvítí (okna + teplá zář), za soumraku zlátne obloha;
u kamery se pasou srnky a v noci létají světlušky (LOD: z dálky mizí).
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

- `data/biomes.json` — biomy: barva, variace jasu, rozsahy hloubky/výšky/vlhkosti,
  volitelný `clickYield` (co dá ruční klik).
- `data/worldgen.json` — velikosti světa a terénní presety (hladina moře, šum;
  `frequency` = počet vln na 100 dlaždic).
- `data/resources.json` — suroviny: barva ikony, počáteční zásoba, kapacita skladu.
- `data/buildings.json` — budovy: půdorys, cena, recept (vstupy → výstupy za N tiků),
  pracovní místa, kapacita bydlení, povolené biomy, bonus skladu, `autoBuild`.
- `data/gameplay.json` — balanc smyčky: startovní populace, růst, spotřeba jídla,
  parametry auto-stavby, auto-silnic, detekce osad a denního cyklu
  (délka dne, barvy noci/soumraku).
- `data/settlement-names.json` — jména osad (vlastní jména se nepřekládají).
- `data/decorations.json` — biomové dekorace (barvy, hustota, velikosti) — anti-repetice.
- `data/fauna.json` — ambientní fauna (biomy, denní doba, rychlost, `glow` u světlušek).
- `data/lang/*.json` — jazyky (cs, en): všechny texty hry včetně jmen obsahu
  (`biome.*`, `building.*`, …). Loader hlídá, že jazyky mají shodné klíče a nic nechybí.

Stejný seed + stejná data = vždy stejná mapa. Mapa je **nekonečná**: terén je čistá
funkce šumu, počítá se on-demand pro libovolnou (i zápornou) dlaždici a nikdy se
neukládá — save drží jen seed, budovy a cesty.
- `data/decorations.json`, `data/fauna.json` — dekorace a ambientní fauna živé mapy.
- `data/devlog.json` — vývojový deník do menu (volitelný).

## Ukládání

Uložit hru jde z pauzy (Esc), pokračovat z hlavního menu. Save je binární,
komprimovaný a verzovaný (aktuálně v3 — nekonečná mapa, terén se neukládá;
starší save odmítne se srozumitelnou hláškou); definice odkazuje stabilními
string ID, takže přeuspořádání datových souborů save nerozbije. Vše se ukládá do profilu
uživatele: `%APPDATA%/CivDle/` (na Linuxu `~/.config/CivDle/`) —
`settings.json` + `saves/save.civdle`.
