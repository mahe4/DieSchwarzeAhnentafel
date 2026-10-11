# DSA5 Character Creator

Heldenerschaffung und Steigerung für *Das Schwarze Auge 5* auf Avalonia 12 / .NET 10 (Windows, Linux, Android).
Gesamtplan und Architektur: [docs/PLAN.md](docs/PLAN.md). Erfassungsstand der Inhalte: [docs/CONTENT_STATUS.md](docs/CONTENT_STATUS.md).
Offene und optionale Punkte: [docs/OFFENE_PUNKTE.md](docs/OFFENE_PUNKTE.md) (nach jeder Änderung pflegen).

## Inhalte: Quelle ist das Regel-Wiki

- **Alle Regelinhalte** (Werte, Regeltexte, Voraussetzungen, AP-Kosten, Publikationen) werden aus dem offiziellen
  Ulisses DSA Regel-Wiki erfasst: <https://dsa.ulisses-regelwiki.de/>
- **Lizenz:** Keine Fluff- oder Beschreibungstexte aus Büchern oder PDFs, auch nicht aus den Regelwerks-PDFs unter
  `~/Nextcloud/DSA`. Nur Texte übernehmen, die im Wiki stehen.
- Werte nie aus dem Gedächtnis eintragen, sondern immer am Wiki prüfen.
- **Kultur und Profession sind Vorlagen** (Vorauswahl), danach ist alles abwandelbar – siehe PLAN.md Abschnitt 5/6.
- **Paketkosten werden immer aus dem Inhalt berechnet** (Talente, Kampftechniken, später SF/Zauber …). Der AP-Wert im
  Wiki (`wikiAp`) überschreibt nie die Berechnung; weicht er ab, ist das ein Wiki-Fehler → Hinweis in der App und
  `docs/WIKI_ABWEICHUNGEN.md` (`tools/wiki/check_packages.py`).
- Die Übersichtslisten im Wiki werden per JavaScript gefüllt, `curl` sieht dort keine Einträge. Deshalb den
  integrierten Browser verwenden und den Seitentext auslesen. Einzelseiten haben URLs wie
  `vorteil.html?vorteil=Begabung`.
- Wiki-Felder → Datenfelder: Regel → `ruleText`, Voraussetzung → `prerequisitesText` + strukturierte `prerequisites`,
  AP-Wert → `apValueText` + strukturierte `cost`, Anmerkung → `note`, Publikation(en) → `publications`.
- Ü18-Inhalte stehen im Wiki unter „Wege der Vereinigungen – ab 18 Jahre“ und bekommen in `books.json` die
  Markierung `adult: true`.
- Nach jeder Erfassung `docs/CONTENT_STATUS.md` aktualisieren.
- **Werkzeug:** `tools/wiki/wiki.py` lädt Wiki-Seiten (Cache in `tools/wiki/.cache/`, 0,7 s Pause) und liest die
  Feldpaare der Eintragsseiten. Die Übersichtslisten stehen trotz JavaScript im HTML (Links mit einfachen
  Anführungszeichen). Beispiel: `python3 tools/wiki/import_skills.py` erzeugt Talente und Kampftechniken.
  Weitere Importe: `import_species.py`, `import_cultures.py`, `crawl_professions.py` + `import_professions.py`.
  Artikelseiten (Spezies, Kulturen, Professionen) liest `pagetext.py`; Quellenangaben normalisiert `books.py`
  (neue Bücher werden automatisch ergänzt – danach `prune_books.py` und Schreibvarianten in `CANONICAL` pflegen).
  Talent-/Kampftechnik-Tippfehler im Wiki stehen in `skillnames.ALIASES`. Weitere Importe:
  `import_advantages.py`, `crawl_special_abilities.py` + `crawl_magic_sf.py` (magische/karmale SF) +
  `import_special_abilities.py`, `crawl_spells.py` + `import_spells.py` (Zauber, Liturgien …); `crawl_equipment.py` + `import_equipment.py` (Waffen, Rüstungen), `crawl_items.py` + `import_items.py` (Gegenstände, Ausrüstungspakete; vorher Waffen/Rüstungen importieren); `check_packages.py` erzeugt
  `docs/WIKI_ABWEICHUNGEN.md`. Nach jedem Import `prune_books.py` laufen lassen.
- Daten liegen in `src/DSACharacterCreator.Content/data/<kategorie>/<buch-id>.json` (Buch = erste Publikation).
- Charaktermodell: Talente/Kampftechniken werden als Abweichung vom Paketwert (Kultur + Profession) gespeichert,
  Eigenschaften als gekaufter Wert ohne Spezies-Änderung. Formatänderungen → `CharacterSerializer.Migrate`.
- Steigerungsphase: Änderungen gehen als Einträge ins Protokoll (`Character.Log`). Die Seiten ändern eine Arbeitskopie
  (`AdvancementInfo.Working`), `Phases.Record` überträgt die Unterschiede. Kosten werden beim Abspielen berechnet.
  `RuleData.Load` prüft IDs, Verweise und Bücher; `Content.Tests` laufen über alle Dateien.

## Konventionen

- Charakterbogen: `Core/Sheet/CharacterSheet.cs` erzeugt alle Texte des Bogens; App-Ansicht und PDF zeigen nur an.
  PDF: `src/DSACharacterCreator.Pdf` (`SheetPdf` ordnet an, `PdfLayout` setzt Text/Tabellen mit Seitenumbruch;
  `SpellTextPdf` gibt die Regeltexte der Zauber/Liturgien aus `Core/Sheet/SpellTextSheet.cs` getrennt aus);
  ansehen: PDF-Tests mit `DSA_SHOTS=<ordner>` schreiben `bogen.pdf`/`bogen-lang.pdf`, dann `pdftoppm -png`.
- Regeln gehören in `Core`, nicht in ViewModels. Die Berechnung ist eine reine Funktion über die Entscheidungen
  des Charakters (siehe PLAN.md, Abschnitt 4).
- Jede Regel trägt einen Geltungsbereich: `Always` oder `CreationOnly`.
- Validierung warnt, sie sperrt nicht. Overrides sind erlaubt.
- Referenzprojekt mit gleichem Stack: `~/RiderProjects/SplittermondCharacterCreator`.

## Bauen & Testen

```bash
dotnet build src/DSACharacterCreator.Desktop
dotnet run --project src/DSACharacterCreator.Desktop
dotnet test --solution DSACharacterCreator.slnx
dotnet build src/DSACharacterCreator.Android -p:AndroidSdkDirectory=$HOME/Android/Sdk
# Auf Gerät installieren (PATH muss adb enthalten: $HOME/Android/Sdk/platform-tools)
dotnet build src/DSACharacterCreator.Android -t:Install -p:AndroidSdkDirectory=$HOME/Android/Sdk "-p:AdbTarget=-s <geräte-id>"
```

- Debug-APKs nicht per `adb install` installieren: Wegen Fast Deployment fehlen darin die Assemblies, und die App
  stürzt beim Start ab. Stattdessen `-t:Install` verwenden.

- UI-Tests direkt ausführen, wenn etwas komisch ist: `tests/DSACharacterCreator.App.Tests/bin/Debug/net10.0/DSACharacterCreator.App.Tests`.
  `dotnet test` hat „Test Case Cleanup Failures“ schon einmal als bestanden gemeldet.
- Screenshots der Layout-Tests: Umgebungsvariable `DSA_SHOTS=<ordner>` setzen.
- Zeitmessungen (Laden, Berechnung, Seitenwechsel) stehen in Debug-Builds im Logcat: `adb logcat | grep DSA-Zeit`.
  Im Release-Build mit `-p:DsaTiming=true` bauen. Debug ist am Handy etwa 2–3-mal langsamer als Release.
- Leistung: Seiten bleiben nach dem ersten Öffnen erhalten (`SectionViewModel.LoadedPage`), lange Listen sind
  virtualisiert (ListBox + VirtualizingStackPanel), das Protokoll wird ab der ersten Änderung neu abgespielt
  (`ReplayCache`). Regeldaten laden im Hintergrund (`MainViewModel` mit Ladehinweis).
- Adaptives Layout in `MainView.axaml` über Container Queries. Standardwerte gehören in Styles, nicht als lokale
  Attribute, weil lokale Werte die Container-Query-Styles überstimmen.
- ViewModel → View über `DataTemplates` in `App.axaml`, ohne reflektionsbasierten ViewLocator (Trimming auf Android).
