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
| Spezies | ⬜ | | | |
| Kulturen | ⬜ | | | |
| Professionen | ⬜ | | | |
| Vor-/Nachteile | ⬜ | | | |
| Sonderfertigkeiten | ⬜ | | | |
| Magie | ⬜ | | | |
| Götterwirken | ⬜ | | | |
| Ausrüstung | ⬜ | | | |

## Offene Punkte

- `Gewandheit` ist im Wiki bei den Kampftechniken falsch geschrieben; der Import bildet es auf `GE` ab.
- Basiswerte mit Spezies-Grundwert (LeP, SK, ZK, GS) sowie AsP/KaP folgen mit Spezies bzw. Vorteilen.
- Die optionale Regel „Eigenschaftsobergrenze“ ist noch nicht umgesetzt.
