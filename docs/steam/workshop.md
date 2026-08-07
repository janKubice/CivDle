# Steam Workshop pro mody CivDle

Workshop je jediná část Steamu, která do hry sahá hlouběji než „odemkni
achievement" — stahuje soubory, které pak hra načítá jako obsah. Proto je tady
zvlášť.

---

## 1. Zapnutí Workshopu

Steamworks → *Application → Workshop → Configuration*.

| Položka | Hodnota | Proč |
|---|---|---|
| Enable Workshop | ✅ | |
| Workshop type | **Ready-To-Use Items** | Mody jsou hotové věci ke stažení, ne zdrojáky ke kompilaci |
| Enable item tags | ✅ | Bez tagů se v modech nedá hledat |
| Allow item collections | ✅ | Hráči si dělají balíčky modů |

### Tagy položek

Zadej přesně tyhle — hra podle nich mody v prohlížeči filtruje:

```
Buildings
Resources
Research
Events
Balance
Total Conversion
Translation
Cosmetic
```

---

## 2. Jak vypadá mod

Mod je **složka**, nic víc. Struktura:

```
muj-mod/
  mod.json           ← povinný manifest
  preview.png        ← náhled pro Workshop (max 1 MB, doporučeně 512×512)
  buildings.json     ← přebíjí/doplňuje základní data (vše volitelné)
  resources.json
  tech.json
  events.json
  lang/
    cs.json
    en.json
  sprites/
    moje-budova.png
```

`mod.json`:

```json
{
  "id": "muj_mod",
  "name": "Můj mod",
  "version": "1.0.0",
  "enabled": true
}
```

Datové soubory se **slučují** se základní hrou přes `JsonOverlay`: co má stejné
`id`, přepíše se; co je nové, přidá se. Modder tedy nemusí kopírovat celý
`buildings.json` kvůli jedné budově.

> **Pořadí modů rozhoduje.** Načítají se podle jména složky a pozdější přebíjí
> dřívější. Ve správci modů to jde přeskládat.

---

## 3. Nahrání položky

Steam nemá „nahraj složku" tlačítko — položka se zakládá přes API. Hra to dělá
sama (tlačítko *Publikovat na Workshop* ve správci modů), ale pro ruční nahrání
existuje oficiální **Workshop Item Uploader** ve Steamworks SDK:

```
tools/ContentBuilder/builder/steamcmd.exe +login <účet> ^
  +workshop_build_item ..\scripts\workshop_item.vdf +quit
```

`workshop_item.vdf`:

```vdf
"workshopitem"
{
    "appid"          "2345670"
    "publishedfileid" "0"          // 0 = založit novou položku
    "contentfolder"  "C:\\mods\\muj-mod"
    "previewfile"    "C:\\mods\\muj-mod\\preview.png"
    "visibility"     "0"           // 0 public, 1 friends, 2 private
    "title"          "Můj mod"
    "description"    "Přidává tři budovy pro pozdní hru."
    "changenote"     "První verze"
}
```

Po prvním nahrání ti Steam vrátí `publishedfileid` — **zapiš si ho** a příště ho
dej do souboru místo nuly, jinak založíš druhou položku.

---

## 4. Kam Steam mody stahuje

```
<Steam>/steamapps/workshop/content/<AppID>/<PublishedFileId>/
```

Hra musí tuhle složku prohledávat **navíc** ke své `mods/`. Rozhraní
`IWorkshopService` (viz `platform-integration.md`) vrací seznam cest a
`ModCatalog.Discover` je projde stejně jako lokální složku — pro zbytek hry
není mezi lokálním a workshopovým modem rozdíl.

---

## 5. Pravidla, o která se lidi řežou

- **Autorská práva.** Za obsah ručí ten, kdo ho nahraje. Do popisu Workshopu dej
  odkaz na pravidla a jasně napiš, že sprity z jiných her jsou důvod ke smazání.
- **Moderace.** Jako vydavatel můžeš položky skrývat. Steam sám maže jen jasné
  porušení pravidel — spam a kradený obsah řešíš ty.
- **Mody a achievementy.** Rozmysli si, jestli s modem půjde odemykat
  achievementy. Doporučení: **nechat jít**, protože zákaz lidi otravuje a idle
  hra nemá kompetitivní integritu, kterou by to poškodilo. Ale **žebříčky
  s aktivním modem vypni** — jinak se do nich dostanou čísla z upravených dat
  a přestanou dávat smysl. Hra to řeší přes `IPlatformServices.LeaderboardsAllowed`.

---

## 6. Testovací kolečko, než to pustíš mezi lidi

```
[ ] Vytvoř mod ve vestavěném editoru
[ ] Publikuj ho jako viditelný jen pro sebe (visibility = private)
[ ] Odhlaš odběr, smaž lokální složku
[ ] Přihlas odběr znovu → Steam ho musí stáhnout
[ ] Hra ho musí najít a načíst bez restartu Steamu
[ ] Vypni ho ve správci a ověř, že se obsah vrátí do původního stavu
[ ] Nahraj změnu (changenote) a ověř, že se aktualizace projeví
```
