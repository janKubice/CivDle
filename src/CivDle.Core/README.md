# CivDle.Core

Herní jádro bez závislosti na MonoGame. Drží se pravidla „simulace nezná render" (tech-stack.md) —
všechno tady jde spustit a otestovat headless.

| Složka | Zodpovědnost |
|---|---|
| `Content/` | Načtení JSON definic (`data/`) do registrů, fail-fast validace odkazů a hodnot. |
| `WorldGen/` | Deterministický generátor světa: seedovaný Perlinův šum + výběr biomů podle definic. |
| `World/` | Datově orientovaná reprezentace mapy (plochá pole, žádné stromy objektů). |
| `Sim/` | Simulační smyčka s pevným krokem (10 Hz) — zatím jen kostra, poroste s dalšími fázemi. |

Tok dat: `data/*.json` → `ContentLoader` → `GameContent` (registry) → `MapGenerator` → `WorldMap` → `Simulation`.
Instance v simulaci odkazují na definice přes `int`/`byte` index, ne string (data-driven-content.md).
