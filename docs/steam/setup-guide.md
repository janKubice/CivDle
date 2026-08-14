# Steamworks od nuly: co udělat a v jakém pořadí

Návod předpokládá, že hru vydáváš sám. Kroky jsou v pořadí, ve kterém je Steam
odemyká — přeskočit se nedají.

---

## 0. Než začneš (jednorázově, trvá dny)

1. **Partner účet** — <https://partner.steamgames.com>. Potřebuješ IČO nebo
   rodné číslo (daňový formulář), bankovní účet a doklad totožnosti.
2. **Daňový dotazník** (W-8BEN pro ČR). Bez něj ti Steam nevyplatí nic. Vyřízení
   trvá i týden.
3. **Zaplať poplatek za aplikaci** — 100 USD za App ID. Vrátí se ti, až hra
   vydělá 1 000 USD.
4. Steam ti přidělí **App ID** (číslo jako `2345670`). Zapiš si ho, budeš ho
   potřebovat všude.

> **Do App ID se nedá nic naprogramovat.** Steam achievementy, žebříčky ani
> Workshop nejdou testovat bez něj — proto je hra postavená tak, že běží úplně
> bez Steamu a Steam se do ní zapojí až potom. Viz `platform-integration.md`.

---

## 1. Založ App ID a základní nastavení

Steamworks → *Apps & Packages* → tvá aplikace.

| Sekce | Co vyplnit |
|---|---|
| Application → General | Jméno, podpora, jazyky |
| Application → Store Settings | Kategorie, žánry, tagy — vše z `tags-categories-assets.md` |
| Store Presence → Basic Info | Texty z `store-page-cs.md` a `store-page-en.md` |
| Store Presence → Graphical Assets | Kapsle vygenerované přes `--capsules` |
| Store Presence → Screenshots | Screenshoty z `--capture`, min. 5 |
| Store Presence → Trailers | Video (ne GIF) |

**Store stránku můžeš zveřejnit dřív, než je hra hotová** — a měl bys.
Wishlisty před vydáním jsou to jediné, co rozhoduje o viditelnosti v den vydání.
Steam vyžaduje **minimálně 2 týdny** mezi zveřejněním stránky a vydáním.

---

## 2. Depoty a nahrání buildu

**Depot** = balík souborů pro jednu platformu. Minimum je jeden.

1. *SteamPipe → Depots* → přidej depot (dostane ID `<AppID>+1`).
2. Stáhni **SteamCMD** a *Steamworks SDK*.
3. Vytvoř skript `app_build_<AppID>.vdf`:

```vdf
"appbuild"
{
    "appid"     "2345670"
    "desc"      "CivDle 0.1.0"
    "buildoutput" "..\\output\\"
    "contentroot" "..\\publish\\"
    "setlive"   ""              // prázdné = nenasadit rovnou, nastavíš ručně
    "depots"
    {
        "2345671"
        {
            "FileMapping"
            {
                "LocalPath" "*"
                "DepotPath" "."
                "recursive" "1"
            }
        }
    }
}
```

4. Publikuj hru a nahraj:

```powershell
dotnet publish src/CivDle/CivDle.csproj -c Release -r win-x64 --self-contained true -o publish
steamcmd +login <účet> +run_app_build ..\scripts\app_build_2345670.vdf +quit
```

5. Ve Steamworks → *Builds* přiřaď build na větev `default` a **Publikuj změny**.

> `--self-contained true` je tu schválně: hráč nemá mít povinnost instalovat
> .NET 8 runtime. Build je pak o ~70 MB větší a stojí to za to.

---

## 3. Achievementy

Steamworks → *Application → Achievements → Edit Achievements*.

Hra jich má **96** a všechny jsou vypsané v
`docs/steam/generated/achievements.csv` — sloupce `api_name`, `name_<jazyk>`,
`desc_<jazyk>`.

Postup:

1. Pro každý achievement klikni *New Achievement*.
2. **API Name** = hodnota ze sloupce `api_name` (např. `ACH_WOODCUTTER`).
   Musí sedět přesně — podle něj ho hra odemyká.
3. **Display Name / Description** — vlož z CSV. Steam podporuje překlady:
   přepni jazyk nahoře a vlož další sloupec.
4. **Icon (achieved)** = `docs/steam/generated/achievement-icons/<id>.png`
   **Icon (locked)** = `.../<id>-locked.png`
5. *Hidden* nech vypnuté u všech kromě těch, které prozrazují pozdní obsah
   (v CSV označené sloupcem `hidden`).

> Zadávat 96 achievementů ručně je asi hodina práce. Steamworks hromadný import
> **nemá** — CSV je tu proto, abys kopíroval, ne přepisoval.

**Po zadání** klikni *Publish* v sekci *Stats & Achievements*. Bez publikace se
achievementy z hry neodemknou ani v testu.

---

## 4. Statistiky

Statistiky jsou čísla za hráčem (Steam je synchronizuje mezi stroji) a jsou
**podmínkou pro žebříčky, které mají smysl**. Seznam je
v `docs/steam/generated/stats.csv`.

Steamworks → *Application → Stats*:

| API Name | Typ | Výchozí | Co znamená |
|---|---|---|---|
| `STAT_PEAK_POPULATION` | INT | 0 | Nejlidnatější město |
| `STAT_TOTAL_BUILDINGS` | INT | 0 | Postavených budov celkem |
| `STAT_ASCENSIONS` | INT | 0 | Počet Vzestupů |
| `STAT_LEGACIES` | INT | 0 | Počet zanechaných Odkazů |
| `STAT_GRAND_WORK_STAGE` | INT | 0 | Stupeň Velkého díla |
| `STAT_TOTAL_POWER` | FLOAT | 1 | Celkový násobič síly (×N) |
| `STAT_TECHS_RESEARCHED` | INT | 0 | Vyzkoumaných technologií |
| `STAT_CITIES_ABSORBED` | INT | 0 | Pohlcených cizích měst |
| `STAT_TILES_EXPLORED` | INT | 0 | Odkrytých dlaždic mapy |
| `STAT_PLAYTIME_SECONDS` | INT | 0 | Odehraný čas |

**Pozor na `INT` vs `FLOAT`:** ×N přeroste `int` velmi rychle, proto je FLOAT.
Steam u FLOAT statistik doporučuje `Max Change` nechat prázdné, jinak ti velké
skoky zahodí jako podezřelé.

---

## 5. Žebříčky

Steamworks → *Application → Leaderboards*. Vytvoř ručně, jeden po druhém.

| Název (API) | Sort | Display | Co měří |
|---|---|---|---|
| `LB_PEAK_POPULATION` | Descending | Numeric | Nejlidnatější město |
| `LB_TOTAL_POWER` | Descending | Numeric | Nejvyšší ×N |
| `LB_ASCENSIONS` | Descending | Numeric | Nejvíc Vzestupů |
| `LB_LEGACIES` | Descending | Numeric | Nejvíc Odkazů |
| `LB_GRAND_WORK` | Descending | Numeric | Nejdál ve Velkém díle |
| `LB_BUILDINGS` | Descending | Numeric | Nejvíc budov |
| `LB_CITIES_ABSORBED` | Descending | Numeric | Nejvíc pohlcených měst |
| `LB_FASTEST_ASCENSION` | Ascending | Time (ms) | Nejrychlejší první Vzestup |
| `LB_TILES_EXPLORED` | Descending | Numeric | Nejvíc prozkoumané mapy |
| `LB_DAILY_<YYYYMMDD>` | Descending | Numeric | Denní výzva (zakládá hra sama) |

**Denní žebříčky nezakládej ručně.** Steam umí `FindOrCreateLeaderboard` —
hra si žebříček pro dnešní výzvu vytvoří sama při prvním odeslání. Ruční zakládání
365 žebříčků ročně nedává smysl.

`LB_FASTEST_ASCENSION` je jediný **Ascending** (nižší = lepší). Uložený je
v milisekundách, protože Steam žebříčky berou jen `int32` — sekundy by byly moc
hrubé a minuty nečitelné.

---

## 6. Steam Cloud

Steamworks → *Application → Cloud*.

| Položka | Hodnota |
|---|---|
| Byte quota | 100 000 000 (100 MB) |
| Number of files | 200 |
| Root | `WinAppDataLocal` |
| Subdirectory | `CivDle` |
| Pattern | `*.sav` a `*.json` |

Save hry je sekce po sekcích a tolerantní k neznámým sekcím, takže cloud sync
mezi verzemi nerozbije rozehranou hru. Časosběry (`timelapses/`) do cloudu
**nedávej** — jsou velké a nejsou to postup.

---

## 7. Workshop

Viz samostatný `workshop.md` — má vlastní nastavení, typy položek a pravidla
pro moderaci.

---

## 8. Než pošleš hru na review

Steam kontroluje build ručně, trvá to 1–5 pracovních dní. Nejčastější důvody
zamítnutí, na které narazíš:

- **Zaškrtnutá kategorie, kterou build nemá.** Máš-li v kategoriích „Steam
  Achievements", musí se aspoň jeden opravdu odemknout.
- **Screenshoty nejsou ze hry.** Koláže, loga a texty přes celý obrázek Steam
  odmítá. Proto je `--capture` režim — fotí skutečnou hru.
- **Chybí Store stránka v angličtině.** Čeština nestačí ani pro české studio.
- **Hra nejde spustit z čistého systému.** Otestuj na stroji bez .NET SDK.
- **Nesouhlasí název** mezi App ID, store stránkou a spustitelným souborem.

---

## 9. Checklist před vydáním

```
[ ] App ID zaplacené, daňový formulář schválený
[ ] Store stránka zveřejněná ≥ 14 dní před vydáním
[ ] 5+ screenshotů z --capture, žádný s tutoriálem nebo chybovou hláškou
[ ] Trailer nahraný
[ ] Všechny kapsle ve správných rozměrech
[ ] 96 achievementů zadaných a PUBLIKOVANÝCH
[ ] 10 statistik zadaných
[ ] 9 žebříčků založených
[ ] Cloud nastavený a otestovaný na dvou strojích
[ ] Workshop zapnutý, testovací mod nahraný a stažený zpět
[ ] Build na větvi default, spuštěný z čisté Windows instalace
[ ] Cena nastavená ve všech měnách
[ ] Věkové hodnocení vyplněné
[ ] Review odeslaná ≥ 2 týdny před plánovaným vydáním
```

## Demoverze

Demo je **samostatný build**, ne přepínač za běhu:

```bash
./publish.sh win-x64 demo      # → dist/win-x64-demo
./publish.sh linux-x64 demo
```

O edici rozhoduje překladová konstanta (`src/CivDle/Edition.cs`), takže se plná
hra nemůže omylem tvářit jako demo ani naopak — na disku není nic, co by šlo
přejmenovat nebo smazat a tím edici přepnout. Že běží demo, se pozná i z konzole
(`edice: DEMO`).

### Co demo omezuje

| Věc | V demu |
|---|---|
| Obyvatelé | strop z `data/gameplay.json` → `demo.populationCap` (výchozí 10 000) |
| Vzestup | **první normální**, od druhého práh `demo.ascensionRequirement` |
| Strom výzkumu | `demo.techFraction` dílu (výchozí 20 %), vždy souvislý výřez |
| Mody | nenačítají se vůbec (ani sprity) |
| Achievementy, žebříčky | zamčené |

Meze jsou v **datech**, ne v kódu: demo se ladí podle toho, jak dlouho má trvat,
a překládat kvůli tomu hru je zbytečné. Blok `demo` v `gameplay.json` má i plná
hra — tam se jen ignoruje.

### Proč právě takhle

- **Výřez stromu je uzávěr přes předpoklady**, ne „prvních N v pořadí". Strom
  není psaný striktně od kořene, takže prostý řez by nechal v nabídce uzly, ke
  kterým v ukázce nevede cesta. Hlídá to test `TheCutKeepsTheTreeConnected`.
- **První Vzestup zůstává normální**, aby si hráč osahal mechaniku, na které hra
  stojí. Druhý je cíl, na kterém ukázka končí.
- **Zamčené kvůli demu vypadá jinak než nesplněná podmínka.** Zámek v popisce,
  teplá barva uzlu ve stromu, odznak „DEMO" v menu. Bez toho by hráč hledal ve
  hře cestu, která neexistuje.

