# Offene und optionale Punkte

Sammelliste über alle Meilensteine. Einzelheiten zur Erfassung stehen in [CONTENT_STATUS.md](CONTENT_STATUS.md), der
Gesamtplan in [PLAN.md](PLAN.md). Erledigtes wird hier gestrichen.

## Offen: geplante Meilensteine

- **9 Inhaltsausbau** Buch für Buch (läuft dauerhaft).

## Offen: Regeln und Inhalte

### Ausrüstung und Kampf
- **Helme und Rüstungsteile:** Ihre Werte stehen im Wiki auf einer eigenen Kostenseite und gehören zur Fokusregel
  Trefferzonen (siehe optionale Regeln).
- **Seltene & einmalige Waffen/Rüstungen** (benannte Einzelstücke) sind nicht erfasst.
- **Ausrüstungspakete:** 8 von 10 Paketseiten sind im Wiki leer (Bürger, Geweihte, Hexen, Höhlenforscher, Magier,
  Reise, Stadt, Wildnis).
- **Tierzubehör** (Seite unter Grundregeln › Tiere) ist nicht erfasst.
- **Beidhändiger Kampf:** Die zweite Zeile je Waffe zeigt AT/PA beim Angriff mit beiden Waffen und für die falsche
  Hand. Die Basismanöver-Beschränkung und Patzerfolgen bleiben Text. Die SF Beidhändiger Kampf
  wirkt nur für die Kampftechniken aus ihrem Feld „Kampftechniken“.
- **Würfelproben im Charakterbogen** (seit 2026-10-12, `Core/Rules/CheckRolls.cs`, `DiceRollViewModel`): Talente und
  Zauber/Liturgien mit QS, AT/PA/FK je Kampftechnik und Waffe (auch beidhändig/falsche Hand) mit Bestätigungswurf und
  TP; Belastung wird bei Talenten mit Belastung „ja“ als Modifikator vorgegeben. Nicht berücksichtigt: Schmerz und
  andere Zustände, SK/ZK des Ziels (nur Hinweis), Fokusregeln (Trefferzonen, Patzertabellen), Schicksalspunkte. Im
  Einstellungsdialog ausblendbar (`AppSettings.ShowDiceButtons`).
- **Umstände bei Kampfproben** (seit 2026-10-12, `Core/Rules/CombatSituations.cs`): Im Würfeldialog ankreuzbar und in den
  Modifikator eingerechnet – Waffe des Gegners (Nahkampfreichweite), beengte Umgebung (Reichweite bzw. Schildgröße,
  `shieldSize` aus der Anmerkung des Schilds), winziger Gegner, Sicht 1–3, vorteilhafte Position, Angriff von hinten,
  wiederholte Verteidigung, Fernkampfangriff (nur Schildparade/Ausweichen); im Fernkampf Distanz (mit ±1 TP), Größe,
  Bewegung von Ziel und Schütze, Zielen, Sicht, Reittier, Haken, Kampfgetümmel. Nur als Hinweis: Sicht Stufe 4 (AT
  halbiert bzw. nur bei einer 1), Trab, große/riesige Gegner (nur Schild/Ausweichen). Die Auswahl gilt nur, solange der
  Dialog offen ist.
- **Ausweichen** (seit 2026-10-12): eigener Würfelknopf im Kampfteil des Bogens. Dauerhafte Boni im AW-Wert
  (`EquipmentState.DodgeBonus`): Verbessertes Ausweichen (nur ohne BE und ohne zusätzliche GS/INI-Abzüge), Weg des
  Tänzers (ohne Belastung), Feenblut (Blütenfee, Rüstung höchstens RS 1). Situative Boni des Helden für PA und AW
  (VW-Boni gelten für beide): Verteidigungshaltung, Fedorino-Stil, Unauer Schule, Blitzreflexe, Rhodenstein-Stil,
  Formationen, Odilmar-Stil, Elementare Verteidigung, Schutz der Muttersau, Abzüge nach eigenen Manövern
  (Blutgrätsche, Klinge drehen, Machtvoller Sturmangriff, Durchgezogener Tritt), überlange Waffe beim Ausweichen.
  Kampfstile gelten bei der Parade nur mit ihren Kampftechniken. Nicht erfasst: Formationen ohne eigene SF (Bonus von
  Mitstreitern), Boni von anderen (Anführer, Haltet durch!, Rondras-Schutz-Stil …), Paktgeschenk Geschwindigkeit
  (KdV × 2) – dafür gibt es den freien Modifikator. Boni auf AT aus SF (Formation, Rhodenstein-Stil +1 AT …) fehlen noch.
- **Situative Boni** bleiben Text: Waffen-/Rüstungsvorteile und -nachteile, Verteidigungshaltung, überlange Waffen beim
  Ausweichen (−4), improvisierte Waffen, Holzwaffen (−3 TP).
- **Fassungsvermögen von Behältern** (Rucksack 50 Stein …) wird nicht geprüft.

### Magie und Götterwirken
- **Zauber- und Liturgieerweiterungen** (Fokusregel, immer aktiv; seit 2026-10-11): FW, AP und vorausgesetzte Erweiterung
  werden geprüft, Fremdzauber haben keine. Nicht geprüft: „höhere Stufen derselben Erweiterung lassen sich nicht
  kombinieren“ (betrifft nur das Wirken). 9 Voraussetzungen bleiben Text, weil sie keine Erweiterung sind („Das Wesen hat
  mindestens Größenkategorie Groß“) oder auf Erweiterungen verweisen, die es auf der Wiki-Seite nicht gibt
  (Rinderruf, Traumbild, Einhorngestalt, Destructibo). Zusätzliche AsP/KaP-Kosten stehen nur im Regeltext.
- **Talismane** sind nicht erfasst.
- 1 Zauber-Teil von Professionen ohne Zuordnung (`unparsedSpells`: „Kleiner Windstoß“ des Efferdgeweihten gibt es als
  Liturgie im Wiki nicht), 32 SF-Teile (`unparsedSpecialAbilities`).
- Lieblingszauber u. Ä. kosten je Steigerungsfaktor; vorerst Ausprägung „A-Zauber“ … „D-Zauber“ statt Zauberauswahl.
- 125 magische/karmale SF mit Voraussetzungsteilen zum Prüfen von Hand (Pakte, Vampir-/Lykanthropen-Status …).

### Vorlagen, Vor-/Nachteile, Sonderfertigkeiten
- **Tabellen in Wiki-Texten:** Bei Sonderfertigkeiten werden Tabellen erkannt und getrennt (`tables`). Zwei
  Seiten haben Beispiel-Tabellen, deren Zellen nicht zum Text passen (Neue Körperteile, Magnum Opus); dort steht der
  Inhalt weiter als Text in der Anmerkung zum AP-Wert. Vor-/Nachteile und Zauber sind nicht auf Tabellen geprüft.
- 8 Vor-/Nachteile ohne festen AP-Wert (Spieler trägt ein), 19 Voraussetzungsteile nur als Hinweis.
- 47 profane SF mit Voraussetzungsteilen zum Prüfen von Hand.
- 3 Professionen mit Resten in `unparsed`; Varianten ändern SF, Zauber und Liturgien u. Ä. nur als Text (z. B. Diener der Erdmutter: „Archofaxius 6 statt Ignifaxius“ – die Vorlage behält Ignifaxius, der Tausch bleibt von Hand).
- Leere Kulturseiten im Wiki: Stammesgoblins, Tocamuyac, Brobim.
- **Wiki-Fehler** (Stand 2026-10-12, alle Einträge geprüft): Was im Wiki so steht, aber nicht zusammenpasst, listet
  `tools/wiki/check_packages.py` in [WIKI_ABWEICHUNGEN.md](WIKI_ABWEICHUNGEN.md) unter „Weitere Auffälligkeiten“
  (u. a. 57 Zauber-/Liturgieerweiterungen mit AP, die nicht zum Steigerungsfaktor passen, Pakt-SF mit Stufen, aber
  einem AP-Wert, Vor-/Nachteile in der falschen Liste). Die App übernimmt diese Werte unverändert; offen ist, sie mit
  den Büchern abzugleichen bzw. Ulisses zu melden. Eindeutige Tippfehler in Namen korrigiert der Import (Liste am Ende
  derselben Datei). Unklar, nicht automatisch erkannt: Raubtiergeruch setzt laut Wiki Unfähig (Reiten) und Unfähig
  (Tierkunde) *voraus*; Blindkampf III hat keinen AP-Wert (laut Anmerkung nur für Wesen).

### Würfelhelfer der persönlichen Angaben
- **Geburtsdatum** ohne Jahr (das aktuelle aventurische Jahr hängt von der Runde ab) und ohne die Namenlosen Tage
  (Wiki: bei Rahja-Wurf 20 erneut würfeln, 16–20 = verfluchter Tag – nur wenn man so einen Helden spielen will).
- **Namen:** Rufnamen (Achaz), Ehren-, Kriegs- und Beinamen, Adelsfamilien („von …“, Horasier „dy“), Stämme und die
  Zusatzsilben der Adoru werden nicht gewürfelt. Ohne Namenslisten: Räuberbande („je nach ursprünglicher Kultur“),
  Orkland weiblich (nur Beschreibung), Adoru-Familiennamen. Nachtalben bekommen den Ehrennamen erst ab Meisterlich.
- **Aussehen:** Hautfarbe hat keine Tabelle. Die Würfeltabelle (Untergruppe) wird nach der Kultur vorausgewählt, wo
  das Wiki sie den Menschenvölkern bzw. Elfen zuordnet; Achaz-Arten und Orkstämme wählt man von Hand.

## Geklärt

- **Sonderfertigkeiten mit Stufen** (2026-10-11): Jede Stufe wird einzeln erworben (Stufe II setzt Stufe I voraus), die
  Kosten werden aufsummiert: Wuchtschlag III kostet 15 + 20 + 25 = 60 AP, nicht 25. Die App zählte vorher nur den Wert
  der höchsten Stufe (Stufe II → III kostete im Protokoll nur die Differenz, z. B. 5 AP). Gefunden beim Abgleich mit
  Optolith; Tiergefährten rechneten schon richtig. Gespeicherte Helden mit solchen SF haben dadurch weniger freie AP.
- **AP-Abweichung zu Optolith** (Bhuk, Quin Dargel, 2026-10-11): vollständig erklärt. Neben den Stufenkosten (Fehler
  der App, behoben) bleiben: Mächtiger Rundumschlag kostet in Optolith 10 AP, im Wiki 20 AP (Kodex des Schwertes S. 343;
  das Wiki gilt), und `SPELL_53` (Optolith-1.0-ID, in Optolith 1.5 nicht mehr vorhanden). Hruruzat fehlte nur in der
  Zuordnung (bei uns „Hruruzat-Stil“); mit ihm wurden 40 weitere Namensvarianten zugeordnet. Zauber-/Liturgieerweiterungen
  gibt es seit 2026-10-11 (Quin +22 AP, gleiche Kosten wie Optolith). Nadia: Optolith rechnet selbst 1254 von 1250 AP
  (die Datei ist dort schon überzogen); eine gekaufte zweite Ortskenntnis ging beim Import verloren (behoben).

- **Belastung und FK** (2026-10-11): Belastung senkt den FK nicht. Der Zustand Belastung und die Seite „Rüstung und
  Belastung“ nennen nur Talente, AT, Verteidigung, INI und GS; die Fernkampf-Modifikatoren im Wiki führen Belastung
  nicht auf. Das offizielle Foundry-System bestätigt das: Belastung für Fernkampf ist dort eine abschaltbare Hausregel
  („Laut Grundregeln betrifft Belastung den Fernkampf nicht“).
  Falls das Grundregelwerk oder ein Errata etwas anderes sagt, ist es eine Zeile in `CharacterCalculator.Equipment`.
- **Zukauf von LeP/AsP/KaP** (2026-10-11): nur nach der Heldenerschaffung, wie im Wiki. Rückkauf verlorener AsP/KaP
  ist auch bei der Erschaffung möglich.
- **„App reagiert nicht“ am Handy** (2026-10-11): Ursache waren die Container Queries. Beim Aufbau großer Seiten
  (Charakterbogen, Ausrüstung) wurden sie über alle geladenen Seiten neu ausgewertet. Mit allen geöffneten Seiten
  kostete der Bogen rund 800 MB Allokationen und über 30 große GCs, in Einzelfällen hing er 12 bis 30 s. Ersetzt durch
  `WidthClasses`: Der Bogen braucht jetzt etwa 30 MB und 1,3 s, auch die anderen Seitenwechsel sind schneller.
  Am Desktop (CoreCLR) war das nicht nachstellbar, nur am Gerät. Messen: `DSA-Zeit` im Logcat.

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
- **Tiergefährten: Vertrautentiere und tierische Begleiter** (gewünscht 2026-10-11, Layout abgestimmt, noch nichts
  umgesetzt). Regeln aus dem Wiki:
  - *Grundregeln › Vertrautentiere* (`GR_Vertraute.html`; Regelwerk S. 278 f., Kodex der Magie S. 366 ff.):
    Voraussetzung SF **Vertrautenbindung** (20 AP, Tradition Hexe, „gegebenenfalls andere Tradition“; bei der
    Erschaffung erworben, startet die Hexe mit gebundenem Tier). Traditionen: Hexen, Goblinzauberinnen, Geoden,
    Brobim-Geoden, Zibiljas – je nur ein Vertrauter, nach dessen Tod frühestens nach 1 Monat ein neuer.
  - Tierart je Tradition: Hexen nach Schwesternschaft (Schöne der Nacht Katze, Schwarze Witwen Spinne, Fahrende
    Gemeinschaft Affe, Schwesternschaft des Wissens Schlange, Seherinnen von Heute und Morgen Rabe, Töchter der Erde
    Kröte, Verschwiegene Schwesternschaft Eule); Geoden nach Element (Eis Vielfraß, Erz Hund/Weißer Koscher, Feuer
    Wildkatze, Humus Kröte, Luft Falke, Wasser Schlange); Brobim-Geoden Feuer/Luft/Erz; Goblinzauberinnen Iltis, Kröte,
    Opossum, Rabe, Schnecke, Vielfraß; Zibiljas Iltis, Schlange. „Besondere Vertrautentiere“: Aventurische
    Tiergefährten S. 116. Jede Tierart lässt sich als Vertrauter wählen (sonst Hinweis „besonderes Vertrautentier,
    Meisterentscheid“). Die Auswahl zeigt bei üblichen Vertrauten fremder Traditionen, welche Tradition mit welcher
    Schwesternschaft bzw. welchem Element nötig ist (`CompanionCalculator.FamiliarRequirements`, `choiceLabel` in
    `familiars.json`); Tierarten ohne übliche Tradition stehen dort gesammelt unter „Besondere Vertraute (nur
    Meisterentscheid)“ und bekommen einen eigenen Hinweis. Die Schwesternschaft des Helden selbst ist nicht erfasst.
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
    AP-Konto in Höhe der Helden-AP, auch für Vertraute – wird nicht umgesetzt (siehe unten).
  - **Festgelegt (2026-10-11): Es gilt der aktuelle Stand = Kodex des Schwertes (08/2023, S. 153–158) bzw. das Wiki.**
    Recherche: Aventurisches Kompendium II (2018) und Aventurische Tiergefährten (2019, 2. Aufl. 2020) gaben Begleitern
    ⅒ der AP und Ausbildungsaufsätze für 80–138 AP; der Kodex des Schwertes hat beides geändert (¼, Aufsätze 10–17 AP,
    im Wiki als Errata vermerkt). Vertraute: ¼ (Regelwerk, Kodex der Magie 2023 unverändert). Tricks unverändert.
    - AP: Vertraute und tierische Begleiter je **¼** der AP, die der Held nach der Erschaffung bekommt (nicht vom Helden
      abgezogen). Gerundet wird über die Summe seit der Bindung/Aufnahme, nicht je Gutschrift.
    - Die optionale Regel „eigenes AP-Konto in Höhe der Helden-AP“ (Tiergefährten S. 99) wird nicht angeboten: mit den
      gesenkten Aufsatz-Kosten des Kodex wäre das Konto viel zu groß (Entscheidung 2026-10-11).
    - Je Tier lassen sich **Bonus-AP nach Meisterentscheid** von Hand gutschreiben (eigene Einträge im Protokoll des
      Tiers) – ausdrücklich gewünscht.
    - Tiere bei der Heldenerschaffung: können nach Meisterentscheid schon AP ausgegeben haben („Startausbildung“);
      Fairness-Ausgleich zwischen mehreren Begleitern als Hinweis (Kodex des Schwertes S. 153).
    - Loyalität ist ein Talent des Tiers (Spalte B, MU/KL/IN, Start 4 domestiziert / 0 wild), bezahlt mit den AP des
      **Helden**; bei der Erschaffung ohne Probe steigerbar (S. 157).
    - Ausbildungsaufsätze und Tricks kosten AP des Tiers; ein Aufsatz (Reittier zusätzlich kombinierbar), Vertraute zwei;
      domestizierte Tiere zusätzlich bis zu KL/2 Tricks, Wildtiere höchstens KL/2 (S. 155–157). Mögliche Aufsätze und
      Mods je Tierart aus der Tabelle der Wiki-Seite Ausbildungsaufsätze.
    - Tiervor-/-nachteile (Tiergefährten S. 117): bei der Aufnahme je höchstens 30 AP; die der Tierart kosten nichts.
    - Steigern wie Helden: Eigenschaften, Talente, LE/AE; Vertraute: Kampftechniken des Tiers in Spalte C; Begleiter: AT
      und VW direkt in Spalte C.
  - **Stand:** Schritt 1 (Import, siehe CONTENT_STATUS.md), Schritt 2 (Core: `Character.Companions`, Format 8, eigenes
    Protokoll je Tier, `CompanionCalculator`, `CompanionTests`) und Schritt 3 (App: Bereich „Tiergefährten“,
    `CompanionEditorViewModel` mit eigenen Tabs, `CompanionPageTests`, Handy-Prüfung, am Fairphone 4 getestet) und Schritt 4
    (PDF: je lebendem Tier eine Seite „Tier/Vertrauter“; im Bogen der App nicht, dort gibt es die eigene Tier-Ansicht)
    erledigt.
  - **App (2026-10-11):** Steigern per −/+ schreibt `RaiseEntry`-Schritte ins Protokoll des Tiers (direkt folgende
    Steigerungen desselben Werts werden zusammengefasst, Senken nimmt die letzte eigene zurück). ✕ bei Fähigkeiten und im
    Protokoll löscht den Eintrag (Korrektur); `RemoveEntry` (Ablegen mit AP) bietet die App nicht an. Neue Tiere zählen in
    der Steigerungsphase erst ab der nächsten AP-Gutschrift des Helden (änderbar unter Grundlagen). Weitere Talente:
    „＋ Talent“ steigert gleich auf 1. Am Handy zeigt die Kopfzeile weiter den Heldennamen (Tiername als Seitenüberschrift).
  - **Umsetzung in Core (2026-10-11):** Steigerungen im Protokoll des Tiers wirken als Schritte auf den aktuellen Wert
    (ein später erworbener Aufsatz/Vorteil verschiebt nichts). LeP/AsP-Zukauf nach Tabelle D (Warnung über KO).
    Ablegen gibt die Kosten zurück (bei Nachteilen kostet es deren AP-Wert). Ein eigener AP-Wert (`ManualAp`) geht immer
    vor. Boni „A- oder B-FW“ über die gewählte Option (sonst die erste). Prozent-Boni (LeP, GS) auf den Wert der Tierart,
    kaufmännisch gerundet. Nicht modelliert: Heldenwuchs (LeP = KO), Eigenwillig (Loyalität 0), Halbwild,
    Extrem friedliebend und andere Vorteile mit reinem Regeltext; Trick-Erlernen per Probe (nur AP);
    Fairness-Ausgleich zwischen Begleitern bei der Erschaffung (noch kein Hinweis).
  - **Oberfläche (Vorschau abgestimmt 2026-10-11):**
    - Eigener Bereich „Tiergefährten“ beim Helden (zwischen Ausrüstung und Charakterbogen): Liste aller Tiere als
      Karten; Vertraute oben, mit Akzentrand und Abzeichen „✦ Vertrauter“; Begleiter darunter; verstorbene Tiere
      ausgegraut am Ende. Je Karte freie AP und AP-Regel. Knöpfe „＋ Vertrautentier“/„＋ Tierischer Begleiter“, am
      Handy kurz „＋ Vertrauter“/„＋ Begleiter“ (nicht nur „＋“).
    - Ein geöffnetes Tier **ersetzt die Navigation** (Desktop links, Handy unten), oben „‹ Tiergefährten von …“; die
      rechte Spalte und die AP-Anzeige zeigen das Tier.
    - Tabs: **Ergebnis** (öffnet zuerst; Kästen Eigenschaften, Basiswerte, Angriffe, Talente, Tricks, Vor-/Nachteile,
      SF, Aussehen) · Grundlagen (Art, Tierart, AP-Regel, gebunden/aufgenommen ab, Startausbildung, Persönliches,
      Status) · Eigenschaften (mit LE/AE und Kampf) · Vor-/Nachteile · Sonderfertigkeiten · Talente (mit Loyalität,
      markiert „AP vom Helden“) · Vertrautentricks (nur Vertraute) · Ausbildung & Tricks · Protokoll.
- Würfel- und Probenhelfer, Gruppenverwaltung (PLAN.md, Meilenstein 10).
- **Optolith** (Import/Export erledigt 2026-10-11, siehe [OPTOLITH.md](OPTOLITH.md)): Der Export ist nur über den
  Rundweg in der App und gegen echte Optolith-1.0–1.5-Dateien geprüft, noch nicht durch Laden in Optolith selbst.
  Nicht übernommen werden Tiere, Pakte und permanent verlorene LeP (fehlen in der App), dazu rund 150 Zauber- und
  Liturgieerweiterungen, die seit Optolith 1.5 in neueren Büchern umbenannt wurden (z. B. „Zwei Personen“ → „Zwei
  Lebewesen“; Liste in OPTOLITH.md); viele
  Magierakademien haben in unseren Wiki-Daten keine Varianten (Zweige), die Variante geht dann verloren.
- **Dark Aid** (Import/Export erledigt 2026-10-11, siehe [DARKAID.md](DARKAID.md)): Gegen 46 echte Dark-Aid-15/16-Helden
  geprüft (alle Werte gleich, Rundweg ohne Verlust), der Export aber noch nicht durch Laden in Dark Aid selbst. Offen:
  - AP-Summe weicht bei einigen Helden ab: Hexen +10, Graumagier (Festum) +6/+15, Geode +26, Klingensturm-Kämpfer +5
    (Klingensturm kostet in Dark Aid 25 AP, im Wiki 30 AP – das Wiki gilt). Dark Aid speichert keine AP-Summe; zum
    Prüfen die Helden in Dark Aid öffnen und die AP vergleichen.
  - Zwerge: Dark Aid speichert Dunkelsicht, Immunität gegen Tulmadron und Unfähig (Schwimmen) beim Helden; unsere
    Zwerg-Daten haben keine automatischen Vor-/Nachteile, die Einträge kommen deshalb als gekaufte. Immunität gegen
    (Gift) hat in der App keinen festen AP-Wert (Dark Aid: Giftstufe/2).
  - Nicht zugeordnet (Liste in DARKAID.md): u. a. Denkrichtungen und Schwesternschafts-SF der Magier/Hexen, Runen,
    Ü18-Inhalte, eigene Gegenstände; Fokusregeln werden beim Export fest gesetzt (Zauber-/Liturgieerweiterungen an).
- **Vorteile mit Stufenliste:** Schuppenkleid (15/35) und Einkommen (gratis/2/6/20/60/200) zählen weiter nur den Wert
  der gewählten Stufe; bei Sonderfertigkeiten werden die Stufen seit 2026-10-11 aufsummiert. Prüfen, ob das Wiki bei den
  beiden Vorteilen Gesamtwerte oder Werte je Stufe meint.
- Regionale Währungen umrechnen (Wiki: Währungen); Münzgewicht zur Traglast (die Ausrüstung zeigt das Geld schon
  in Dukaten, Silbertalern, Hellern und Kreuzern).
- Inhalt von Behältern (was steckt im Rucksack), Lagerorte (Pferd, Herberge) statt nur „dabei“.
- **PDF:** Teilen per Android-Share-Intent (jetzt nur Speichern); Druckvorschau in der App; Regeltexte auch für
  Sonderfertigkeiten und Vor-/Nachteile (für Zauber, Liturgien usw. gibt es einen eigenen Export). Vom Ulisses-Heldendokument fehlen: Spalte „Wirkung“ bei Zaubern (Wiki hat nur den langen Regeltext),
  Seitenangaben, Routineproben-Tabelle, Eigenschaftsmodifikationen, AP-Berechnungsseiten,
  Zonenrüstungen (Fokusregel), Bruchfaktor/Strukturpunkte von Waffen und Schilden, Spezialisierungen in „Anmerkung“.
- **Stile:** Umschalten nur im Hauptmenü (nicht im Editor); keine Option „wie das System“ (hell/dunkel automatisch);
  das PDF bleibt unabhängig vom Stil.
- Charakterbogen in der App folgt noch der alten Gliederung (PDF hat den Aufbau des Heldendokuments).
- **Quellenauswahl:** Bücher sind nur nach Grundregeln/Weitere/Ü18 gruppiert, nicht nach Art (Regelwerk, Spielhilfe,
  Abenteuer); das Wiki ordnet die Publikationen nicht ein. Weitere App-Einstellungen gibt es noch nicht (nur
  Standardquellen).
