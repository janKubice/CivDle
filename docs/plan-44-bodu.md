# Plán: 44 připomínek z hraní

Seznam z testování, rozdělený do dávek. Pořadí není podle čísel, ale podle
toho, **co nejvíc bolí** a co na sobě závisí.

Značky: `[ ]` čeká · `[~]` rozděláno · `[x]` hotovo

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

- [ ] **7.** Načítání občas spadne
- [ ] **6.** „Pokračovat" ukazuje tři tečky → vypadá to jako zamrznutí; nahradit
      ukazatelem postupu
- [ ] **19.** Terraformace změní dlaždici, ale ne vizuál
- [ ] **40.** Potopa z modliteb nedělá vůbec nic
- [ ] **9.** NPC města občas bez budov nebo na nevalidním místě
- [ ] **32.** Žebříčky tvrdí „připojeno ke Steamu", i když připojeno není
      *(regrese, kterou jsem zavedl: `IsAvailable` u lokální platformy vrací
      true schválně, ale obrazovka to čte jako „jsme na Steamu")*
- [ ] **13.** Radar a pátrací balon neodhalují mapu (nebo extrémně pomalu)

## Dávka 2 — HUD, čitelnost, ikonky

Rychlé a hodně viditelné. Skoro vše je UI, málo rizika.

- [ ] **3.** Chybí ikonka aplikace v hlavním panelu Windows
- [ ] **4.** Do menu logo CivDle místo malého nápisu + podtitulu
- [ ] **5.** „Novinky" zvětšit
- [ ] **10.** „Civilisation might" pryč z prostředka obrazovky → do panelu statů
- [ ] **11.** Panel surovin zasahuje do pravého okna se staty
- [ ] **12.** Ikona rychlosti je stejná pro 2× i 3×
- [ ] **29.** Dvě tlačítka pro sázení
- [ ] **25.** Build menu se nevejde, když je odemčeno hodně budov
- [ ] **30.** Ikona úkolů ukazuje, kolik je dostupných a kolik ke splnění
- [ ] **31.** Ikona výzkumu ukazuje, kolik jde teď vyzkoumat
- [ ] **33.** Tlačítko voleb indikuje, že volby běží
- [ ] **34.** U koupeného města dvojitý nápis (žlutý „(tvoje)" i bílý)
- [ ] **35.** Hlášky guvernéra přes celou obrazovku → malý seznam vpravo

## Dávka 3 — nástroje a stavění

- [ ] **24.** Zóny a terraformaci schovat pod jednu ikonu s podmenu (jako stavění)
- [ ] **20.** Terraformace tažením
- [ ] **23.** Sázení tažením + hezčí výběr, co sázet
- [ ] **22.** Terraformace: efekt a zvuk
- [ ] **26.** Hromadné stavění (×25) staví budovy, ale bez silnic
- [ ] **17.** Guvernér plní zóny obřími bloky bez cest
- [ ] **18.** Univerzální zóny (bydlení+parky, průmysl+těžba) nebo vlastní zóna
      z nastavení
- [ ] **27.** Cesta k cizímu městu nejde postavit; má se zeptat, ze kterého
      mého města ji vést
- [ ] **28.** Automatická cesta mezi městy musí najít hezkou validní trasu

## Dávka 4 — budovy a progrese

- [ ] **14.** V dalších érách jde rovnou stavět lepší domky, ne jen základní
      a vylepšovat
- [ ] **15.** Větší budovy aspoň 2×2, nejvyšší tier domku klidně 3×3
- [ ] **16.** Sloučený dům musí jít vylepšovat na další tiery jako 1×1
- [ ] **41.** Čím větší NPC město, tím dražší dary a odkoupení (a víc budov
      na převzetí)
- [ ] **43.** Velký přístav pro zámořské lodě

## Dávka 5 — obsah a mechaniky

- [ ] **8.** Debug menu: přidávání bodů Vzestupu
- [ ] **37.** Guvernér je pomalý → výzkumy a vylepšení na jeho rychlost
- [ ] **36.** Výzkumy s obecným popiskem („trochu něco zvětší") → konkrétní
      popis a víc úrovní
- [ ] **21.** Těžební technologie i pro nižší éry (dynamit…) + komba, ať se
      vyplatí občas těžit ručně i v pozdní hře
- [ ] **38.** Meteor dramatičtější: výbuch, efekt, ničení budov i při špatném
      kliknutí
- [ ] **39.** Po meteoru zůstane radioaktivní půda → nová surovina, budovy
      a výzkumy okolo
- [ ] **42.** Letadla a balony létají po mapě (jako rybářské lodičky)

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

Tohle je samo o sobě práce na několik dní. Model (`ModDraft`, `ModValidator`)
a testy z minula zůstávají — mění se UI a rozšiřuje se katalog typů.

---

## Jak to budu dělat

- Jedna dávka = jeden commit (nebo pár tematických).
- Po každé dávce build + testy + smoke, pak teprve další.
- Body, které jsou ve skutečnosti návrh (18, 21, 39, 44), popíšu předem
  a až pak kódím — ať nestavím něco, co jsi nechtěl.
- Co se ukáže jako větší, než vypadalo, radši vypíchnu, než abych to udělal
  napůl.
