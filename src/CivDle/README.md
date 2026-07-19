# CivDle (spustitelná hra)

Render/UI vrstva nad `CivDle.Core`. Čte stav simulace, nikdy do něj nezapisuje (tech-stack.md).

| Složka | Zodpovědnost |
|---|---|
| `Screens/` | Zásobník obrazovek: hlavní menu → nová hra → hra (+ pauza jako overlay). |
| `Rendering/` | Kamera (pan/zoom) a vykreslení mapy. |
| `Input/` | Snímkový stav klávesnice/myši (stisknuto vs. drženo, delta kolečka…). |

MVP vykreslení mapy: 1 texel = 1 dlaždice (barvy biomů z JSON), jeden draw call se
škálováním — culling i LOD řeší GPU. Až budou sprity, nahradí to chunky + texture atlas.
UI staví na knihovně Myra (default dle tech-stack.md), včetně vestavěného fontu —
projekt tak nepotřebuje MonoGame content pipeline.
