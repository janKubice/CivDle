# CivDle (spustitelná hra)

Render/UI vrstva nad `CivDle.Core`. Čte stav simulace; jediný zápis do ní jsou
příkazy hráče přes veřejné metody simulace (stavba budov).

| Složka | Zodpovědnost |
|---|---|
| `Screens/` | Zásobník obrazovek: hlavní menu → nová hra / nastavení → hra (+ pauza jako overlay). Texty přes `Localization`; po změně jazyka se obrazovky přestaví (event). |
| `Rendering/` | Kamera (pan/zoom, viditelné meze), vykreslení mapy a budov + ghost náhled stavby. |
| `Input/` | Snímkový stav klávesnice/myši (stisknuto vs. drženo, delta kolečka, klik vs. tažení). |

MVP vykreslení mapy: 1 texel = 1 dlaždice (barvy biomů z JSON), jeden draw call se
škálováním — culling i LOD řeší GPU. Budovy jsou barevné obdélníky s cullingem
podle výřezu kamery. Až budou sprity, nahradí to chunky + texture atlas.
UI staví na knihovně Myra (default dle tech-stack.md) s vestavěným fontem —
projekt tak nepotřebuje MonoGame content pipeline. Nastavení grafiky (rozlišení,
režim okna, VSync) aplikuje `CivDleGame` na `GraphicsDeviceManager`.
