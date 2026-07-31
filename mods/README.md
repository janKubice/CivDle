# Mody

Každý mod je složka s `mod.json` a datovými soubory, které se **vrství** na
základní hru z `data/`. Mod nemusí dodat celý soubor — stačí mu položka, kterou
přidává nebo mění. Díky tomu ho aktualizace hry nepřepíše a dva mody jdou
použít naráz.

## Jak to funguje

Soubor v modu se slévá se stejnojmenným souborem v `data/`:

- **Pole položek s `id`** se slévají podle `id`. Shodné ID přepíše původní
  záznam (na jeho místě, ať se nemění pořadí), nové ID se připojí na konec.
- **Pole bez `id`** (barvy, seznamy jmen) mod nahradí celá — není co s čím párovat.
- **Objekty** se slévají po klíčích do hloubky, takže mod smí změnit jediné
  číslo v `gameplay.json` a zbytku se nedotknout.

Mody se uplatňují v abecedním pořadí složek: kdo je později, přebíjí.

## mod.json

```json
{
  "id": "muj-mod",
  "name": "Můj mod",
  "version": "1.0",
  "enabled": true
}
```

`enabled: false` mod nechá ve složce, ale hra ho nenačte.

## Příklad

Zlevnit dům a přidat novou budovu (`buildings.json` v modu):

```json
{
  "schemaVersion": 1,
  "buildings": [
    { "id": "house", "buildCost": { "wood": 3 } },
    { "id": "observatory", "category": "civic", "mapColor": "#8FA9D6",
      "footprint": [2, 2], "buildCost": { "planks": 30, "stone": 40 },
      "allowedBiomes": ["grassland"] }
  ]
}
```

Nová budova potřebuje jméno v jazycích — `lang/cs.json` v modu stačí s jediným
klíčem, zbytek se vezme ze základního jazyka:

```json
{
  "schemaVersion": 1, "id": "cs", "nativeName": "Čeština",
  "strings": { "building.observatory": "Hvězdárna" }
}
```

Hotový příklad je ve složce `priklad/` (vypnutý — přepni `enabled` na `true`).

## Co mod zatím neumí

Nový **kód** ne: chování za behavior-ID (efekty upgradů, podívané megastruktur,
druhy terraformace) musí existovat ve hře. Mod smí libovolně kombinovat a ladit
to, co hra umí — čísla, obsah, texty, jazyky.
