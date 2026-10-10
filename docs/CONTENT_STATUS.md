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
| Vor-/Nachteile | ⬜ | | | |
| Sonderfertigkeiten | ⬜ | | | |
| Magie | ⬜ | | | |
| Götterwirken | ⬜ | | | |
| Ausrüstung | ⬜ | | | |

## Offene Punkte

- `Gewandheit` ist im Wiki bei den Kampftechniken falsch geschrieben; der Import bildet es auf `GE` ab.
- AsP/KaP folgen mit den Vorteilen Zauberer/Geweihter (Meilenstein 4/6).
- Leere Kulturseiten im Wiki: Stammesgoblins, Tocamuyac, Brobim (nicht übernommen).
- Wüstenelfen haben im Wiki keine Quellenangabe → Pseudo-Quelle `wiki`.
- Kulturpaket Zahori: Wiki nennt 27 AP, die Talente kosten 28 AP (Wiki-Wert übernommen, im Test als bekannte Abweichung).
- 7 Professionen mit Werten je Erfahrungsgrad („10/12/12“) bzw. Punkteverteilung: nicht zugeordnete Teile stehen in
  `unparsed` und werden in der App als „von Hand übernehmen“ angezeigt.
- Varianten: Werteänderungen („X statt Y“) strukturiert; SF-Änderungen u. Ä. als Text (`variants[].text`).
- Eigenschaftssumme bei der Erschaffung zählt gekaufte Punkte ohne Spezies-Änderungen (Auslegung von Schritt 5).
- Quellenangaben werden in `tools/wiki/books.py` normalisiert (`CANONICAL`, `clean()`): Tippfehler, Untertitel,
  Jahreszahlen, Zusätze wie „[Als …]“.
- Die optionale Regel „Eigenschaftsobergrenze“ ist noch nicht umgesetzt.
