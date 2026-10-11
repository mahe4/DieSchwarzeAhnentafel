# Offene und optionale Punkte

Sammelliste über alle Meilensteine. Einzelheiten zur Erfassung stehen in [CONTENT_STATUS.md](CONTENT_STATUS.md), der
Gesamtplan in [PLAN.md](PLAN.md). Erledigtes wird hier gestrichen.

## Offen: geplante Meilensteine

- Startalter nach Spezies und Erfahrungsgrad (Wiki: Schritt 14, Würfeltabelle) und Größe/Gewicht nach Spezies werden
  nicht ausgewürfelt; die Felder sind frei einzutragen (passt zum optionalen Würfelhelfer).
- **9 Inhaltsausbau** Buch für Buch (läuft dauerhaft).

## Offen: Regeln und Inhalte

### Ausrüstung und Kampf
- **Helme und Rüstungsteile:** Ihre Werte stehen im Wiki auf einer eigenen Kostenseite und gehören zur Fokusregel
  Trefferzonen (siehe optionale Regeln).
- **Seltene & einmalige Waffen/Rüstungen** (benannte Einzelstücke) sind nicht erfasst.
- **Ausrüstungspakete:** 8 von 10 Paketseiten sind im Wiki leer (Bürger, Geweihte, Hexen, Höhlenforscher, Magier,
  Reise, Stadt, Wildnis).
- **Tierzubehör** (Seite unter Grundregeln › Tiere) ist nicht erfasst.
- **Preise und Gewichte ohne Zahl:** 18 Gegenstände ohne Preis („50 bis 500 S“, „Preis x 2“, „unbezahlbar“), 31 ohne
  Gewicht; der Spieler trägt eigene Werte ein.
- **Beidhändiger Kampf:** −2 auf AT und Verteidigung bei Angriffen mit beiden Waffen, −4 für die falsche Hand (Vorteil
  Beidhändig, SF Beidhändiger Kampf I–II) sind nicht berechnet; die Waffenwerte gelten für die Haupthand.
- **Situative Boni** bleiben Text: Waffen-/Rüstungsvorteile und -nachteile, Schild gegen Fernkampf, Verteidigungshaltung,
  Reichweiten- und Größenmodifikatoren, improvisierte Waffen, Holzwaffen (−3 TP).
- **Fassungsvermögen von Behältern** (Rucksack 50 Stein …) wird nicht geprüft.

### Magie und Götterwirken
- **Talismane** sind nicht erfasst.
- 17 Zauber-Teile von Professionen ohne Zuordnung (`unparsedSpells`), 44 SF-Teile (`unparsedSpecialAbilities`).
- Lieblingszauber u. Ä. kosten je Steigerungsfaktor; vorerst Ausprägung „A-Zauber“ … „D-Zauber“ statt Zauberauswahl.
- 165 magische/karmale SF mit Voraussetzungsteilen zum Prüfen von Hand (Pakte, Vampir-/Lykanthropen-Status …).

### Vorlagen, Vor-/Nachteile, Sonderfertigkeiten
- **Tabellen in Wiki-Texten:** Bei Sonderfertigkeiten werden Tabellen erkannt und getrennt (`tables`). Zwei
  Seiten haben Beispiel-Tabellen, deren Zellen nicht zum Text passen (Neue Körperteile, Magnum Opus); dort steht der
  Inhalt weiter als Text in der Anmerkung zum AP-Wert. Vor-/Nachteile und Zauber sind nicht auf Tabellen geprüft.
- 8 Vor-/Nachteile ohne festen AP-Wert (Spieler trägt ein), 19 Voraussetzungsteile nur als Hinweis.
- 53 profane SF mit Voraussetzungsteilen zum Prüfen von Hand.
- 3 Professionen mit Resten in `unparsed`; Varianten ändern SF u. Ä. nur als Text.
- Leere Kulturseiten im Wiki: Stammesgoblins, Tocamuyac, Brobim.

## Geklärt

- **Belastung und FK** (2026-10-11): Belastung senkt den FK nicht. Der Zustand Belastung und die Seite „Rüstung und
  Belastung“ nennen nur Talente, AT, Verteidigung, INI und GS; die Fernkampf-Modifikatoren im Wiki führen Belastung
  nicht auf. Das offizielle Foundry-System bestätigt das: Belastung für Fernkampf ist dort eine abschaltbare Hausregel
  („Laut Grundregeln betrifft Belastung den Fernkampf nicht“).
  Falls das Grundregelwerk oder ein Errata etwas anderes sagt, ist es eine Zeile in `CharacterCalculator.Equipment`.
- **Zukauf von LeP/AsP/KaP** (2026-10-11): nur nach der Heldenerschaffung, wie im Wiki. Rückkauf verlorener AsP/KaP
  ist auch bei der Erschaffung möglich.

## Abgleich mit dem offiziellen Foundry-System (2026-10-11)

Verglichen mit <https://github.com/Plushtoast/dsa5-foundryVTT> (Grundwerte, Kostentabellen, Zukauf, Belastung,
Traglast, Waffen, Schilde, Höchstwerte).

**Übereinstimmend:** LeP, SK, ZK, AW, INI, AsP/KaP (Leiteigenschaft, bei manchen Traditionen halb, aufgerundet),
Kostentabelle A–E, Zukauf nach Tabelle D und Rückkauf 2 AP, Höchstwert Fertigkeiten/Kampftechniken (Eigenschaft + 2),
Belastungsgewöhnung (BE minus Stufe, zusätzliche Abzüge entfallen erst darüber), Traglast KK × 2, Gewicht der
angelegten Rüstung zählt nicht, Schild-Parade mit doppeltem PA-Mod, passiver Bonus der besten Parierwaffe/des Schilds,
Schadensbonus über der Schadensschwelle, kein Belastungsabzug auf FK.

**Behoben:**
- Herausragende Fertigkeit/Kampftechnik heben jetzt das Maximum bei der Erschaffung und das endgültige Maximum
  (auch für Zauber/Liturgien, dort auch die Grenze 14) – fehlte vorher.
- Zweihändig geführte Waffen bekommen keinen Bonus von Schild oder Parierwaffe (wie in Foundry).
- Anzahlgrenzen aus dem Regeltext: Herausragende Fertigkeit höchstens viermal, je Fertigkeit zweimal (zweimal dieselbe
  Fertigkeit war vorher fälschlich als „bereits vorhanden“ gesperrt); Herausragende Kampftechnik höchstens zweimal,
  jede Kampftechnik einmal.

**Bewusst anders (Wiki geht vor):**
- **Traglast an der Grenze:** Foundry rechnet ⌈(Gewicht − Traglast − 4) / 4⌉, also erst ab mehr als 4 Stein darüber
  Stufe I. Das Wiki sagt „für jeweils 4 volle weitere Stein“; die App gibt bei genau 4 (8, 12 …) Stein darüber schon
  die nächste Stufe. Unterschied nur bei genau 4, 8, 12 … Stein Überlast.
- **Zukauf AsP/KaP bei halber Leiteigenschaft** (z. B. Animisten): Foundry begrenzt auf die halbe Leiteigenschaft,
  das Wiki nennt „in Höhe ihrer Leiteigenschaft“; die App folgt dem Wiki.
- **Zauber/Liturgien-Höchstwert:** Foundry erlaubt ohne Merkmals-/Aspektkenntnis pauschal 14, mit Kenntnis mindestens
  14. Die App nimmt Probeneigenschaft + 2, ohne Kenntnis höchstens 14 – Zauber sind Fertigkeiten, und für diese gilt
  laut Wiki „höchste beteiligte Eigenschaft + 2“.
- **Rückkauf vor Zukauf:** Foundry prüft nicht, ob verlorene AsP/KaP zuerst zurückgekauft sind; die App schon (Wiki).
- **Leiteigenschaft mit zwei Schwellen** („GE 15/KK 14“): Foundry kennt nur eine Schwelle je Waffe; die App rechnet je
  Eigenschaft.
- **AT-Abzug großer Schilde neben einer Parierwaffe:** Foundry nimmt den größten (günstigsten) Wert aller Zweitwaffen,
  die App den Abzug des Schilds.

**Nur in Foundry (offen/optional):** falscher Griff (Zweihandwaffe einhändig u. Ä.), Abnutzung von Waffen und
Rüstungen, Reiten (−1 BE), Münzgewicht als Option, Behälter mit Inhalt.

## Optional

### Optionale Regeln aus dem Wiki
- **Eigenschaftsobergrenze** (Heldenerschaffung).
- **Variables Startkapital nach Erfahrungsgrad:** je Grad über „Erfahren“ +200 S, darunter −200 S.
- **Sprachspezialisierungen** (Heldenerschaffung, Schritt 4).
- **Fokusregel Trefferzonen** mit Helmen und Rüstungsteilen.
- **Reich teurer:** Das Wiki schlägt vor, Reich mit 2 bis 5 AP je Stufe anzusetzen, wenn Geld in der Kampagne wichtig
  ist.

Dafür bräuchte es eine Einstellung „optionale Regeln“ je Held (oder global mit Überschreibung je Held).

### Funktionen
- **Steigerungshelfer für Vertrautentiere** (gewünscht 2026-10-11). Regeln aus dem Wiki (recherchiert, noch nichts
  erfasst):
  - *Grundregeln › Vertrautentiere* (`GR_Vertraute.html`; Regelwerk S. 278 f., Kodex der Magie S. 366 ff.):
    Voraussetzung SF **Vertrautenbindung** (20 AP, Tradition Hexe, „gegebenenfalls andere Tradition“; bei der
    Erschaffung erworben, startet die Hexe mit gebundenem Tier). Traditionen: Hexen, Goblinzauberinnen, Geoden,
    Brobim-Geoden, Zibiljas – je nur ein Vertrauter, nach dessen Tod frühestens nach 1 Monat ein neuer.
  - Tierart je Tradition: Hexen nach Schwesternschaft (Schöne der Nacht Katze, Schwarze Witwen Spinne, Fahrende
    Gemeinschaft Affe, Schwesternschaft des Wissens Schlange, Seherinnen von Heute und Morgen Rabe, Töchter der Erde
    Kröte, Verschwiegene Schwesternschaft Eule); Geoden nach Element (Eis Vielfraß, Erz Hund/Weißer Koscher, Feuer
    Wildkatze, Humus Kröte, Luft Falke, Wasser Schlange); Brobim-Geoden Feuer/Luft/Erz; Goblinzauberinnen Iltis, Kröte,
    Opossum, Rabe, Schnecke, Vielfraß; Zibiljas Iltis, Schlange. „Besondere Vertrautentiere“: Aventurische
    Tiergefährten S. 116.
  - **Werte:** Grundwerte der Tierart aus dem Bestiarium (z. B. `Best_Scheunenkatz.html`: Eigenschaften, LeP, INI,
    VW, SK, ZK, GS, Angriffe mit AT/TP/RW, RS/BE, Vor-/Nachteile, SF, Talente) plus Boni: körperliche und geistige
    Eigenschaften +1, +10 LeP, +1 RS, +1 SK, +1 ZK, 15 AsP. Regeneration wie bei Helden.
  - **AP:** ein Viertel der AP, die die Hexe nach der Erschaffung bekommt (nicht von ihren abgezogen). Steigern nach
    den Heldenregeln: Eigenschaften, Talente, LE und AE; Kampftechniken des Tiers (Biss, Schnabel …) in Spalte C;
    Kampf-SF theoretisch möglich (Meisterentscheid).
  - **Vertrautentricks** (`VSF_Vertrautentricks.html`, 35 Seiten `VSF_*`/`SVF_*`): Felder Wirkung, Tierarten,
    AsP-Kosten, Wirkungsdauer, Merkmal, AP-Wert, Publikation. Zwiegespräch und Hexensinn hat jeder Vertraute
    kostenlos. Keine Probe, 1 Aktion; gelten für Antimagie als Zauber mit QS 1. Abweichungen je Tradition: Geoden
    (kein Fluchbote, Hexensinn = Geodensinn, Gefährte in neuer Gestalt → Zwergengestalt), Goblinzauberinnen (kein
    Fluchbote, Goblinsinn, Goblingestalt, Krötengift = Schneckengift), Zibiljas (kein Fluchbote und keine neue
    Gestalt, Zibiljasinn).
  - **Abrichten** (Fokusregel, Kodex des Schwertes S. 157): Vertraute zählen als domestiziert, Proben der Hexe +2,
    bis zu **zwei** Ausbildungsaufsätze (sonst einer), nur die Herrin bildet aus. Tricks und Ausbildungsaufsätze
    stehen unter `TSF_Tricks.html` und `TSF_Ausbildungsaufsätze.html` (Aufsätze über Tierkunde-Sammelprobe, Modifikator
    je Tierart).
  - **Optionale Regel** „Schnellere Steigerungsmöglichkeiten für Tiere“ (Aventurische Tiergefährten S. 99): eigenes
    AP-Konto in Höhe der Helden-AP, auch für Vertraute.
  - Umsetzung später: Bestiarium-Werte der Vertrautentierarten und die Vertrautentricks importieren; Vertrauter als
    eigener Teil des Helden (Tierart, AP-Protokoll mit Viertel-Gutschriften, Steigerung nach Kostentabelle, Tricks nach
    Tierart/Tradition filtern), Ausgabe auf dem Charakterbogen (Heldendokument hat eine Seite „Tier/Vertrauter“).
    Dieselbe Mechanik deckt tierische Begleiter ab (Kodex des Schwertes S. 153: ebenfalls ¼ AP, AT und VW direkt in
    Spalte C).
- Optolith-Import, Würfel- und Probenhelfer, Gruppenverwaltung (PLAN.md, Meilenstein 10).
- Geld in Münzen (D/S/H/K) aufteilen und regionale Währungen umrechnen; Münzgewicht zur Traglast.
- Inhalt von Behältern (was steckt im Rucksack), Lagerorte (Pferd, Herberge) statt nur „dabei“.
- Kampfwerte für beidhändigen Kampf als eigene Zeile (Waffenpaar mit Abzügen).
- **PDF:** Teilen per Android-Share-Intent (jetzt nur Speichern); Druckvorschau in der App; Regeltexte auch für
  Sonderfertigkeiten und Vor-/Nachteile (für Zauber, Liturgien usw. gibt es einen eigenen Export). Vom Ulisses-Heldendokument fehlen: Spalte „Wirkung“ bei Zaubern (Wiki hat nur den langen Regeltext),
  Seitenangaben, Routineproben-Tabelle, Eigenschaftsmodifikationen, Tier/Vertrauter, AP-Berechnungsseiten,
  Zonenrüstungen (Fokusregel), Bruchfaktor/Strukturpunkte von Waffen und Schilden, Spezialisierungen in „Anmerkung“.
- Charakterbogen in der App folgt noch der alten Gliederung (PDF hat den Aufbau des Heldendokuments).
- **Quellenauswahl:** Bücher sind nur nach Grundregeln/Weitere/Ü18 gruppiert, nicht nach Art (Regelwerk, Spielhilfe,
  Abenteuer); das Wiki ordnet die Publikationen nicht ein. Weitere App-Einstellungen gibt es noch nicht (nur
  Standardquellen).
