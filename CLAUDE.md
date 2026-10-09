# DSA5 Character Creator

Heldenerschaffung und Steigerung für *Das Schwarze Auge 5* auf Avalonia 12 / .NET 10 (Windows, Linux, Android).
Gesamtplan und Architektur: [docs/PLAN.md](docs/PLAN.md). Erfassungsstand der Inhalte: [docs/CONTENT_STATUS.md](docs/CONTENT_STATUS.md).

## Inhalte: Quelle ist das Regel-Wiki

- **Alle Regelinhalte** (Werte, Regeltexte, Voraussetzungen, AP-Kosten, Publikationen) werden aus dem offiziellen
  Ulisses DSA Regel-Wiki erfasst: <https://dsa.ulisses-regelwiki.de/>
- **Lizenz:** Keine Fluff- oder Beschreibungstexte aus Büchern oder PDFs, auch nicht aus den Regelwerks-PDFs unter
  `~/Nextcloud/DSA`. Nur Texte übernehmen, die im Wiki stehen.
- Werte nie aus dem Gedächtnis eintragen, sondern immer am Wiki prüfen.
- Die Übersichtslisten im Wiki werden per JavaScript gefüllt, `curl` sieht dort keine Einträge. Deshalb den
  integrierten Browser verwenden und den Seitentext auslesen. Einzelseiten haben URLs wie
  `vorteil.html?vorteil=Begabung`.
- Wiki-Felder → Datenfelder: Regel → `ruleText`, Voraussetzung → `prerequisitesText` + strukturierte `prerequisites`,
  AP-Wert → `apValueText` + strukturierte `cost`, Anmerkung → `note`, Publikation(en) → `publications`.
- Ü18-Inhalte stehen im Wiki unter „Wege der Vereinigungen – ab 18 Jahre“ und bekommen in `books.json` die
  Markierung `adult: true`.
- Nach jeder Erfassung `docs/CONTENT_STATUS.md` aktualisieren.

## Konventionen

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
```

- UI-Tests direkt ausführen, wenn etwas komisch ist: `tests/DSACharacterCreator.App.Tests/bin/Debug/net10.0/DSACharacterCreator.App.Tests`.
  `dotnet test` hat „Test Case Cleanup Failures“ schon einmal als bestanden gemeldet.
- Screenshots der Layout-Tests: Umgebungsvariable `DSA_SHOTS=<ordner>` setzen.
- Adaptives Layout in `MainView.axaml` über Container Queries. Standardwerte gehören in Styles, nicht als lokale
  Attribute, weil lokale Werte die Container-Query-Styles überstimmen.
- ViewModel → View über `DataTemplates` in `App.axaml`, ohne reflektionsbasierten ViewLocator (Trimming auf Android).
