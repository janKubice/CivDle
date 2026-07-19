# Content Design — Budovy a infrastruktura

*Pracovní dokument · verze 0.1*

Návrh budov a dopravní infrastruktury. Navazuje na dokument **Content Design** (suroviny T0–T6, práce, biomy, eventy) a na **Tech Stack** (hustota populace = klíč k „milionům na mapě"). Co bylo ponecháno na doplnění, je označeno **[návrh]**.

---

## 0. Návrhová filozofie budov

Každá budova je definovaná čtyřmi parametry, ať je systém konzistentní:

| Parametr | Co znamená | Proč |
|---|---|---|
| **Footprint** | kolik místa na mapě zabírá | řídí hustotu (viz mrakodrapy) |
| **Sloty** | kolik dělníků / obyvatel pojme | napojení na práce a populaci |
| **Upkeep** | co spotřebovává za tik (flow) | soft-pressure hlídání |
| **Vstup → výstup** | co bere a co vyrábí | výrobní řetězec |

Tři vůdčí principy tohoto dokumentu:

1. **Hustota vypráví příběh pokroku.** Kamenný domek = pár lidí na velké ploše. Mrakodrap = tisíce lidí na malé ploše. Vyšší tiery balí víc populace do menšího místa → „miliony obyvatel" jde zobrazit bez nekonečného rozlézání (a sedí to na LOD renderer z tech doku).
2. **Spojení není dekorace, je podmínka.** Kolonie bez napojení na síť žije jen z místních surovin. Silnice, koleje, přístav nebo letiště jsou to, co ji zapojí do impéria — a co mapu vizuálně oživí (viz sekce 4).
3. **Občanské budovy tlumí eventy.** Hasičárna → menší dopad požárů, nemocnice → menší dopad nemocí. Tím se katastrofy z minulého doku propojí s tím, co hráč staví.

---

## 1. Obytné budovy

Osa: **vyšší tier = víc obyvatel na menší footprint, ale dražší materiál a vyšší nároky na služby** (spokojenost). Bez trhu, vody a luxusu ti do mrakodrapu nikdo nenastěhuje.

| Tier | Budova | Kapacita | Footprint | Vstup (stavba) | Éra |
|---|---|---|---|---|---|
| R0 | Přístřešek / stan **[návrh]** | velmi malá | malý | dřevo | start |
| R1 | Kamenný domek | malá | velký | kámen, dřevo | T0–T1 |
| R2 | Cihlový dům | střední | velký | cihly, prkna | T1 |
| R3 | Řadové domy **[návrh]** | střední+ | střední | cihly, sklo | T2 |
| R4 | Činžák **[návrh]** | vysoká | střední | ocel, sklo, cihly | T3–T4 |
| R5 | Panelák | vysoká++ | malý (na kapacitu) | beton, ocel | T4–T5 |
| R6 | Mrakodrap | obrovská | malý (na kapacitu) | ocel, sklo, elektronika | T5–T6 |
| R7 | Arkologie / mega-struktura **[návrh]** | extrémní | malý | kompozity, energočlánky | T6 |

> **Design pointa:** hráče nenutíš bourat staré domky — ty zůstávají užitečné na okrajích a v nových koloniích. Vyšší tiery jsou volba „chci hustotu v centru", ne povinný upgrade všeho. Míň frustrace, víc rozhodnutí.

---

## 2. Průmyslové budovy

Pracoviště z dokumentu prací. Každé má sloty pro dělníky a mění vstup na výstup. Vyvíjejí se v tierech a lze je **automatizovat upgrady** (méně dělníků / vyšší výstup).

### 2.1 Sběr surovin

| Budova | Práce | Výstup | Biom |
|---|---|---|---|
| Dřevorubecká chata → pila-dvůr | dřevorubec | dřevo | les, nížina |
| Farma → velkofarma | farmář | jídlo | nížina |
| Rybářská chata → přístav-rybárna | rybář | jídlo | pobřeží, řeka |
| Lovecká chata **[návrh]** | lovec | maso, kůže | les |
| Důl → hlubinný důl | horník | ruda, uhlí, kámen | hory |
| Lom | horník | kámen | hory, nížina |
| Hliniště **[návrh]** | kopáč | hlína | bažina, nížina |
| Ropná věž / vrt **[návrh]** | dělník | ropa | poušť, tundra, moře |

### 2.2 Zpracování

| Budova | Vstup → výstup | Éra |
|---|---|---|
| Pila | dřevo → prkna | T1 |
| Cihelna | hlína → cihly | T1 |
| Milíř **[návrh]** | dřevo → dřevěné uhlí | T1 |
| Huť / slévárna | ruda + palivo → bronz / železo | T2–T3 |
| Ocelárna | železo + uhlí → ocel | T4 |
| Sklárna **[návrh]** | písek → sklo | T4 |
| Betonárna **[návrh]** | kámen + cement → beton | T4 |
| Rafinerie **[návrh]** | ropa → palivo, plast | T5 |
| Továrna | suroviny → zboží, elektronika | T5 |
| High-tech fab **[návrh]** | + výzkum → čipy, kompozity | T6 |

### 2.3 Energie a sklady

| Budova | Role | Éra |
|---|---|---|
| Sklad / silo | buffer pro stock suroviny (kapacita zásob) | od začátku |
| Vodárna / studna | voda (flow) | T1+ |
| Elektrárna (uhelná → jaderná → fúzní) **[návrh]** | energie (flow) | T4+ |
| Solární / větrná farma **[návrh]** | čistá energie | T5+ (poušť = bonus) |

> **Sklady jsou tichý hrdina soft-pressure smyčky:** čím větší kapacita, tím déle vydrží kolonie bez dodávek, když se něco stane. Hráč, který staví sklady, si kupuje klid.

---

## 3. Občanské budovy (služby pro lidi)

Zvyšují spokojenost, umožňují růst populace, tlumí eventy a generují peníze. Bez nich vyšší obytné tiery nefungují.

| Budova | Efekt | Éra |
|---|---|---|
| Radnice | centrum kolonie, administrativa, dosah služeb | start |
| Trh | peníze, distribuce zboží, spokojenost | T1 |
| Sýpka | skladování jídla, pojistka proti hladu | T1 |
| Studna / vodárna | voda pro obyvatele | T1 |
| Škola → univerzita **[návrh]** | vychovává specialisty (vědci, inženýři) | T2+ |
| Léčitel → nemocnice **[návrh]** | **tlumí event „nemoc"**, zdraví | T2+ |
| Hasičská zbrojnice **[návrh]** | **tlumí event „požár"** | T3+ |
| Banka **[návrh]** | úroky, větší finanční kapacita | T3+ |
| Hospoda / taverna **[návrh]** | spokojenost, luxus | T1+ |
| Divadlo | spokojenost, luxus | T3+ |
| Park **[návrh]** | spokojenost, footprint-levný luxus | T2+ |
| Chrám / kostel **[návrh]** | spokojenost, morálka | T1+ |
| Muzeum / stadion **[návrh]** | pozdní luxus, turistika = peníze | T5+ |

> **Propojení s eventy:** občanské budovy dávají katastrofám smysl. Hráč, který postavil hasičárnu a nemocnici, cítí, že jeho příprava měla efekt — a ten, kdo je zanedbal, dostane jen mírný políček, ne zničené město. (Anti-frustrace zůstává.)

---

## 4. Transport a infrastruktura *(jádro tohoto dokumentu)*

Tady žije tvoje pointa: **kolonii v horách nezaložíš jen tak — musí k ní vést spojení.** Z toho dělám plnohodnotnou mechaniku, ne kulisu.

### 4.1 Pravidlo spojení

> **[ROZHODNUTO]** Kolonii lze založit ve chvíli, kdy k ní **vede alespoň rozestavěná cesta** (silnice / koleje / linka). Nemusí být hotová — stačí, že se k ní staví. Bez rozestavěného spojení místo nezaložíš.

Dokud se cesta staví, kolonie žije z místních surovin. Jakmile je link hotový, kolonie se plně zapojí do sítě a začne sdílet materiál, lidi a zboží se zbytkem impéria. Výhody tohoto řešení:

- **Nefrustruje, ale zavazuje** — nemusíš čekat na hotovou cestu, ale musíš se ke spojení zavázat (bez rozestavěné trasy to nejde).
- **Motivuje expanzi** — bohatý horský biom láká, ale musíš k němu prorazit cestu.
- **Oživuje mapu** — po spojnicích se pohybují vozidla (viz 4.4).

### 4.2 Typy spojení

| Typ | Vyžaduje | Propustnost | Dosah / terén | Éra |
|---|---|---|---|---|
| **Silnice** | volný terén (mosty přes řeku) | nízká–střední | krátký–střední, obchází hory těžko | od začátku |
| **Železnice** | nádraží + koleje | vysoká (hromadné) | dlouhý po souši, tunely přes hory | T3+ |
| **Přístav / loď** | voda (řeka, moře) | vysoká (hromadné) | přes vodu, mezi kontinenty | T2+ |
| **Letiště / letadla** | letiště na obou stranách | nízká, ale **ignoruje terén** | nejdelší, izolovaná místa | T5+ |

**Návaznost na biomy:**
- Ostrov nebo kolonie za mořem → jedině **loď** nebo **letadlo**.
- Zapadlé horské údolí → **železnice** (tunel) nebo **letadlo**.
- Sousední nížina → stačí **silnice**.

Tím se volba dopravy stává rozhodnutím podle terénu, ne kosmetikou.

### 4.3 Tiery a propustnost

Spojení se dá vylepšovat, když je kolonie přeroste:

```
Silnice:   pěšina ──► dlážděná ──► dálnice
Železnice: jednokolejka ──► dvojkolejka ──► vysokorychlostní
Voda:      molo ──► přístav ──► velký terminál
Vzduch:    přistávací pruh ──► letiště ──► mezinárodní hub
```

**Soft-pressure ve dopravě:** když kolonie produkuje víc, než link unese, přebytek se hromadí ve skladu a nakonec „ucpe" — signál, že je čas upgradovat trasu. Žádná penalizace, jen viditelná fronta.

### 4.4 Vozidla = vizuální i funkční vrstva

Po spojnicích se pohybují vozidla, která **reprezentují skutečný tok surovin** (nebo aspoň tak působí — viz tech dok, render je iluze):

| Spojení | Vozidla (dle éry) |
|---|---|
| Silnice | povozy → náklaďáky, autobusy, osobní auta |
| Železnice | nákladní i osobní vlaky |
| Voda | čluny → nákladní lodě, trajekty |
| Vzduch | nákladní i osobní letadla, později vzducholodě / drony **[návrh]** |

**Osobní doprava [ROZHODNUTO: kosmetika]:** kromě nákladu jezdí i lidé (auta ve městě, autobusy, vlaky plné cestujících) — čistě vizuálně, bez mechaniky dojíždění. Přesně tohle dělá mapu „živou", jak zněl původní vizuál, a nestojí to žádnou herní složitost. LOD to řeší: zblízka jednotlivá vozidla, z dálky jen proudy světel a pohybu.

**Vozidla vozí tok jen vizuálně [ROZHODNUTO]:** vozidla naznačují pohyb surovin, ale nejsou reálná logistika (žádné „náklaďák fyzicky veze 4 železa"). Levné na výkon i design, bohaté na dojem. Reálnou logistiku lze doplnit později, když by hra volala po hloubce.

### 4.5 Sklady a logistické uzly

- **Depo / překladiště [návrh]** — uzel, kde se přepojí silnice ↔ železnice ↔ přístav. Bez něj nejde kombinovat typy dopravy.
- **Sklady** (ze sekce 2.3) fungují jako buffer na koncích tras — vyrovnávají výkyvy v dodávkách.

---

## 5. Megastruktury **[návrh]**

Landmark stavby, které **zásadně zasáhnou krajinu** i ekonomiku. Jsou to pozdní cíle a prestižní mety idle progrese — obří cena, dlouhá vícefázová stavba s viditelným postupem (kostra → částečné → dokončeno) a **globální efekt** (bez radiusu, sedí na rozhodnutí „bez dosahu"). Některé přímo mění terén nebo biom — to je ono „fakt vizuálně zasáhne krajinu".

| Megastruktura | Vizuální dopad | Efekt | Éra |
|---|---|---|---|
| Velká přehrada | zatopí údolí, změní řeku v jezero | obří energie + voda | T4+ |
| Velký kanál | rozřízne pevninu, propojí dvě moře | nová lodní trasa napříč mapou | T4+ |
| Obří viadukt / most | překlene biomy a rokle | doprava přes jinak nepřístupný terén | T3+ |
| Mega solární pole | pokryje kus pouště panely | masivní čistá energie | T5+ |
| Kosmodrom | startující rakety (spektákl) | prestiž, výzkum, pozdní cíl | T5+ |
| Fúzní reaktor | zářící komplex dominující kraji | obrovská čistá energie | T6 |
| Výzkumný prstenec (urychlovač) | obří kruh vrytý do krajiny | velký boost výzkumu | T6 |
| Orbitální výtah | lano mizící v nebi přes celou obrazovku | obchod + výzkum, symbol civilizace | T6 |
| **Terraformer** | postupně **mění biom** (poušť → zeleň) | přepíše vlastnosti biomu okolo | T6 |
| Monument / div světa | dominanta panoramatu | globální spokojenost + turistika (peníze) | libovolná |

**Design zásady megastruktur:**

- **Vícefázová stavba s viditelným postupem** — hráč sleduje, jak roste. Payoff za dlouhé idle spoření surovin.
- **Mění krajinu, ne jen stojí na ní** — přehrada zatopí údolí, terraformer ozelení poušť, kanál rozřízne pevninu. To je ten „wow" moment při oddálení.
- **Unikátnost = prestiž** — divy světa klidně jen jednou za hru. Dává to koloniím identitu.
- **Globální bonus** — efekt platí pro celé impérium (konzistentní s „bez dosahu").

> **Pozn. k implementaci:** efekty typu „zatop údolí" nebo „změň biom" jsou terénní zásahy — v datech se popíšou jako `terrainEffect` odkaz, ale samotná změna terénu je kód (viz dokument Data-Driven Content, sekce o hranici dat vs. logiky).

---

## 6. Jak budovy oživují svět (poznámka k renderu)

Propojení s **Tech Stack** dokumentem:

- **Hustota** vyšších obytných tierů = miliony lidí na malém footprintu → render nezahltíš rozlézáním.
- **Vozidla po spojnicích** = hlavní zdroj pohybu a „života" na mapě; LOD je zobrazuje jednotlivě zblízka a agregovaně z dálky.
- **Budovy jsou statické sprity**, pohyb dodávají panáčci a vozidla → laciné na render, bohaté na dojem.

---

## 7. Jak to zapadá do zbytku

```
BIOM  ──dává──►  SUROVINY
SUROVINY  ──zpracují──►  PRŮMYSLOVÉ BUDOVY (sloty pro PRÁCE)
PRÁCE  ──bydlí v──►  OBYTNÝCH BUDOVÁCH (hustota dle tieru)
OBYVATELÉ  ──potřebují──►  OBČANSKÉ BUDOVY (spokojenost, tlumí EVENTY)
KOLONIE  ──musí být──►  SPOJENA (silnice / kolej / loď / letadlo)
SPOJENÍ  ──oživuje mapu──►  VOZIDLA (vizuál + tok)
```

Celý loop budov: **zapoj kolonii → postav průmysl → ubytuj dělníky → přidej služby → napoj dopravu → sleduj, jak to žije → upgraduj hustotu a trasy**.

---

## 8. Rozhodnutá nastavení

| Otázka | Rozhodnutí |
|---|---|
| Striktnost pravidla spojení | **Stačí rozestavěná cesta** — kolonii založíš, jakmile se k ní staví spojení (nemusí být hotové). |
| Kolik obytných tierů do launche | **Celé R0–R7** včetně mrakodrapů a arkologie. |
| Vozidla vozí suroviny reálně, nebo vizuálně | **Jen vizuál toku** — vozidla naznačují pohyb, nejde o reálnou logistiku. |
| Služby mají radius, nebo globál | **Bez dosahu** — služby fungují globálně v rámci kolonie. |
| Osobní doprava | **Kosmetika** — lidé se pohybují jen pro dojem, bez mechaniky. |

Tato rozhodnutí zjednodušují hru směrem k relaxačnímu jádru (méně micromanagementu) a nechávají prostor pro pozdější prohloubení (reálná logistika, radiusy) jako volitelný update.

---

*Návrh k iteraci. Vše označené **[návrh]** je moje doplnění tvého zadání — klidně škrtej a přepisuj.*
