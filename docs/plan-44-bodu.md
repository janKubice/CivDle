# Plán: 44 připomínek z hraní

Seznam z testování, rozdělený do dávek. Pořadí není podle čísel, ale podle
toho, **co nejvíc bolí** a co na sobě závisí.

Značky: `[ ]` čeká · `[~]` rozděláno · `[x]` hotovo

## Stav

| Dávka | Body | Stav |
|---|---|---|
| 0 — odpovědi | 1 | odpovězeno (kód není potřeba; chybí `docs/steam/code-signing.md`) |
| 1 — pády | 7, 6, 19, 40, 9, 32, 13 | **hotovo** |
| 2 — HUD | 3, 4, 5, 10, 11, 12, 25, 29, 30, 31, 33, 34, 35 | **hotovo** |
| 3 — nástroje | 24, 20, 23, 22, 26, 17, 27, 28 | **hotovo** |
| 3 — nástroje | 18 | **hotovo** (varianta C: smíšené zóny v datech, vlastní zóna v dávce 6) |
| 4 — budovy | 14, 15, 16, 41, 43 | **hotovo** |
| 5 — obsah | 8, 37, 36, 21, 38, 39, 42 | **hotovo** |
| 6 — velké funkce | 44, 2 | čeká |

**Zbývají 2 body z 44** (dávka 6).

---

## Dávka 0 — odpovědi, ne kód

### 1. Avast při prvním spuštění — bude to i na Steamu?

**Ano, bude.** Antivirus nereaguje na to, odkud se hra stáhla, ale na to, že je
to **nepodepsaná binárka od neznámého vydavatele**. Steam na tom nic nemění:
soubor se stáhne do `steamapps/common/` a Defender SmartScreen i Avast ho vidí
stejně jako když si ho stáhneš z webu. U .NET her je to ještě častější, protože
self-contained build vypadá jako „velký neznámý spustitelný soubor".

Co s tím, seřazeno podle účinnosti:

| Krok | Cena | Účinek |
|---|---|---|
| **EV code signing certifikát** | ~400–600 USD/rok | Okamžitá reputace u SmartScreen. Nejjistější. |
| **OV code signing certifikát** | ~200–400 USD/rok | Reputace se buduje postupně (týdny, podle počtu stažení). |
| Nahlásit false positive u Avastu | zdarma | Vyřeší jednu konkrétní detekci, ne příští build. |
| Nic nedělat | 0 | Část hráčů hru nespustí a napíše negativní recenzi. |

**Doporučení:** OV certifikát stačí, pokud vydáváš do early accessu a počítáš
s tím, že první týdny budou hlášení. EV kup, pokud chceš klidné vydání.
Podepisuje se `signtool.exe` na výsledné `.exe` **před** nahráním do depotu.

Detaily a přesné příkazy: `docs/steam/code-signing.md` (bude doplněno v dávce 1).

---

## Dávka 1 — pády a věci, co nefungují

Nejvyšší priorita: hráč přijde o partii nebo mechanika tiše nedělá nic.

- [x] **7.** Načítání občas spadne
- [x] **6.** „Pokračovat" ukazuje tři tečky → vypadá to jako zamrznutí; nahradit
      ukazatelem postupu
- [x] **19.** Terraformace změní dlaždici, ale ne vizuál
- [x] **40.** Potopa z modliteb nedělá vůbec nic
- [x] **9.** NPC města občas bez budov nebo na nevalidním místě
- [x] **32.** Žebříčky tvrdí „připojeno ke Steamu", i když připojeno není
      *(regrese, kterou jsem zavedl: `IsAvailable` u lokální platformy vrací
      true schválně, ale obrazovka to čte jako „jsme na Steamu")*
- [x] **13.** Radar a pátrací balon neodhalují mapu (nebo extrémně pomalu)

## Dávka 2 — HUD, čitelnost, ikonky

Rychlé a hodně viditelné. Skoro vše je UI, málo rizika.

- [x] **3.** Chybí ikonka aplikace v hlavním panelu Windows
- [x] **4.** Do menu logo CivDle místo malého nápisu + podtitulu
- [x] **5.** „Novinky" zvětšit
- [x] **10.** „Civilisation might" pryč z prostředka obrazovky → do panelu statů
- [x] **11.** Panel surovin zasahuje do pravého okna se staty
- [x] **12.** Ikona rychlosti je stejná pro 2× i 3×
- [x] **29.** Dvě tlačítka pro sázení
- [x] **25.** Build menu se nevejde, když je odemčeno hodně budov
- [x] **30.** Ikona úkolů ukazuje, kolik je dostupných a kolik ke splnění
- [x] **31.** Ikona výzkumu ukazuje, kolik jde teď vyzkoumat
- [x] **33.** Tlačítko voleb indikuje, že volby běží
- [x] **34.** U koupeného města dvojitý nápis (žlutý „(tvoje)" i bílý)
- [x] **35.** Hlášky guvernéra přes celou obrazovku → malý seznam vpravo

## Dávka 3 — nástroje a stavění

- [x] **24.** Zóny a terraformaci schovat pod jednu ikonu s podmenu (jako stavění)
- [x] **20.** Terraformace tažením
- [x] **23.** Sázení tažením + hezčí výběr, co sázet
- [x] **22.** Terraformace: efekt a zvuk
- [x] **26.** Hromadné stavění (×25) staví budovy, ale bez silnic
- [x] **17.** Guvernér plní zóny obřími bloky bez cest
- [ ] **18.** Univerzální zóny (bydlení+parky, průmysl+těžba) nebo vlastní zóna
      z nastavení
- [x] **27.** Cesta k cizímu městu nejde postavit; má se zeptat, ze kterého
      mého města ji vést
- [x] **28.** Automatická cesta mezi městy musí najít hezkou validní trasu

## Upřesnění z hraní (8. 8.)

- **16** — „vylepšovat jako 1×1" znamená *stejné chování* jako u řetězce
  vylepšení 1×1 domku. Půdorys zůstává 2×2; nemá se zmenšovat.
- **Velké dílo (sink na přebytky)** — nemá to být položka v menu. Má za tím být
  **výzkum** a pak **obří budova**: díra do země, která opravdu vypadá jako
  bezedný sink. Řeší se v dávce 4 spolu s velkými půdorysy (body 15, 43), aby
  se velké stavby dělaly jednou.
- **×N a tažení** — mřížka ulic platí pro ×N (automat si vybírá místo), ne pro
  tažení (obdélník kreslí hráč). Opraveno.

### Nález: startovní domek se nikdy nepostavil

`gameplay.json` má `startingBuildings: ["cottage"]`, jenže `cottage` byl
`buildable: false` — a `CanPlace` na to hlídá. Startovní domek proto **tiše
nevznikl**; hra vždycky začínala na prázdné louce.

Vyšlo to najevo u bodu 14: jakmile se vyšší stupně domů udělají stavitelné,
startovní chalupa se začne stavět a 22 testů spadne na tom, že město má o jednu
budovu víc, než čekaly.

**Rozhodnuto (B):** hra začíná na prázdné louce, `startingBuildings` je prázdné.
Vyšší stupně domů jsou teď stavitelné, ale zamčené výzkumem podle éry
(masonry → chalupa, iron_working → cihlový dům, steam_power → činžák,
electrification → byty, robotics → mrakodrap, fusion → arkologie) — takže
„rovnou" neznamená „hned".

## Dávka 4 — budovy a progrese

- [x] **14.** V dalších érách jde rovnou stavět lepší domky, ne jen základní
      a vylepšovat
- [x] **15.** Větší budovy aspoň 2×2, nejvyšší tier domku klidně 3×3
- [x] **16.** Sloučený dům musí jít vylepšovat na další tiery jako 1×1
- [x] **41.** Čím větší NPC město, tím dražší dary a odkoupení (a víc budov
      na převzetí)
- [x] **43.** Velký přístav pro zámořské lodě

## Dávka 5 — obsah a mechaniky

- [x] **8.** Debug menu: přidávání bodů Vzestupu
- [x] **37.** Guvernér je pomalý → výzkumy a vylepšení na jeho rychlost
      *(Veřejné práce a Územní plán, +35 % každý)*
- [x] **36.** Výzkumy s obecným popiskem („trochu něco zvětší") → konkrétní
      popis a víc úrovní *(osmnáct uzlů je teď opakovatelných, `maxLevel` 5)*
- [x] **21.** Těžební technologie i pro nižší éry (dynamit…) + komba, ať se
      vyplatí občas těžit ručně i v pozdní hře *(7 těžebních + 4 na sílu série)*
- [x] **38.** Meteor dramatičtější: výbuch, efekt, ničení budov i při špatném
      kliknutí *(nevyslyšená rána spadne VEDLE — výsledek `Strayed`)*
- [x] **39.** Po meteoru zůstane radioaktivní půda → nová surovina, budovy
      a výzkumy okolo *(biom `fallout`, uran, tři budovy, tři výzkumy)*
- [x] **42.** Letadla a balony létají po mapě (jako rybářské lodičky)

### Velké dílo (upřesnění z hraní)

- [x] Sink není položka v menu: odemyká ho výzkum **Velký výkop** a stavba
      **Velké dílo** — jáma 5×5 se stupňovitými etážemi a jeřáby.

## Dávka 6 — velké samostatné funkce

- [ ] **44.** Šablony (templaty): hráč si uloží kus zástavby a staví ho znovu.
      Výzkum, ikona v menu, ghost náhled, nastavení.
- [ ] **2.** **Komplexní ingame content creator** — přepsat editor modů

### K bodu 2 podrobněji

Současný editor umí jen suroviny a budovy přes formulář. Zadání je jinde:
**klikací tvůrce obsahu**, kde si hráč vybere typ (budova, surovina, událost,
výzkum, fauna, jména měst, úkol…) a nakonfiguruje ho z toho, co hra zná.

Návrh postupu:

1. **Katalog typů obsahu.** Jeden popis na typ: jaká má pole, jakého typu,
   z čeho se vybírá (odkaz na suroviny / budovy / biomy…), co je povinné.
   Data, ne kód — jinak přidání typu znamená psát novou obrazovku.
2. **Obecný formulář nad katalogem.** Jedna obrazovka, která umí vykreslit
   pole podle popisu (číslo, text, výběr ze seznamu, seznam dvojic
   surovina+množství, přepínač). Přidat typ = přidat popis.
3. **Sprite editor** pro typy, které mají obrázek: mřížka 16×16 / 32×32,
   paleta, kbelík, guma, náhled na mapě.
4. **Kontrola a náhled** — už hotové (`ModValidator` pouští skutečný loader),
   jen navázat.
5. **Mod packy** — víc typů obsahu v jednom modu, seznam, mazání, editace
   existujícího modu (teď umí jen zakládat nový).

**Upřesnění od tebe (8. 8.):** typy obsahu, které tvůrce musí umět, jsou
*budova, surovina, událost, výzkum, fauna, jména měst, úkol* — a v principu
cokoliv dalšího, co je ve hře v datech. U každého typu se nastavují jeho
vlastní parametry z toho, co hra zná: u budovy velikost půdorysu, co vyrábí,
co spotřebuje, kolik dá bydlení a pracovních míst, cena, povolené biomy — a
protože budova má sprite, i kreslítko na ten sprite.

To potvrzuje bod 1 výše jako správný základ: bez katalogu typů v datech by
každý další typ znamenal novou ručně psanou obrazovku, a u sedmi typů se to
rozpadne. Odhad zůstává: **několik dní práce**, a je to největší jednotlivá
položka z celého seznamu.

Model (`ModDraft`, `ModValidator`) a testy z minula zůstávají — mění se UI
a rozšiřuje se katalog typů.

---

## Jak to budu dělat

- Jedna dávka = jeden commit (nebo pár tematických).
- Po každé dávce build + testy + smoke, pak teprve další.
- Body, které jsou ve skutečnosti návrh (18, 21, 39, 44), popíšu předem
  a až pak kódím — ať nestavím něco, co jsi nechtěl.
- Co se ukáže jako větší, než vypadalo, radši vypíchnu, než abych to udělal
  napůl.
