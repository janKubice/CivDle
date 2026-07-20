# CivDle (spustitelná hra)

Render/UI vrstva nad `CivDle.Core`. Čte stav simulace; jediný zápis do ní jsou
příkazy hráče přes veřejné metody simulace (stavba budov).

| Složka | Zodpovědnost |
|---|---|
| `Screens/` | Zásobník obrazovek: hlavní menu → nová hra / nastavení → hra (+ pauza jako overlay). Texty přes `Localization`; po změně jazyka se obrazovky přestaví (event). |
| `Rendering/` | Kamera, vykreslení mapy, cest, budov a ghost náhledu; jmenovky osad; den/noc overlay + noční světla budov (aditivní zář a okénka); deterministické biomové dekorace s LOD. |
| `Rendering/Effects/` | Juice a život: pooled částice, plovoucí popupy („+2") a ambientní fauna spawnovaná jen u kamery — čistě vizuální, simulace o nich neví. |
| `Audio/` | Procedurální placeholder zvuky (sek, žuch) s náhodným pitchem; bez audio zařízení se tiše vypnou. |
| `Input/` | Snímkový stav klávesnice/myši (stisknuto vs. drženo, delta kolečka, klik vs. tažení). |

MVP vykreslení mapy: 1 texel = 1 dlaždice (barvy biomů z JSON), jeden draw call se
škálováním — culling i LOD řeší GPU. Budovy jsou barevné obdélníky s cullingem
podle výřezu kamery. Až budou sprity, nahradí to chunky + texture atlas.
UI staví na knihovně Myra (default dle tech-stack.md) s vestavěným fontem —
projekt tak nepotřebuje MonoGame content pipeline. Nastavení grafiky (rozlišení,
režim okna, VSync) aplikuje `CivDleGame` na `GraphicsDeviceManager`.
