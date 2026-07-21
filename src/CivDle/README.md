# CivDle (spustitelná hra)

Render/UI vrstva nad `CivDle.Core`. Čte stav simulace; jediný zápis do ní jsou
příkazy hráče přes veřejné metody simulace (stavba budov).

| Složka | Zodpovědnost |
|---|---|
| `Screens/` | Zásobník obrazovek + `MenuBackground` (živé město za menu) a rolovací devlog. Herní HUD: kategorizované stavební menu s indikací dostupnosti, sledovač cílů, tlačítka rychlých akcí. Overlaye: detail/vylepšení budovy, výzkum, osady, Vzestup, úkoly, achievementy. Texty přes `Localization`. |
| `Rendering/` | Nekonečná kamera; chunkovaný terén (`TerrainRenderer`), cesty, budovy (sprity) + ghost; těžitelné stromy/kameny s animací kácení; jmenovky osad; den/noc + noční světla; dekorace s LOD; minimapa a toasty (oznámení ze simulace jako data). |
| `Rendering/Effects/` | Juice a život: pooled částice, popupy („+2"), ambientní fauna a agenti (chodci, vozíky) spawnovaní jen u kamery — čistě vizuální. |
| `Audio/` | Procedurální placeholder zvuky (sek, žuch) s náhodným pitchem; bez audio zařízení se tiše vypnou. |
| `Rendering/Sprites/` | Procedurální sprity a ikony (`SpriteLibrary`) — suroviny, budovy, stromy/kameny, agenti; dokud nejsou hotové assety. |
| `Input/` | Snímkový stav klávesnice/myši (stisknuto vs. drženo, delta kolečka, klik vs. tažení). |

MVP vykreslení mapy: 1 texel = 1 dlaždice (barvy biomů z JSON), jeden draw call se
škálováním — culling i LOD řeší GPU. Budovy jsou barevné obdélníky s cullingem
podle výřezu kamery. Až budou sprity, nahradí to chunky + texture atlas.
UI staví na knihovně Myra (default dle tech-stack.md) s vestavěným fontem —
projekt tak nepotřebuje MonoGame content pipeline. Nastavení grafiky (rozlišení,
režim okna, VSync) aplikuje `CivDleGame` na `GraphicsDeviceManager`.
