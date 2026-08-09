# Podepisování buildu (a proč na to Avast řve)

Odpověď na bod 1 ze seznamu z hraní: **ano, hlášení antiviru přijde i na
Steamu.** Antivirus nereaguje na to, odkud se soubor stáhl, ale na to, že je to
**nepodepsaná binárka od neznámého vydavatele**. Steam na tom nic nemění: hra se
stáhne do `steamapps/common/` a Defender SmartScreen i Avast ji vidí stejně jako
soubor z webu. U .NET her je to častější, protože self-contained build vypadá
jako velký neznámý spustitelný soubor bez historie.

## Co s tím, seřazeno podle účinku

| Krok | Cena | Účinek |
|---|---|---|
| **EV code signing certifikát** | ~400–600 USD/rok | Okamžitá reputace u SmartScreen. Nejjistější. |
| **OV code signing certifikát** | ~200–400 USD/rok | Reputace se buduje postupně (týdny, podle počtu stažení). |
| Nahlásit false positive u Avastu | zdarma | Vyřeší jednu konkrétní detekci, ne příští build. |
| Nedělat nic | 0 | Část hráčů hru nespustí a napíše negativní recenzi. |

**Doporučení:** OV stačí, pokud vydáváš do early accessu a počítáš s tím, že
první týdny budou hlášení. EV kup, pokud chceš klidné vydání.

## Odkud certifikát vzít

Certifikační autority, které code signing prodávají koncovým vývojářům:
DigiCert, Sectigo (dřív Comodo), GlobalSign, SSL.com. Prodejci jako
Certera/SignMyCode bývají levnější — je to tentýž certifikát od téže autority.

**Ověření identity trvá dny až týdny.** U OV chce autorita doklad o existenci
subjektu (u OSVČ typicky živnostenský rejstřík + telefonní ověření), u EV navíc
osobní/notářské ověření. **Začni s tím dřív, než budeš mít datum vydání** —
tohle je ta část, která se nedá uspěchat.

Od června 2023 musí soukromý klíč EV i OV certifikátu ležet na **hardwarovém
tokenu** (USB klíčenka) nebo v cloudovém HSM. Prakticky to znamená:

- podepisuje se z jednoho počítače, do kterého je token zapíchnutý,
- nebo se použije cloudové podepisování (Azure Trusted Signing, DigiCert
  KeyLocker) a podepisovat umí i CI.

## Jak se podepisuje

Podepisuje se **výsledné `.exe` (a vlastní DLL) PŘED nahráním do depotu.**
Nemá smysl podepisovat až to, co leží ve `steamapps` — tam se dostane přesně to,
co jsi nahrál.

```powershell
# 1) Build hry (self-contained, jak ji vydáváš)
dotnet publish src/CivDle/CivDle.csproj -c Release -r win-x64 --self-contained `
    -p:PublishSingleFile=false -o build\win-x64

# 2) Podpis. /fd a /td musí být sha256 — sha1 už autority neberou.
#    /tr je časové razítko: bez něj podpis přestane platit, až vyprší certifikát.
signtool sign /fd sha256 /td sha256 /tr http://timestamp.digicert.com `
    /a build\win-x64\CivDle.exe

# 3) Ověření (co uvidí Windows)
signtool verify /pa /v build\win-x64\CivDle.exe
```

`signtool.exe` je součástí Windows SDK
(`C:\Program Files (x86)\Windows Kits\10\bin\<verze>\x64\signtool.exe`).

S cloudovým podepisováním (Azure Trusted Signing) se místo `/a` použije
dlib provider:

```powershell
signtool sign /v /debug /fd sha256 /tr http://timestamp.acs.microsoft.com `
    /td sha256 /dlib "C:\ts\Azure.CodeSigning.Dlib.dll" `
    /dmdf "C:\ts\metadata.json" build\win-x64\CivDle.exe
```

### Co podepsat

- `CivDle.exe` — povinně.
- Vlastní DLL v balíku (`CivDle.Core.dll`) — doporučeně.
- Knihovny třetích stran (MonoGame, Myra) podepsané být nemusí; SmartScreen
  hodnotí spouštěný soubor.

## Nahlášení false positive

Když detekce přijde i s podpisem (stává se to u nových certifikátů), nahlas ji
— je to zdarma a vyřídí se řádově v hodinách až dnech:

- **Avast/AVG:** https://www.avast.com/false-positive-file-form.php
- **Microsoft Defender:** https://www.microsoft.com/en-us/wdsi/filesubmission
- **VirusTotal** napřed použij ke zjištění, kdo přesně to hlásí, ať neposíláš
  formuláře naslepo.

Do formuláře patří přesná verze souboru a jeho SHA-256 — po každém buildu je to
jiný soubor, takže nahlášení platí na tu jednu binárku.

## Steam a Windows navíc

- **Depot nahrávej podepsané soubory.** SteamPipe soubory nemění, podpis
  přežije.
- Do `steam_appid.txt` a build skriptů nic zvláštního není potřeba.
- **Nepodepsaný build klidně testuj** — hlášení antiviru je otravné, ale hru
  nespustitelnou nedělá. Podepisuj až to, co jde ven k hráčům.
