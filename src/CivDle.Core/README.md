# CivDle.Core

Herní jádro bez závislosti na MonoGame. Drží se pravidla „simulace nezná render"
(tech-stack.md) — všechno tady jde spustit a otestovat headless.

| Složka | Zodpovědnost |
|---|---|
| `Content/` | Načtení JSON definic (`data/`) do registrů, fail-fast validace odkazů a hodnot; lokalizace (jazyky, překlady). |
| `WorldGen/` | Deterministický generátor světa: seedovaný Perlinův šum + výběr biomů podle definic. |
| `World/` | Datově orientovaná reprezentace mapy (plochá pole, žádné stromy objektů). |
| `Sim/` | Simulace s pevným krokem (10 Hz): budovy jako struktury v plochém poli, výroba se sklady, populace jako agregát, auto-stavba dle poptávky (nízká frekvence, deterministicky ze seedu), příkazy hráče. |
| `Save/` | Binární verzovaný save (vlastní writer + gzip): remap definic přes stabilní ID, atomický zápis, poškozený soubor hru neshodí. |
| `Config/` | Uživatelská nastavení (jazyk, grafika) + jejich ukládání. Na rozdíl od obsahu ne-fail-fast: rozbitý soubor = výchozí hodnoty. |

Tok dat: `data/*.json` → `ContentLoader` → `GameContent` (registry) → `MapGenerator`
→ `WorldMap` → `Simulation` (systémy `ProductionSystem`, `PopulationSystem`, `AutoBuildSystem`).
Instance v simulaci odkazují na definice přes `int`/`byte` index, ne string
(data-driven-content.md). Jména obsahu žijí v jazycích pod klíči `biome.<id>`,
`building.<id>` atd. a jejich úplnost hlídá loader.
