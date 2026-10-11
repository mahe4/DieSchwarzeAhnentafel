# Offene und optionale Punkte

Sammelliste über alle Meilensteine. Einzelheiten zur Erfassung stehen in [CONTENT_STATUS.md](CONTENT_STATUS.md), der
Gesamtplan in [PLAN.md](PLAN.md). Erledigtes wird hier gestrichen.

## Offen: geplante Meilensteine

- **7d PDF-Export** (Desktop und Android) auf Grundlage von `CharacterSheet` (7c).
- Startalter nach Spezies und Erfahrungsgrad (Wiki: Schritt 14, Würfeltabelle) und Größe/Gewicht nach Spezies werden
  nicht ausgewürfelt; die Felder sind frei einzutragen (passt zum optionalen Würfelhelfer).
- **8 Quellenauswahl-UI** pro Charakter plus Standardauswahl in den Einstellungen; Ü18-Kennzeichnung.
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
- **Tabellen im AP-Wert:** Bei 17 Sonderfertigkeiten hängt der Import eine Wiki-Tabelle hinter dem AP-Wert an
  `apValueText` (Belastungsgewöhnung: Rüstungstabelle; Dämonische Attribute, Tierkrieger-Stil, Krakenruf …). In den
  Details erscheint sie als Fließtext. Besser: Tabelle vom AP-Wert trennen und lesbar als Tabelle anzeigen.
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
- Optolith-Import, Würfel- und Probenhelfer, Gruppenverwaltung (PLAN.md, Meilenstein 10).
- Geld in Münzen (D/S/H/K) aufteilen und regionale Währungen umrechnen; Münzgewicht zur Traglast.
- Inhalt von Behältern (was steckt im Rucksack), Lagerorte (Pferd, Herberge) statt nur „dabei“.
- Kampfwerte für beidhändigen Kampf als eigene Zeile (Waffenpaar mit Abzügen).
