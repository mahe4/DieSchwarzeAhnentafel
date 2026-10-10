# Erfassungsstand Inhalte

Quelle: <https://dsa.ulisses-regelwiki.de/>. Nur Wiki-Texte, keine Buchtexte (siehe `CLAUDE.md`).
Erzeugt bzw. gepflegt mit `tools/wiki/` (siehe dort).

## Nach Kategorie

Die Kategorien werden aus den Wiki-Übersichtslisten vollständig übernommen, also über alle Bücher hinweg. Welche
Bücher ein Eintrag hat, steht in seinen `publications`.

| Kategorie | Stand | Datei(en) | Quelle im Wiki | Erzeugt durch |
|---|---|---|---|---|
| Publikationen | 🟨 alle, die in erfassten Einträgen vorkommen | `data/books.json` | „Publikation(en)“ der Einträge | von Hand / `tools/wiki` |
| Eigenschaften | ✅ 8 | `data/attributes.json` | Grundregeln › Eigenschaften | von Hand |
| Erfahrungsgrade | ✅ 7 | `data/rules/experience-levels.json` | Heldenerschaffung › Schritt 2 | von Hand |
| Erschaffungsregeln (Startwerte, Grenzen) | ✅ | `data/rules/creation.json` | Heldenerschaffung › Schritt 2, 5, 8, 9 | von Hand |
| Kostentabelle A–E | ✅ bis 25 | `data/rules/improvement-costs.json` | Heldenerschaffung › Schritt 8 | von Hand |
| Talente | ✅ 59 | `data/skills/*.json` | talentauswahl.html | `import_skills.py` |
| Kampftechniken | ✅ 21 | `data/combat-techniques/*.json` | kampftechnikauswahl.html | `import_skills.py` |
| Spezies | ✅ 10 | `data/species/*.json` | spezies.html | `import_species.py` (Eigenschaftsänderungen strukturiert im Skript, gegen Wiki-Text geprüft) |
| Kulturen | ✅ 43 (3 Wiki-Seiten leer) | `data/cultures/*.json` | kulturen.html › Kategorien | `import_cultures.py` |
| Professionen | 🟨 296; Talente/Kampftechniken/Varianten strukturiert, SF/Zauber/Liturgien als Text | `data/professions/*.json` | professionen.html › Kategorien (rekursiv) | `crawl_professions.py`, `import_professions.py` |
| Vor-/Nachteile | ✅ 175 + 81; Kosten zu 97 % strukturiert, 16 Basiswert-Effekte | `data/advantages/*.json`, `data/disadvantages/*.json` | vorteilauswahl.html, nachteilauswahl.html | `import_advantages.py` |
| Sonderfertigkeiten (profan) | ✅ 581; Kosten zu 99,8 % strukturiert | `data/special-abilities/*.json` | sonderfertigkeiten.html › Profane (rekursiv) | `crawl_special_abilities.py`, `import_special_abilities.py` |
| Sprachen, Schriften | ✅ 80 + 41 (Aventurien, Uthuria, Myranor) | `data/languages/`, `data/scripts/` | Sprachen.html (Tabellen) | `import_special_abilities.py` |
| Sonderfertigkeiten (magisch/karmal) | ✅ 1290 (37 magische + 35 karmale Traditionen mit Leiteigenschaft); Kosten zu 99,5 % strukturiert | `data/special-abilities/*.json` (`domain`) | Magische/Karmale Sonderfertigkeiten (Auswahllisten + Artikelseiten) | `crawl_magic_sf.py`, `import_special_abilities.py` |
| Magie | ✅ 263 Zaubersprüche, 74 Rituale, 113 Zaubertricks | `data/spells/*.json` (`group`) | magie.html › Auswahllisten | `crawl_spells.py`, `import_spells.py` |
| Magische Handlungen | ✅ 207 (Hexenflüche, Herrschafts-, Geoden-, Goblin-, Zibiljarituale, Elfenlieder, verzerrte Elfenlieder, Zaubermelodien, Zaubertänze, Animistenkräfte, Schelmenstreiche, Zauberrunen, Bannzeichen) | `data/spells/*.json` (`group: magicalAction`) | Z_MagHandlungen.html | `import_spells.py` |
| Götterwirken | ✅ 199 Liturgien, 138 Zeremonien, 12 Segnungen (Talismane noch nicht) | `data/spells/*.json` | goetterwirken.html › Auswahllisten | `crawl_spells.py`, `import_spells.py` |
| Waffen | ✅ 401 reguläre Nah- und Fernkampfwaffen (TP, L+S, AT/PA-Mod, RW bzw. LZ/RW/Munition) | `data/weapons/*.json` | RS_Waffen.html › Kategorien › Reguläre … | `crawl_equipment.py`, `import_equipment.py` |
| Rüstungen | ✅ 77 (Grundrüstungen der Rüstungstabelle und reguläre Rüstungen: RS, BE, zusätzliche Abzüge) | `data/armor/*.json` | RS_Ruestung.html › Kategorien | `crawl_equipment.py`, `import_equipment.py` |
| Sonstige Ausrüstung | ✅ 362 Gegenstände (Ausrüstung mit Sammelseiten wie Kleidung, Lichtquellen, Bücher; Waffenausrüstung, Munition, Rüstungsausstattung): Gewicht, Preis, Strukturpunkte, Komplexität, Regeltechnik | `data/items/*.json` | RK_Ausruestung.html, RS_Waffenausruestung.html, RS_Ruestungsausstattung.html | `crawl_items.py`, `import_items.py` |
| Ausrüstungspakete | 🟡 2 von 10 (Abenteurer, Adlige); die übrigen Paketseiten sind im Wiki leer | `data/equipment-packages/*.json` | ruestkammer/ausruestungspakete.html | `crawl_items.py`, `import_items.py` |
| Helme, Rüstungsteile, seltene & einmalige Stücke | ⬜ (Helme/Rüstungsteile: Werte auf eigener Kostenseite, Fokusregel Trefferzonen) | | | |

## Offene Punkte

- **Waffen und Rüstungen (Meilenstein 7a):** AT/PA mit Waffe = Kampftechnik + AT/PA-Mod − Belastung; TP + 1 je Punkt
  der Leiteigenschaft über der Schadensschwelle (bei mehreren Leiteigenschaften die günstigere; „GE 15/KK 14“ je
  Eigenschaft). Fernkampf: FK der Kampftechnik, ohne Abzug durch Belastung (Wiki nennt nur AT und Verteidigung; geklärt 2026-10-11). „0/-“ = keine Parade.
  Belastung (Zustand): Stufe I–III −1 je Stufe auf AT, Verteidigung, INI, GS und Talente mit Belastung, Stufe IV
  handlungsunfähig. Belastungsgewöhnung: zwei Zeilen der Rüstungstabelle je Stufe nach oben (BE −1 je Stufe;
  zusätzliche Abzüge entfallen erst, wenn die Stufe die BE der Rüstung übersteigt). Eine angelegte Rüstung; Helme,
  Rüstungsteile und seltene & einmalige Stücke folgen.
  Parierwaffen und Schilde (Wiki: Parierwaffen und Schilde): Wer zusätzlich zur Hauptwaffe eine Parierwaffe oder
  einen Schild führt, bekommt deren passiven Parade-Bonus auf die PA der Hauptwaffe; nur der höchste zählt. Bonus bei
  Schilden = PA-Mod, bei Parierwaffen aus der Anmerkung „Parierwaffe (PA-Bonus +1 für die Hauptwaffe)“ (`parryBonus`).
  Die eigene Parade eines Schilds (Kampftechnik Schilde) erhält den doppelten PA-Mod. Große Schilde geben „zusätzlich
  –1/–2 AT auf die Hauptwaffe“ (`mainWeaponAttackModifier`). Es zählen nur geführte Waffen (Häkchen in der Liste).
  Holzwaffen sind eine Abwandlung (−3 TP) und nicht als eigene Einträge erfasst. Ausrüstung kostet keine AP und steht
  außerhalb von Erschaffung und Protokoll (`Character.Equipment`).

- **Gegenstände, Traglast und Geld (Meilenstein 7b):** Gewicht und Preis werden aus dem Wiki-Text in Stein bzw.
  Silbertaler umgerechnet (`tools/wiki/units.py`; Währungen: 1 D = 10 S, 1 H = 0,1 S, 1 K = 0,01 S; Maßeinheiten:
  Unze 25 g, Skrupel 1 g). Spannen („50 bis 500 S“), „Preis x 2“ oder „unbezahlbar“ bleiben ohne Zahl; dann trägt man
  einen eigenen Preis ein. Traglast (Tragkraft und Stemmen): KK × 2 Stein frei, je 4 volle weitere Stein eine Stufe
  Belastung; sie addiert sich zur Belastung der Rüstung (Belastungsgewöhnung gilt nur für die Rüstung). Das Gewicht
  der angelegten Rüstung zählt nicht (Zustand Belastung); abgelegte, mitgeführte Rüstungen zählen. Geld: Startkapital
  750 S, Reich +250 S, Arm −250 S je Stufe (Schritt 13); gekaufte Einträge gehen ab, dazu Einnahmen/Ausgaben. Mehr
  ausgegeben als vorhanden ist eine Warnung. Pakete: jede Zeile wird ein eigener Eintrag mit Gewicht/Preis laut
  Paketliste, Zeilen mit „–“ übernehmen den Wert des gleichnamigen Gegenstands; der Preis ergibt sich aus den Zeilen
  (Abweichungen beim Gewicht in `WIKI_ABWEICHUNGEN.md`). Nicht umgesetzt: optionale Regel „Variables Startkapital“,
  Fassungsvermögen von Behältern, Tierzubehör.

- **Zauber und Liturgien (Meilenstein 6):** Kosten = Aktivierung + Steigerung nach Kostentabelle, Zaubertricks und
  Segnungen 1 AP. Höchstwert: höchste Probeneigenschaft + 2, ohne Merkmals- bzw. Aspektkenntnis höchstens 14
  (Erfahrung); bei der Erschaffung Höchstwert, Anzahl Zauber/Liturgien und Fremdzauber nach Erfahrungsgrad.
  Begabung gilt laut Wiki auch für Zauber und Liturgien. Merkmals-/Aspektkenntnis sind Sonderfertigkeiten mit Auswahl
  des Merkmals bzw. Aspekts (Kosten nach Anzahl: 10/20/40 bzw. 15/25/45; Voraussetzung „3 Zauber des Merkmals auf
  10“ geprüft). Aspekte stehen bei Liturgien in der Verbreitung in Klammern („Praios (Ordnung)“).
- **Magische Handlungen** werden laut Wiki wie Zauber aktiviert und gesteigert („alle Grundregeln für Fertigkeiten
  gelten“); sie unterscheiden sich nur beim Wirken. Geklärt mit dem Nutzer (2026-10-10): Sie zählen bei der Erschaffung
  zur Höchstzahl der Zauber/Liturgien (nie als Fremdzauber), die Grenze 14 ohne Merkmalskenntnis gilt. Steigerungsfaktor
  B für Hexenflüche, Herrschafts- und Geodenrituale laut Übersichtsseite (im Import geprüft). Faktor zur Wahl bei
  „Bannzeichen wider (Wesenheit)“ (A Spinnen, B Daimonide/Untote/Geister/Feen, C Dämonen) und den Tierart-Kräften
  (A–D, das Wiki nennt keinen). Tradition über die Kategorie; Zauberrunen und Bannzeichen auch mit der SF
  Zauberrunenkunde bzw. Bannzeichnungskunde. Elfenlieder setzen den Vorteil Zweistimmiger Gesang voraus.
  „Flüche für insgesamt N AP“ einer Profession erscheint als Hinweis (`curseAp`).
- **Professionen mit Zaubern/Liturgien:** strukturiert als `spells`, `spellChoices` („ein Zaubertrick aus folgender
  Liste“, „Archofaxius oder Ignifaxius 7“), `curseAp` („Flüche für insgesamt N AP“, Hinweis). Kurznamen („Balsam“) über
  eindeutige Anfänge. 254 von 296 Professionen sind vollständig strukturiert; 17 Teile bleiben in `unparsedSpells`
  (Platzhalter wie „(Element-)faxius“, im Wiki nicht vorhandene Zauber).
- **Traditionsartefakte und permanente AsP:** Bindungen kosten ihren AP-Wert und die Bindungskosten als permanente
  AsP (`permanentAstralCost`, beim Bannschwert je Kampftechnik), die den AsP-Höchstwert senken. Verlorene AsP/KaP
  lassen sich für 2 AP je Punkt zurückkaufen (Erfahrung), auch bei der Erschaffung; Zukauf erst danach und erst, wenn
  alles zurückgekauft ist. Professionsvorlagen kaufen die AsP ihrer Bindungen zurück (Wiki-Anmerkung: „kostet diese
  Sonderfertigkeit 14 AP“) – damit passen die meisten Magier-Pakete exakt zum Wiki-AP-Wert.
- **Element-Platzhalter** in Professionen („(Element-)faxius 10“) werden zur Auswahl unter den Element-Varianten
  (Ignifaxius, Aquafaxius …).

- **Magische/karmale SF (4c):** Traditionen tragen `tradition` (Art, Leiteigenschaft, „nur zur Hälfte“ laut Tabelle
  Heldenerschaffung › Leiteigenschaften, wohlgefällige Talente). AsP/KaP = Grundwert des Vorteils + Leiteigenschaft
  ± Vor-/Nachteile. „Leiteigenschaft der Tradition N“ ist als Voraussetzung strukturiert. 165 SF haben
  Voraussetzungsteile zum Prüfen von Hand (Pakte, Vampir-/Lykanthropen-Status, Zauberwerte – letztere mit
  Meilenstein 6). Kosten je Zauber-/Liturgie-Steigerungsfaktor (Lieblingszauber …) sind vorerst Ausprägungen
  „A-Zauber“ … „D-Zauber“. Stufen ohne Ende („Stufe I: 5 AP, Stufe II: 5 AP … usw.“) haben `openLevels`.
  Die Traditionsartefakte selbst (Magierstab …) sind keine SF und werden nicht übernommen; der Crawl folgt in den
  Feenpakt-Seiten Links auf Regelseiten, `magic_pages()` filtert diesen Beifang heraus.
- **Voraussetzungen der Professionen** (Zauberer, Tradition, Prinzipientreue, Verpflichtungen …) stehen in
  `prerequisiteEntries` und werden bei der Wahl der Profession als Vorauswahl übernommen; sie gehören nicht zum
  AP-Wert der Profession.

- **Sonderfertigkeiten:** 53 mit Voraussetzungsteilen zum Prüfen von Hand (meist kontextabhängig: „je nach
  Material“, „Einweisung durch …“). Voraussetzungen je Stufe („Stufe II: GE 15“) gelten ab der jeweiligen Stufe;
  Verweise auf die eigene niedrigere Stufe entfallen. „Passender Kampf-/Talentstil“ wird über die Stillisten
  (`extendedAbilitiesText`/`fittingStylesText`) geprüft. Fertigkeitsspezialisierung: n-te kostet n-fach und braucht FW 6n.
- **Professionsvorlagen:** Sonderfertigkeiten werden als einzelne Einträge übernommen (`specialAbilities`), das
  Sprachen-/Schriften-Budget (`languageAp`) als Hinweis. Nicht zuordenbare SF (44, z. B. Zaubermelodien, „Flüche für
  insgesamt N AP“) stehen in `unparsedSpecialAbilities`.
  Professionen ohne Zauber/Liturgien gelten als vollständig strukturiert: kein Wiki-Rest, Differenz = Wiki-Fehler.
- **Kultur:** Muttersprache (Sprache III, aus dem Sprachtext der Kultur) und Ortskenntnis kostenlos.
- **Quellen:** Ähnlichkeitsabgleich (`books.similar`, Schwelle 0,88, nie über Bandnummern hinweg) ordnet Tippfehler
  im Wiki bekannten Titeln zu; Tabellenzellen ohne Seitenangabe werden nicht als Bücher angelegt.

- **Vor-/Nachteile:** 8 Kosten ohne festen Wiki-Wert (z. B. „halbe Giftstufe“, „je nach Schlechter Eigenschaft“) trägt
  der Spieler ein (`cost.type = manual`). 19 Voraussetzungsteile sind nicht strukturiert (`unparsedPrerequisites`; fast
  nur erzählerische Bedingungen wie „muss einen Basilisken getötet haben“ oder Namens-Vorteile) und erscheinen als
  Hinweis „von Hand bzw. mit der Spielleitung prüfen“. „Nicht mehr als drei Begabungen pro Person“ / „Maximal 2 Unfähigkeiten“ sind als
  `maxCount` umgesetzt (jeweils für eine andere Fertigkeit); Begabung gilt laut Wiki auch für Zauber und Liturgien –
  die Auswahl umfasst vorerst nur Talente (Zauber/Liturgien folgen mit Meilenstein 6). Voraussetzungen mit
  Ausprägung („Blind (für Sicht)“) werden großzügig geprüft. Basiswert-Effekte stehen in `EFFECTS` im Import und
  werden gegen den Wiki-Regeltext geprüft.

- `Gewandheit` ist im Wiki bei den Kampftechniken falsch geschrieben; der Import bildet es auf `GE` ab.
- Leere Kulturseiten im Wiki: Stammesgoblins, Tocamuyac, Brobim (nicht übernommen).
- Wüstenelfen haben im Wiki keine Quellenangabe → Pseudo-Quelle `wiki`.
- **Paketkosten:** Kultur- und Professionspakete kosten, was ihr Inhalt kostet. Der AP-Wert aus dem Wiki
  (`wikiAp`) dient nur zum Vergleich; Abweichungen sind Wiki-Fehler und stehen in
  [WIKI_ABWEICHUNGEN.md](WIKI_ABWEICHUNGEN.md) (`python3 tools/wiki/check_packages.py`).
- Professionen mit mehreren AP-Werten („je nach gewählter Variante“) haben kein `wikiAp`; die Werte stehen bei den Varianten.
- 4 Professionen mit Werten je Erfahrungsgrad („Dolche 10/12/12“, AP „380/480/680“) haben `byExperienceLevel`;
  die App nimmt den Wertesatz zum Erfahrungsgrad des Helden (sonst den nächstliegenden). 3 Professionen behalten
  Reste in `unparsed` (Punkteverteilung beim Runenschöpfer, je ein Talent ohne Wert im Wiki).
- Varianten: Werteänderungen („X statt Y“) strukturiert; SF-Änderungen u. Ä. als Text (`variants[].text`).
- **Eigenschaftsänderungen der Spezies** verschieben nur den Höchstwert bei der Erschaffung (Zwerg: KO/KK bis 15),
  sie verändern weder den Wert noch die Eigenschaftssumme (geklärt mit dem Nutzer, 2026-10-10).
- Quellenangaben werden in `tools/wiki/books.py` normalisiert (`CANONICAL`, `clean()`): Tippfehler, Untertitel,
  Jahreszahlen, Zusätze wie „[Als …]“.
- Die optionale Regel „Eigenschaftsobergrenze“ ist noch nicht umgesetzt.
