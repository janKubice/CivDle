# CivDle.Core

Herní jádro bez závislosti na MonoGame. Drží se pravidla „simulace nezná render"
(tech-stack.md) — všechno tady jde spustit a otestovat headless.

| Složka | Zodpovědnost |
|---|---|
| `Content/` | Načtení JSON definic (`data/`) do registrů, fail-fast validace odkazů a hodnot; lokalizace; definice živé mapy (dekorace, fauna, denní cyklus). |
| `WorldGen/` | Seedovaný Perlinův/fBm šum; materializace výřezu do mapy (náhledy, testy). |
| `World/` | Nekonečný terén jako čistá funkce (`ITerrain` / `ProceduralTerrain`) + řídké klíče dlaždic; `WorldMap` jen pro náhledy/testy. |
| `Sim/` | Simulace s pevným krokem (10 Hz): budovy jako struktury v plochém poli, výroba se sklady, populace jako agregát, auto-stavba dle poptávky, auto-silnice a detekce osad; tech tree a vylepšování budov; Vzestup (prestige) s trvalými bonusy; úkoly a achievementy nad sdílenými metrikami (`GoalCondition`); oznámení (toasty) jako data. |
| `Save/` | Binární verzovaný save v6 (gzip): terén se NEUKLÁDÁ (rekonstrukce z presetu+seedu), jen budovy, cesty, technologie, Vzestup (úroveň/body/upgrady) a úkoly; remap ID, poškozený soubor hru neshodí. |
| `Config/` | Uživatelská nastavení (jazyk, grafika, hlasitost) a účet-wide profil (odemčené achievementy) + jejich ukládání. Na rozdíl od obsahu ne-fail-fast: rozbitý soubor = výchozí hodnoty. |

Tok dat: `data/*.json` → `ContentLoader` → `GameContent` (registry) → `MapGenerator`
→ `WorldMap` → `Simulation` (systémy `ProductionSystem`, `PopulationSystem`, `AutoBuildSystem`, `RoadBuilder`, `SettlementSystem`, `QuestSystem`, `AchievementSystem`).
Cíle (Vzestup, úkoly, achievementy) sdílejí typovanou podmínku `GoalCondition`
(metrika + práh): data říkají „co", `Simulation.EvaluateMetric` „jak" — žádná
logika v JSON. Prestige efekty jsou behavior-ID mapované na `PrestigeBonuses`.
Instance v simulaci odkazují na definice přes `int`/`byte` index, ne string
(data-driven-content.md). Jména obsahu žijí v jazycích pod klíči `biome.<id>`,
`building.<id>` atd. a jejich úplnost hlídá loader.
