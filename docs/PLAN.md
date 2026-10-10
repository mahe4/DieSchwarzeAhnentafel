# DSA5 Character Creator – Projektplan

Stand: 2026-10-09

## 1. Ziele

- Heldenerschaffung **und** Steigerung nach dem Regelwerk *Das Schwarze Auge, 5. Edition*.
- Läuft auf **Windows, Linux und Android** mit einer gemeinsamen Codebasis.
- Am Desktop komfortabel (mehrspaltig, Maus/Tastatur), auf dem Smartphone mit eigenem Layout (einspaltig, Touch).
- **Alle Regelwerke** werden langfristig abgedeckt. Welche Quellen gelten, lässt sich pro Charakter ein- und ausschalten.
- PDF-Charakterbogen auf **allen** Plattformen, also auch auf Android.

### Lizenz-Grundsatz (verbindlich)

Es kommen **keine Fluff- oder Beschreibungstexte aus den Büchern** ins Projekt. Texte beschränken sich auf das, was im
offiziellen **Ulisses DSA Regel-Wiki** steht: <https://dsa.ulisses-regelwiki.de/>.
Das Wiki ist die **Quelle für alle Inhalte**, also für Werte, Regeltexte, Voraussetzungen, AP-Kosten und Publikationsangaben.
→ Arbeitsweise für die Datenerfassung: siehe Abschnitt 7 und `CLAUDE.md`.

## 2. Technologie

| Bereich | Wahl | Begründung |
|---|---|---|
| Sprache/Runtime | C# / .NET 10 | wie im SplittermondCharacterCreator |
| UI | **Avalonia 12** (Fluent Theme) | eine UI für Windows, Linux und Android |
| MVVM | CommunityToolkit.Mvvm | Source Generators, bewährt |
| PDF | **SkiaSharp `SKDocument`** (eigenes Bogen-Layout) | läuft auch auf Android; QuestPDF unterstützt Android nicht offiziell |
| Persistenz | System.Text.Json (Source Generator, AOT/Trimming-tauglich) | |
| Tests | xUnit v3 + Microsoft.Testing.Platform, Avalonia.Headless für UI-Smoke-Tests | |

### Solution-Struktur

```
DSACharacterCreator.slnx
├─ src/
│  ├─ DSACharacterCreator.Core/        # net10.0, keine UI-Abhängigkeit
│  │  ├─ Content/      # Datenmodelle der Regelinhalte + Loader + Quellenfilter
│  │  ├─ Characters/   # Charaktermodell (Entscheidungen, Protokoll)
│  │  ├─ Rules/        # Berechnung, Kosten, Voraussetzungen, Validierung
│  │  └─ Persistence/  # Speicherformat, Versionierung, Migration, (später) Optolith-Import
│  ├─ DSACharacterCreator.Content/     # nur eingebettete JSON-Regeldaten (data/**)
│  ├─ DSACharacterCreator.Pdf/         # Charakterbogen via SkiaSharp (wird erst in Meilenstein 7 angelegt)
│  ├─ DSACharacterCreator.App/         # Avalonia: Views, ViewModels, Styles (alle Plattformen)
│  ├─ DSACharacterCreator.Desktop/     # Startprojekt Windows/Linux
│  └─ DSACharacterCreator.Android/     # Startprojekt Android (net10.0-android)
├─ tests/
│  ├─ DSACharacterCreator.Core.Tests/
│  ├─ DSACharacterCreator.Content.Tests/  # Datenintegrität: IDs eindeutig, Referenzen auflösbar, Schema
│  └─ DSACharacterCreator.App.Tests/      # Avalonia.Headless: Layout-/UI-Smoke-Tests (Namespace DSACharacterCreator.UiTests)
└─ docs/
   ├─ PLAN.md
   └─ CONTENT_STATUS.md   # Erfassungsstand je Buch × Kategorie
```

### Adaptives Layout

- Die App wird um ein `MainView` (UserControl) gebaut, nicht um ein `Window`. Android nutzt `ISingleViewApplicationLifetime`,
  der Desktop setzt das `MainView` in ein `MainWindow`.
- Umschalten über **Container Queries** (Breakpoint ungefähr 800 px, eine Zwischenstufe ungefähr 1200 px):
  - **Breit:** Navigation links (Erschaffungsschritte bzw. Kategorien), Editor in der Mitte, rechts eine Übersicht
    mit AP-Konto, Eigenschaften, abgeleiteten Werten und Validierungsmeldungen.
  - **Schmal:** Navigation unten bzw. Schritt-Assistent, AP-Konto als kompakte Leiste oben, Übersicht als eigene Seite,
    Detailansichten als Vollbild-Seiten statt Seitenpanels.
- Listen mit vielen Einträgen (Zauber, Sonderfertigkeiten) laufen mit Virtualisierung, Suche und Filtern nach Quelle,
  Gruppe und „erfüllt Voraussetzungen“.

## 3. Quellen- und Regelwerksauswahl

### Datenmodell

`data/books.json` ist der Katalog aller Publikationen:

```json
{ "id": "grw", "name": "Regelwerk", "wikiName": "Regelwerk" }
{ "id": "wdv", "name": "Wege der Vereinigungen", "wikiName": "Wege der Vereinigungen", "adult": true }
```

- `wikiName` entspricht exakt der Schreibweise unter **„Publikation(en)“** im Wiki und dient zur Zuordnung.
- `adult: true` markiert Ü18-Werke. Im Wiki stehen sie im eigenen Bereich *„Wege der Vereinigungen – ab 18 Jahre“*.
- Jeder Inhaltseintrag trägt `publications: [{ "book": "grw", "page": 163 }, ...]`.
  Ein Eintrag ist **verfügbar, wenn mindestens eine seiner Publikationen aktiv ist**. Nachdrucke, etwa in Kompendien,
  sind damit automatisch abgedeckt.
- Wenn ein Buch einen bestehenden Eintrag erweitert (z. B. neue Anwendungsgebiete oder Optionen), wird das als
  eigener Eintrag mit `extends: "<id>"` und eigener Publikation erfasst. Die Erweiterung greift nur, wenn ihre Quelle aktiv ist.

### Verhalten

- **Standardauswahl für neue Charaktere:** alle Bücher außer `adult: true`. Die Standardauswahl lässt sich in den
  App-Einstellungen ändern.
- Ü18-Werke erscheinen in der Auswahl mit Kennzeichnung „ab 18“ und müssen pro Charakter bewusst aktiviert werden.
- Die Auswahl wird **pro Charakter** gespeichert (`enabledBooks`).
- Wird eine Quelle deaktiviert, obwohl der Charakter schon Inhalte daraus nutzt, wird **nichts still gelöscht**.
  Die betroffenen Einträge bleiben erhalten und erzeugen die Warnung „stammt aus deaktivierter Quelle“, mit den
  Aktionen *entfernen* oder *Quelle wieder aktivieren*.
- Spezies, Kultur und Profession unterliegen demselben Filter.

## 4. Charaktermodell: Entscheidungen statt Endwerte

Gespeichert werden **Entscheidungen**. Alle Werte werden daraus berechnet.

```
Character
├─ Meta: Name, Porträt, persönliche Daten, Notizen
├─ enabledBooks
├─ phase: Creation | Advancement
├─ creation:                      # Erschaffungsschicht
│   ├─ experienceLevel            # Erfahrungsgrad → AP-Startbudget + Erschaffungsgrenzen
│   ├─ species, culture, profession (+ getroffene Paket-Auswahlen)
│   ├─ attributeValues
│   ├─ advantages / disadvantages
│   ├─ specialAbilities
│   ├─ skills, combatTechniques, spells, rituals, liturgies, ceremonies, cantrips, blessings
│   └─ equipment, money
├─ log: [LogEntry]                # Steigerungsprotokoll (nur in Phase Advancement)
│   ├─ ApAward(amount, note, date)               # AP-Gutschrift nach Abenteuer
│   ├─ Raise(target, fromValue, toValue)         # Eigenschaft/Fertigkeit/Kampftechnik/Zauber …
│   ├─ Acquire(target, options, level)           # SF, Zauber, Liturgie, (mit SL-Erlaubnis) Vor-/Nachteil
│   ├─ Remove(target)                            # z. B. Nachteil abbauen
│   └─ Correction(...)                           # manuelle Korrektur (LeP/AsP/KaP-Verlust, permanente Kosten)
├─ overrides: [RuleOverride]      # bewusst akzeptierte Regelverstöße (Hausregeln, SL-Erlaubnis)
└─ creationSnapshot?              # Sicherung beim Wiederöffnen der Erschaffung (siehe 5.3)
```

**Berechnung** (`Rules.CharacterCalculator`, eine reine Funktion):

1. Grundwerte aus Spezies, danach Kultur- und Professionspaket anwenden
2. Erschaffungsentscheidungen anwenden. Ergebnis: **Erschaffungsstand**
3. Protokolleinträge der Reihe nach anwenden. AP-Kosten werden **beim Abspielen** gegen den jeweils aktuellen Stand
   berechnet und nicht gespeichert.
4. Abgeleitete Werte berechnen (LeP, AsP, KaP, SK, ZK, AW, INI, GS, Schicksalspunkte …)
5. Validierung. Ergebnis: Liste von `Issue { severity: Error | Warning | Info, ruleId, target, message }`

Vorteile: Rückgängig/Wiederholen ist trivial, AP stimmen immer (keine aufsummierten Rundungs- oder Folgefehler),
ein Wechsel der Profession berechnet alles korrekt neu, und das Protokoll ist zugleich die Steigerungshistorie.

**Validierung warnt, sie sperrt nicht.** Verstöße werden angezeigt. Errors verhindern nur den *Abschluss* der
Erschaffung, und selbst das kann ein Override (z. B. „Hausregel“ oder „SL erlaubt“) aufheben.

## 5. Phasen: Erschaffung und Steigerung

### 5.1 Zwei Regelschichten

| Regeltyp | gilt in Erschaffung | gilt in Steigerung |
|---|---|---|
| **Allgemeine Regeln** (z. B. Steigerungsobergrenze aus Leiteigenschaften, Voraussetzungen, Steigerungskosten A–E) | ✅ | ✅ |
| **Erschaffungsgrenzen** (aus dem Erfahrungsgrad: max. Eigenschaft, Eigenschaftssumme, max. FW/KtW, Anzahl Zauber/Liturgien, AP-Grenzen für Vor-/Nachteile usw.) | ✅ | ❌ |
| **Erschaffungsrechte** (Vor-/Nachteile frei wählbar, Nachteile bringen AP) | ✅ | ❌, nur über Protokolleintrag mit Kennzeichen „SL-Erlaubnis“ |

Jede Regel im Code trägt ihren Geltungsbereich (`RuleScope.Always | CreationOnly`). Die konkreten Werte
(Erfahrungsgrad-Tabelle, Obergrenzen) kommen aus dem Wiki (Bereich *Regeln*) und werden als Daten erfasst.

### 5.2 Erschaffung abschließen

- Explizite Aktion **„Erschaffung abschließen“**. Es gibt keinen automatischen Übergang.
- Vorher erscheint eine Zusammenfassung: verbleibende AP, offene Errors und Warnings, nicht getroffene Paket-Auswahlen.
- Errors müssen behoben oder per Override akzeptiert werden.
- Danach gilt `phase = Advancement`. Die Erschaffungsschicht ist schreibgeschützt, und alle weiteren Änderungen
  landen im Protokoll. Nicht ausgegebene Start-AP bleiben im AP-Konto (wie viele, regelt das Wiki).

### 5.3 Zurück in die Erschaffung („Erschaffung korrigieren“)

Fehler aus der Erschaffung sollen sich korrigieren lassen, ohne das Steigerungsprotokoll zu verlieren:

1. Aktion **„Erschaffung korrigieren“**. Zuerst wird ein `creationSnapshot` angelegt (Erschaffungsschicht plus Protokoll).
2. Die Phase wechselt zurück auf `Creation`. Die Erschaffungsschicht ist wieder editierbar, und die
   **Erschaffungsgrenzen werden nur gegen den Erschaffungsstand geprüft** (Schritt 2 der Berechnung), nicht gegen
   den Stand nach den Steigerungen.
3. Das Protokoll bleibt erhalten und wird zur Vorschau weiter abgespielt. Die Oberfläche zeigt live, welche
   Einträge durch die Korrektur **in Konflikt** geraten:
   - *Kostenänderung*: Der Eintrag ist noch gültig, kostet aber anders (z. B. weil der Startwert anders ist). Das ist
     ein Info-Hinweis, und die AP-Differenz wird angezeigt.
   - *Wertverschiebung*: `fromValue` passt nicht mehr zum aktuellen Wert. Es gibt zwei Möglichkeiten: den Eintrag
     verschieben (gleiche Anzahl Stufen) oder auf den Zielwert begrenzen.
   - *Ungültig*: Der Eintrag ist hinfällig (z. B. eine SF, die jetzt schon in der Erschaffung gekauft wurde) oder
     verletzt Voraussetzungen. Er kann gelöscht oder per Override behalten werden.
4. **„Korrektur übernehmen“** schließt die Erschaffung wieder ab (wie 5.2), aber erst, wenn alle Konflikte aufgelöst sind.
   **„Korrektur verwerfen“** stellt den Snapshot wieder her.
5. Jede Korrektur wird mit Datum als Info-Eintrag im Protokoll vermerkt, damit sie nachvollziehbar bleibt.

### Kultur und Profession sind Vorlagen

Kultur- und Professionspakete sind nur **Schablonen für eine Vorauswahl**. Nach der Wahl lässt sich alles abwandeln:
Werte steigern oder unter den Vorlagewert senken, Teile weglassen, anderes hinzunehmen.

- Kosten: Werte aus einer Vorlage kosten genau wie selbst gesteigerte (Talente ab 0, Kampftechniken ab 6). Ein Paket
  kostet daher immer, was sein Inhalt kostet; der Wiki-AP-Wert dient nur zum Abgleich (`docs/WIKI_ABWEICHUNGEN.md`).
- Speicherung: Talente/Kampftechniken als Abweichung vom Vorlagewert, damit ein Wechsel der Vorlage die eigenen
  Anpassungen behält.
- Voraussetzungen und Auswahlen einer Vorlage (Eigenschaftsminima, Variante, „eine der folgenden Kampftechniken“) sind
  Hinweise, keine Fehler.
- **Meilenstein 4/6:** Sonderfertigkeiten, Sprachen, Schriften, Zauber und Liturgien der Vorlage werden als einzelne,
  entfernbare Einträge des Helden übernommen (mit Herkunftsvermerk) und einzeln nach ihren eigenen Regeln berechnet
  und geprüft. Der vorläufige „Wiki-Rest“ entfällt dann.

## 6. Regel-Engine im Detail

- **AP-Kostentabelle** für die Steigerungsfaktoren A–E (und Eigenschaften) liegt als Daten in `data/rules/`. Die
  Kostenfunktion ist zentral und vollständig getestet.
- **Voraussetzungen** sind strukturiert und nicht als Freitext gespeichert (Freitext gibt es nur zusätzlich für die Anzeige):

  ```json
  "prerequisites": { "all": [
    { "attribute": "MU", "min": 13 },
    { "not": { "disadvantage": "unfaehig", "option": "$selected" } },
    { "maxCount": { "advantage": "begabung", "count": 3 } }
  ]}
  ```

  Bausteine: `attribute`, `skill`, `combatTechnique`, `specialAbility`, `advantage`, `disadvantage`, `species`,
  `culture`, `profession`, `tradition`, `all`, `any`, `not`, `maxCount`, `$selected` (gewählte Option des Eintrags).
- **Optionen und Stufen:** Einträge mit Auswahl (z. B. „Begabung (Fertigkeit)“), Stufen (I–III) und Kosten
  abhängig von der Wahl (z. B. 6/12/18/24 AP je nach Steigerungsfaktor der gewählten Fertigkeit) werden über
  `options`, `levels` und `cost` mit Kostenformeln modelliert.
- **Pakete** von Spezies, Kultur und Profession: Fixe Bestandteile und Auswahlgruppen („wähle 1 aus …“,
  „verteile X Punkte auf …“) sind dasselbe Datenmodell. Offene Auswahlen erscheinen als Validierungsmeldung.
- **Modifikatoren:** Effekte von Vorteilen, Nachteilen und SF auf abgeleitete Werte (z. B. „Hohe Lebenskraft“) laufen
  über ein generisches `effects`-Feld, damit nicht jeder Eintrag eigenen Code braucht. Echte Sonderfälle bekommen
  einen benannten Handler im Code (`effectHandler: "..."`).

## 7. Inhaltsdaten und das Regel-Wiki

### Quelle

**Alle Inhalte werden aus dem Ulisses DSA Regel-Wiki erfasst: <https://dsa.ulisses-regelwiki.de/>**

Wiki-Bereiche: Regeln · Spezies · Kulturen · Professionen · Sonderfertigkeiten · Vor- und Nachteile · Magie ·
Götterwirken · Rüstkammer · Bestiarium · Herbarium · Gifte und Krankheiten · Wege der Vereinigungen (ab 18).

Aufbau eines Eintrags im Wiki (Beispiel Vorteil, URL `vorteil.html?vorteil=Begabung`):

| Wiki-Feld | Datenfeld |
|---|---|
| Titel | `name` |
| Regel | `ruleText` (Anzeigetext) |
| Voraussetzung | `prerequisitesText` (Anzeige) **und** `prerequisites` (strukturiert, von Hand modelliert) |
| AP-Wert | `apValueText` (Anzeige) **und** `cost` (strukturiert) |
| Anmerkung | `note` |
| Publikation(en) | `publications` (Buch + Seite) |

Hinweis: Die Übersichtslisten im Wiki (z. B. `vorteilauswahl.html`) werden per JavaScript gefüllt. Ein einfaches
`curl` findet dort keine Einträge, deshalb einen Browser (Seitentext auslesen) verwenden.

### Regeln für die Erfassung

1. Nur Texte übernehmen, die **im Wiki stehen**. Keine Texte aus PDFs oder Büchern, auch nicht aus den lokalen
   Regelwerks-PDFs.
2. Jede Zahl, Voraussetzung und Kostenangabe wird am Wiki geprüft, nicht aus dem Gedächtnis übernommen.
3. Jeder Eintrag bekommt `publications` exakt wie im Wiki angegeben.
4. Stabile, sprechende IDs (`vorteil.begabung`, `sf.wuchtschlag`), optional `optolithId` für einen späteren Import.
5. Nach jeder Erfassung `docs/CONTENT_STATUS.md` aktualisieren.

### Ablage

```
data/
├─ books.json
├─ rules/            # Erfahrungsgrade, Kostentabelle, Grundregeln für abgeleitete Werte
├─ attributes.json
├─ species/  cultures/  professions/
├─ advantages/  disadvantages/  special-abilities/
├─ skills/  combat-techniques/
├─ magic/  (spells, rituals, cantrips, traditions …)
├─ karma/  (liturgies, ceremonies, blessings, traditions …)
└─ equipment/ (weapons, armor, items)
```

Pro Kategorie gibt es eine Datei je Buch (z. B. `data/advantages/grw.json`). So ist der Erfassungsstand je Buch
leicht nachzuverfolgen. Ein JSON-Schema je Kategorie plus `Content.Tests` sichern die Integrität (eindeutige IDs,
auflösbare Referenzen, gültige Buch-IDs, Pflichtfelder).

## 8. Speichern, Export, Import

- Eigenes Format `*.dsa5hero.json` mit `formatVersion` und Migrationen in `Persistence`.
- Speicherort: App-Datenverzeichnis (Charakterliste in der App) plus Import/Export über den Avalonia `StorageProvider`
  (auf Android der System-Dateidialog bzw. Teilen).
- PDF-Charakterbogen über SkiaSharp auf allen Plattformen. Auf Android wird er gespeichert oder per Share-Intent geteilt.
- Später: **Optolith-Import** (`*.json`, IDs wie `ADV_4` und `TAL_5`) über die Zuordnung `optolithId`.

## 9. Meilensteine

1. ✅ **Gerüst** (2026-10-09): Solution mit allen Projekten, startet auf Linux, Windows und im Android-Emulator; `MainView` mit
   adaptivem Layout (Platzhalterseiten); CI-fähiger Build und Tests.
1b. ✅ **Heldenverwaltung als Hauptmenü** (2026-10-09): Heldenliste (neu, öffnen, duplizieren, exportieren,
   importieren, löschen mit Bestätigung), Speichern jeder Änderung sofort als `*.dsa5hero.json` im App-Datenordner,
   Editor mit Namensfeld und „‹ Helden“, Android-Zurück-Taste.
2. ✅ **Core-Fundament** (2026-10-09): Content-Loader, Quellenfilter, `books.json`, Kostentabelle, Erfahrungsgrade, Eigenschaften,
   Talente, Kampftechniken. `CharacterCalculator` und Validierung mit Geltungsbereichen. Unit-Tests.
3. ✅ **Erschaffung, Teil 1** (2026-10-10; SF/Zauber/Liturgien der Pakete folgen mit 4/6): Spezies, Kultur, Profession mit Paketen und Auswahlgruppen (zunächst Inhalte aus dem
   Regelwerk); Erschaffungs-Assistent in der UI (breit und schmal).
4. **Vor- und Nachteile, Sonderfertigkeiten:** Voraussetzungs-Engine, Optionen und Stufen, Effekte.
5. **Phasen:** Erschaffung abschließen, Steigerungsprotokoll, AP-Gutschriften, „Erschaffung korrigieren“ mit
   Konfliktauflösung, Rückgängig/Wiederholen.
6. **Magie und Götterwirken:** Traditionen, Zauber, Rituale, Liturgien, Zeremonien, Zaubertricks, Segnungen.
7. **Ausrüstung**, Charakterbogen-Ansicht, **PDF-Export** (Desktop und Android).
8. **Quellenauswahl-UI** pro Charakter plus Standardauswahl in den Einstellungen; Ü18-Kennzeichnung.
9. **Inhaltsausbau:** Buch für Buch nach `CONTENT_STATUS.md`, bis alle Regelwerke abgedeckt sind (läuft dauerhaft
   parallel ab Meilenstein 4).
10. **Optional:** Optolith-Import, Würfel- und Probenhelfer, Gruppenverwaltung.

## 10. Offene Punkte / Risiken

- **Umfang der Inhalte:** Mehrere tausend Einträge. Wichtig sind ein sauberes Schema und eine Erfassung Buch für Buch.
  Eventuell hilft später ein Hilfswerkzeug, das Wiki-Seiten in JSON-Gerüste vorkonvertiert, die dann von Hand
  geprüft werden. Vorher die Nutzungsbedingungen des Wikis prüfen und schonend abrufen.
- **Errata und spätere Regeländerungen:** Gilt immer der aktuelle Wiki-Stand? Falls ein Buch eine Regel ändert,
  wird die Änderung als `extends`- oder `replaces`-Eintrag modelliert.
- **Sonderfälle**, die sich nicht generisch modellieren lassen (z. B. Spezies-Sonderregeln, Paktierer), bekommen
  benannte Handler. Diese Liste wird während der Erfassung gepflegt.
- **Android:** Trimming/AOT mit System.Text.Json Source Generators testen, außerdem Performance bei großen Listen.
