# Jak do hry zapojit skutečný Steam

Hra je postavená tak, že Steam **nepotřebuje**. Achievementy, statistiky
i rekordy fungují bez něj přes `LocalPlatformServices`. Tenhle dokument je
o tom, co udělat, až budeš mít App ID.

---

## Proč to takhle

Steamworks se nedá vyzkoušet bez zaplaceného App ID a běžícího klienta. Kdyby
na něm hra visela přímo:

- nešla by spustit na stroji bez Steamu (a ty na takovém vyvíjíš),
- nešla by testovat v CI (nativní knihovna, přihlášený uživatel),
- nešla by vydat nikde jinde (itch.io, GOG),
- a hráč mimo Steam by přišel o achievementy úplně.

Proto je mezi hrou a Steamem rozhraní `IPlatformServices` se čtyřmi
zodpovědnostmi: **achievementy, statistiky, žebříčky, mody**. Nic víc od
platformy hra nechce.

```
Simulace ──► PlatformCatalog ──► IPlatformServices ──┬─► LocalPlatformServices  (dnes)
                                                     └─► SteamPlatformServices  (až bude App ID)
```

`PlatformCatalog` je jediné místo, kde jsou API jména. Steamworks jména jsou
řetězce, které překladač nezkontroluje — překlep znamená achievement, který se
tiše nikdy neodemkne, a to se pozná až po vydání.

---

## Krok 1: knihovna

Doporučuju **Steamworks.NET** (MIT, tenká vrstva nad nativním SDK, aktivně
udržovaná, používá ji většina .NET her na Steamu).

```powershell
dotnet add src/CivDle package Steamworks.NET
```

Do `src/CivDle/CivDle.csproj` přidej, aby se nativní knihovna kopírovala vedle
binárky:

```xml
<ItemGroup>
  <None Update="steam_api64.dll" CopyToOutputDirectory="PreserveNewest" />
  <None Update="steam_appid.txt" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

`steam_api64.dll` je ze Steamworks SDK. `steam_appid.txt` obsahuje jen tvoje
App ID a slouží k testování mimo Steam klienta — **do release buildu nepatří**,
jinak si každý hráč může App ID přepsat.

> Tohle je jediná nová závislost, kterou projekt kvůli Steamu dostane, a je
> proti pravidlu „no balast" obhajitelná: bez nativního SDK Steam neexistuje
> a psát si vlastní P/Invoke vrstvu by bylo horší.

---

## Krok 2: implementace

Vytvoř `src/CivDle/Platform/SteamPlatformServices.cs`, který implementuje
`IPlatformServices`. Mapování je přímočaré:

| Rozhraní | Steamworks |
|---|---|
| `UnlockAchievement` | `SteamUserStats.SetAchievement(apiName)` |
| `IsAchievementUnlocked` | `SteamUserStats.GetAchievement(apiName, out bool)` |
| `SetStat(string, long)` | `SteamUserStats.SetStat(apiName, (int)value)` |
| `SetStat(string, double)` | `SteamUserStats.SetStat(apiName, (float)value)` |
| `GetStat` | `SteamUserStats.GetStat(...)` |
| `SubmitScore` | `SteamUserStats.FindOrCreateLeaderboard` → `UploadLeaderboardScore` |
| `TopScores` | `DownloadLeaderboardEntries(k_ELeaderboardDataRequestGlobal, 1, count)` |
| `PlayerName` | `SteamFriends.GetPersonaName()` |
| `WorkshopItems` | `SteamUGC.CreateQueryUserUGCRequest(...)` |
| `SubscribedModDirectories` | `SteamUGC.GetItemInstallInfo(...)` |
| `Flush` | `SteamUserStats.StoreStats()` |

Na co si dát pozor:

1. **`SteamAPI.Init()` může selhat** (Steam neběží, hráč není přihlášený).
   Když selže, **nepadej** — vrať se k `LocalPlatformServices`. Hra musí jít
   spustit vždycky.
2. **`SteamAPI.RunCallbacks()` musí běžet každý snímek**, jinak se ti nikdy
   nevrátí odpovědi na žebříčky. Patří do `CivDleGame.Update`.
3. **Žebříčky jsou asynchronní.** `TopScores` nemůže vrátit data hned —
   drž si cache, kterou plní callback, a vracej z ní. Obrazovka se stejně
   překresluje.
4. **Steam žebříčky berou jen `int32`.** ×N přeteče. Posílej `Math.Min(value,
   int.MaxValue)` a v žebříčku počítej s tím, že špička se jednou „zasekne"
   na maximu — nebo posílej logaritmus, když ti to nevadí formátovat.
5. **`StoreStats()` volej dávkově**, ne po každé změně. Steam má limit na
   frekvenci a při překročení tiše zahazuje.

### Kostra

```csharp
public sealed class SteamPlatformServices : IPlatformServices
{
    private readonly IPlatformServices _fallback;
    private readonly bool _ready;

    public SteamPlatformServices(IPlatformServices fallback)
    {
        _fallback = fallback;
        try
        {
            _ready = SteamAPI.Init();
        }
        catch (DllNotFoundException)
        {
            _ready = false; // nativní knihovna chybí — hra běží dál bez Steamu
        }

        if (_ready)
        {
            SteamUserStats.RequestCurrentStats();
        }
    }

    public bool IsAvailable => _ready;

    // Když Steam není, všechno propadne na lokální implementaci — hráč
    // o achievementy nepřijde jen proto, že spustil hru napřímo.
    public void UnlockAchievement(string apiName)
    {
        _fallback.UnlockAchievement(apiName);
        if (_ready)
        {
            SteamUserStats.SetAchievement(apiName);
        }
    }

    // … zbytek podle tabulky výše
}
```

Ve `CivDleGame` pak stačí:

```csharp
var local = new LocalPlatformServices(path) { PlayerName = Environment.UserName };
IPlatformServices platform = new SteamPlatformServices(local);
```

**Zapisuj do obou.** Lokální kopie je záloha pro případ, že hráč hru později
spustí mimo Steam — a při vývoji ti ušetří to, že Steam nemusí běžet.

---

## Krok 3: mody a žebříčky

`LeaderboardsAllowed` u Steam implementace vrať jako „žádný mod není zapnutý":

```csharp
public bool LeaderboardsAllowed => _activeModCount == 0;
```

Achievementy s modem nech jít. Zákaz hráče otravuje a idle hra nemá
kompetitivní integritu, kterou by to poškodilo — ale čísla z upravených dat ve
sdíleném žebříčku se zpětně vzít nedají.

---

## Krok 4: ověření

```
[ ] Hra se spustí, když Steam NEBĚŽÍ (musí spadnout na lokální implementaci)
[ ] Hra se spustí, když steam_api64.dll CHYBÍ
[ ] Achievement se odemkne a objeví se v overlayi
[ ] Statistika se objeví ve Steamworks → Stats po StoreStats()
[ ] Skóre se objeví v žebříčku a hra si stáhne špičku
[ ] Se zapnutým modem se do žebříčku NEODEŠLE nic
[ ] Odhlášení odběru modu na Workshopu ho odstraní i ze hry
```

Testy `PlatformCatalogTests` a `LocalPlatformServicesTests` běží dál — jsou
psané proti rozhraní, ne proti Steamu, takže Steam implementaci nijak nebrzdí.
