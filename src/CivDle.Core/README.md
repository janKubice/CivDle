# CivDle.Core

Herní jádro bez závislosti na MonoGame. Drží se pravidla „simulace nezná render"
(tech-stack.md) — všechno tady jde spustit a otestovat headless.

| Složka | Zodpovědnost |
|---|---|
| `Content/` | Načtení JSON definic (`data/`) do registrů, fail-fast validace odkazů a hodnot; lokalizace; definice živé mapy (dekorace, fauna, denní cyklus). |
| `WorldGen/` | Seedovaný Perlinův/fBm šum; materializace výřezu do mapy (náhledy, testy). |
| `World/` | Nekonečný terén jako čistá funkce (`ITerrain` / `ProceduralTerrain`) + řídké klíče dlaždic; `WorldMap` jen pro náhledy/testy. |
| `Sim/` | Simulace s pevným krokem (10 Hz): budovy jako struktury v plochém poli, výroba se sklady, populace jako agregát, auto-stavba dle poptávky, auto-silnice (BFS napojení nové budovy) a detekce pojmenovaných osad (nízká frekvence, deterministicky ze seedu), příkazy hráče. |
| `Save/` | Binární verzovaný save v3 (gzip): terén se NEUKLÁDÁ (rekonstrukce z presetu+seedu), jen budovy a cesty; remap ID, poškozený soubor hru neshodí. |
| `Config/` | Uživatelská nastavení (jazyk, grafika) + jejich ukládání. Na rozdíl od obsahu ne-fail-fast: rozbitý soubor = výchozí hodnoty. |

Tok dat: `data/*.json` → `ContentLoader` → `GameContent` (registry) → `MapGenerator`
→ `WorldMap` → `Simulation` (systémy `ProductionSystem`, `PopulationSystem`, `AutoBuildSystem`, `RoadBuilder`, `SettlementSystem`).
Instance v simulaci odkazují na definice přes `int`/`byte` index, ne string
(data-driven-content.md). Jména obsahu žijí v jazycích pod klíči `biome.<id>`,
`building.<id>` atd. a jejich úplnost hlídá loader.
