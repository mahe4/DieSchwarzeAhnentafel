"""Erzeugt data/professions/*.json aus dem Regel-Wiki.

Strukturiert übernommen: AP-Wert, Eigenschafts-Voraussetzungen, Kampftechniken (inkl. „eine der folgenden …“),
Talente und Varianten („Klettern 7 statt 3“). Sonderfertigkeiten, Zauber, Liturgien und alles Übrige bleiben
vorerst Wiki-Text (strukturiert ab Meilenstein 4/6). Nicht zuordenbare Teile landen in "unparsed".
"""
import json
import os
import re

import books
import crawl_lists
import pagetext
import skillnames
import wiki

PAGES = os.path.join(os.path.dirname(__file__), ".cache", "_profession_pages.json")
SKILL_GROUPS = ["Körper", "Gesellschaft", "Natur", "Wissen", "Handwerk"]
TEXT_FIELDS = {
    "Voraussetzungen": "prerequisitesText",
    "Sonderfertigkeiten": "specialAbilitiesText",
    "Empfohlene Vorteile": "recommendedAdvantagesText",
    "Empfohlene Nachteile": "recommendedDisadvantagesText",
    "Ungeeignete Vorteile": "unsuitableAdvantagesText",
    "Ungeeignete Nachteile": "unsuitableDisadvantagesText",
    "Anmerkung": "note",
}
MAGIC_FIELDS = ["Zauber", "Zauber/Magische Handlungen", "Zauber/Rituale", "Zaubersprüche", "Animistenkräfte",
                "Zaubertänze", "Ein Zaubertrick aus folgender Liste", "Zaubertrick für Auswahl-Liste", "Herrschaftsritual"]
KARMA_FIELDS = ["Liturgien", "Glaube Macht und Heldenmut"]
STYLE_FIELDS = ["Passender Talentstil", "Passender Zauberstil", "Passender Liturgiestil", "Passender Kampfstil",
                "Passende Kampfstil-SF"]
LABELS = (["AP-Wert", "Kampftechniken", "Talente", "Varianten", "Variante", "Varianten (eine davon muss gewählt werden)",
           "Publikation", "Publikation(en)", "Publikationen", "Errata", "Hinweis", "Geeignete Nachteile"]
          + list(TEXT_FIELDS) + SKILL_GROUPS + MAGIC_FIELDS + KARMA_FIELDS + STYLE_FIELDS)
ATTR = r"\b(MU|KL|IN|CH|FF|GE|KO|KK)\s+(\d+)"


CHOICE_HEAD = (r"(?:eine|zwei)\s+(?:der\s+(?:nach)?folgenden\s+)?Kampftechnik(?:en)?"
               r"(?:\s+aus\s+(?:der\s+)?folgender\s+Liste)?\s*(?:auf\s+)?(\d+)(?:\s+statt\s+6)?")


def _options(text, unparsed, original):
    names = [o.strip() for o in re.split(r",|\boder\b|\bund\b", text) if o.strip()]
    ids = []
    for name in names:
        cid = skillnames.combat_id(name)
        if cid is None:
            # fehlendes Komma: „Hiebwaffen Kettenwaffen“
            parts = [skillnames.combat_id(p) for p in name.split()]
            if None in parts:
                unparsed.append("Kampftechnik-Auswahl: " + original)
                return None
            ids += parts
        else:
            ids.append(cid)
    return ids


def combat_techniques(text, unparsed):
    """Feste Werte und Auswahlen. Auswahl-Einträge: {"options": [...], "value": n}; mehrere Einträge mit
    denselben Optionen müssen verschiedene Techniken wählen. Der Grundwert ist das Maximum aus festen und gewählten Werten."""
    fixed, choices = [], []
    text = text.replace("\n", " ").strip().rstrip(".")
    if text in ("–", "-", "keine"):
        return fixed, choices
    # „eine der folgenden Kampftechniken 12, eine weitere 10: A, B, C“ / „…, die anderen 10: …“
    m = re.search(r",?\s*" + CHOICE_HEAD + r"\s*,\s*(eine weitere|die anderen?)\s*(?:auf\s+)?(\d+)\s*:\s*(.+)$", text)
    if m:
        options = _options(m.group(4), unparsed, m.group(0))
        if options:
            choices.append({"options": options, "value": int(m.group(1))})
            if m.group(2) == "eine weitere":
                choices.append({"options": options, "value": int(m.group(3))})
            else:
                # „die anderen 10“: alle übrigen Optionen erhalten den Wert (der gewählte Wert überwiegt als Maximum)
                fixed += [{"combatTechnique": o, "value": int(m.group(3))} for o in options]
        text = text[:m.start()]
    m = re.search(r",?\s*" + CHOICE_HEAD + r"\s*:\s*(.+)$", text)
    if m:
        options = _options(m.group(2), unparsed, m.group(0))
        if options:
            choices.append({"options": options, "value": int(m.group(1))})
        text = text[:m.start()]
    for part in [p.strip() for p in text.split(",") if p.strip()]:
        alt = re.match(r"(.+?)\s+(\d+)$", part)
        if not alt:
            unparsed.append("Kampftechniken: " + part)
            continue
        names = [n.strip() for n in re.split(r"\boder\b|/", alt.group(1))]
        ids = [skillnames.combat_id(n) for n in names]
        if None in ids:
            unparsed.append("Kampftechniken: " + part)
        elif len(ids) == 1:
            fixed.append({"combatTechnique": ids[0], "value": int(alt.group(2))})
        else:
            choices.append({"options": ids, "value": int(alt.group(2))})
    return fixed, choices


def skills(f, unparsed):
    """Talente aus den Gruppenfeldern. Steht unter „Talente:“ selbst etwas (Wiki-Fehler, z. B. Kampftechniken beim
    Fleischer), wird es mit ausgewertet; Kampftechniken darin gibt misplaced_combat() zurück."""
    result = []
    loose = f.get("Talente", "").replace("\n", ", ").strip()
    if loose:
        result += [{"skill": v["id"], "value": v["value"]}
                   for v in skillnames.value_list(loose, skillnames.skill_id, [])]
    for group in SKILL_GROUPS:
        text = f.get(group, "").replace("\n", ", ")
        if text and text.lower() != "keine" and text != "–":
            result += [{"skill": v["id"], "value": v["value"]}
                       for v in skillnames.value_list(text, skillnames.skill_id, unparsed)]
    return result


def variant(line, unparsed):
    m = re.match(r"#?\s*(.+?)\s*\((\d+)\s*AP\)\s*:?\s*(.*)$", line)
    if not m:
        return None
    name, ap, body = m.group(1).strip(), int(m.group(2)), m.group(3)
    entry = {"id": wiki.slug(name), "name": name, "wikiAp": ap, "skills": [], "combatTechniques": []}
    rest = []
    for part in [p.strip() for p in re.split(r"[;,](?![^(]*\))", body) if p.strip()]:
        c = re.match(r"(.+?)\s+(\d+)\s+statt\s+(\d+)$", part)
        if c:
            sid, cid = skillnames.skill_id(c.group(1)), skillnames.combat_id(c.group(1))
            if sid:
                entry["skills"].append({"skill": sid, "value": int(c.group(2))})
                continue
            if cid:
                entry["combatTechniques"].append({"combatTechnique": cid, "value": int(c.group(2))})
                continue
        rest.append(part)
    if rest:
        entry["text"] = ", ".join(rest)
    return entry


LEVEL_IDS = {"unerfahren", "durchschnittlich", "erfahren", "kompetent", "meisterlich", "brillant", "legendär"}
TRIPLE = re.compile(r"(–\*?|-|\d+)\s*/\s*(–\*?|-|\d+)\s*/\s*(–\*?|-|\d+)")


def level_columns(name, ap_text):
    """„Elementarist (erfahren/kompetent/meisterlich)“ bzw. „AP-Wert: 287/421/634“ → Erfahrungsgrade der Spalten."""
    m = re.search(r"\(([a-zäöü]+(?:/[a-zäöü]+)+)\)", name)
    if m and set(m.group(1).split("/")) <= LEVEL_IDS:
        return m.group(1).split("/")
    if TRIPLE.search(ap_text or ""):
        return ["erfahren", "kompetent", "meisterlich"]
    return None


def pick_level(text, index):
    """Ersetzt „12/13/15“ durch den Wert der Spalte; Einträge mit „–“ in dieser Spalte entfallen."""
    text = TRIPLE.sub(lambda m: m.group(index + 1), text)
    text = re.sub(r"[^,;:\n|]*?\s+[–-]\*?(?=\s*(?:,|;|\n|$))", "", text)
    return re.sub(r",\s*,", ",", text).strip(" ,")


def ap_value(f, lines):
    """„AP-Wert: 387 Abenteuerpunkte“, vereinzelt ohne Doppelpunkt.

    Mehrere Werte („117, 121, 126 oder 129 … je nach gewählter Variante“) → None: Das Paket hat ohne Variante keinen
    eigenen AP-Wert, die Werte stehen bei den Varianten.
    """
    text = f.get("AP-Wert") or next(l for l in lines if l.startswith("AP-Wert"))
    numbers = re.findall(r"\d+", text)
    return int(numbers[0]) if len(numbers) == 1 else None


def names(category):
    """Name → Eintrag (id, maxLevel, cost) einer Datenkategorie."""
    import glob
    folder = os.path.join(books.ROOT, "src", "DSACharacterCreator.Content", "data", category)
    return {e["name"]: e for f in sorted(glob.glob(os.path.join(folder, "*.json"))) for e in json.load(open(f, encoding="utf-8"))}


def special_ability_names():
    """Name → ID aller Sonderfertigkeiten (profan, magisch, karmal)."""
    return {n: e["id"] for n, e in names("special-abilities").items()}


SPELL_HEADERS = r"Zauber/Magische Handlungen|Zauber/Rituale|Zaubersprüche|Zauber|Rituale|Liturgien|Animistenkräfte|Zaubertänze|" \
                r"Herrschaftsritual|Zaubertrick für Auswahl-Liste"
CHOICE = "§AUSWAHL§"


def spell_catalog():
    """Name → Einträge (mehrere Arten können gleich heißen, z. B. Zauber und Ritual)."""
    catalog = {}
    for e in names_list("spells"):
        catalog.setdefault(e["name"].lower(), []).append(e)
    return catalog


def names_list(category):
    import glob
    folder = os.path.join(books.ROOT, "src", "DSACharacterCreator.Content", "data", category)
    return [e for f in sorted(glob.glob(os.path.join(folder, "*.json"))) for e in json.load(open(f, encoding="utf-8"))]


def spells(text, catalog, unparsed):
    """Zauber-/Liturgietext einer Profession → (Einträge {id, value?}, Auswahlen {count, value?, options}).

    „Zauber: ein Zaubertrick aus folgender Liste: Feuerfinger, Flammenhaar; Ignifaxius 6, Visibili (Elfen) 4“,
    „Liturgien: Die Zwölf Segnungen (außer Feuersegen, …), Bann der Dunkelheit 4“, „Archofaxius oder Ignifaxius 7“,
    „eine der folgenden Liturgien auf 5: …“. Zusätze in Klammern (Fremdtradition, gewählte Eigenschaft) gehören nicht
    zum Namen; Kurznamen („Balsam“ für „Balsam Salabunde“) werden über eindeutige Anfänge aufgelöst. Werte je
    Erfahrungsgrad („10/11/14“) und Platzhalter („(Element-)faxius“) bleiben in unparsed.
    """
    entries, choices = [], []
    if not text:
        return entries, choices
    t = re.sub(rf"(^|\n)\s*(?:{SPELL_HEADERS})\s*:", r"\1;", text)
    t = re.sub(r"(\d)\s+(?=[A-ZÄÖÜ])", r"\1, ", t)          # „Objektsegen 5 Schlangenruf 3“
    t = re.sub(r"([a-zäöüß])(\d)", r"\1 \2", t)               # „Entzifferung5“
    t = re.sub(r"(einen|ein|zwei|drei|Einen|Ein|Zwei|Drei) Zaubertricks? aus ?folgender Liste:?",
               lambda m: f";{CHOICE}{WORDS[m.group(1).lower()]};", t)
    t = re.sub(r"(?:eine|ein) der folgenden (?:Liturgien|Zauber|Zeremonien|Rituale) auf (\d+):?", rf";{CHOICE}1/\1;", t)
    items = [i.strip().rstrip(".") for i in re.split(r"[;\n]|,(?![^(]*\))", t)]
    choice = None
    for item in items:
        item = re.sub(r"^(?:Zauberrunen|Animistenkräfte|Zaubertänze|Elfenlieder|Zaubermelodien?)\s*:\s*", "", item)
        if not item or re.search(r"Glaube Macht und Heldenmut|Seite \d+|\[zusätzlicher Vorschlag\]", item):
            continue
        if item.startswith(CHOICE):
            count, _, value = item[len(CHOICE):].partition("/")
            choice = {"count": int(count), "options": []}
            if value:
                choice["value"] = int(value)
            choices.append(choice)
            continue
        if m := re.fullmatch(r"(?:Die|die|alle)?\s*[Zz]wölf Segnungen(?:\s*\(außer (.+)\))?|Segnungen: alle 12", item):
            excluded = {x.strip().lower() for x in re.split(r",|\s+und\s+", m.group(1) or "") if x.strip()}
            entries += [{"id": e["id"]} for es in catalog.values() for e in es
                        if e["group"] == "blessing" and e["name"].lower() not in excluded]
            choice = None
            continue
        if m := re.fullmatch(r"(\w+) Segnungen", item):  # „Sechs Segnungen“: frei wählbar
            choices.append({"count": WORDS.get(m.group(1).lower(), 1), "options":
                            [e["id"] for es in catalog.values() for e in es if e["group"] == "blessing"]})
            choice = None
            continue
        m = re.fullmatch(r"(.+?)\s+(\d+)", item)
        name, value = (m.group(1), int(m.group(2))) if m else (item, None)
        # „(Element-)faxius 10“: Ignifaxius, Aquafaxius … je nach gewähltem Element
        if (em := re.fullmatch(r"\(Element-\)(\w+)", name)) and value is not None:
            options = sorted(e["id"] for es in catalog.values() for e in es if e["group"] == "spell" and
                             e.get("property") == "Elementar" and e["name"].lower().endswith(em.group(1).lower()))
            if options:
                choices.append({"count": 1, "value": value, "options": options})
                choice = None
                continue
        alternatives = re.split(r"\s+oder\s+", name)
        found_all = [find_spell(n, value, catalog) for n in alternatives]
        if None in found_all:
            if choice is not None and choice.get("value") and value is None and (single := find_spell(name, 1, catalog)):
                choice["options"].append(single["id"])  # Liste unter „eine der folgenden Liturgien auf 5“
                continue
            unparsed.append(item)
            continue
        if len(found_all) > 1:  # „Archofaxius oder Ignifaxius 7“
            choices.append({"count": 1, "value": value, "options": [f["id"] for f in found_all]})
            choice = None
            continue
        spell = found_all[0]
        if choice is not None and value is None and (spell["group"] == "cantrip" or choice.get("value")):
            choice["options"].append(spell["id"])
            continue
        choice = None
        entries.append({"id": spell["id"], "value": value} if value is not None else {"id": spell["id"]})
    choices = [c for c in choices if c["options"]]
    return entries, choices


WORDS = {"ein": 1, "eine": 1, "einen": 1, "zwei": 2, "drei": 3, "vier": 4, "fünf": 5, "sechs": 6}
SPELL_TYPOS = {"invovatio minima": "invocatio minima", "kriesgbemalung": "kriegsbemalung", "caldogrigo": "caldofrigo",
               "geisteressenz": "geistessenz", "siegel des grabs": "siegel des grabes", "tabuzone": "goblin-tabuzone"}


def find_spell(name, value, catalog):
    """Name → Eintrag; ohne Wert bevorzugt Zaubertricks/Segnungen, mit Wert die übrigen. Kurznamen über eindeutige
    Anfänge („Balsam“ → „Balsam Salabunde“)."""
    base = re.sub(r"\s*\([^)]*\)$", "", name).strip().lower()
    base = SPELL_TYPOS.get(base, base)
    found = catalog.get(name.lower()) or catalog.get(base) or []
    if not found:
        prefixed = [k for k in catalog if k.startswith(base + " ")]
        if len(prefixed) == 1:
            found = catalog[prefixed[0]]
    if value is None:
        found = [e for e in found if e["group"] in ("cantrip", "blessing")] or found
    else:
        found = [e for e in found if e["group"] not in ("cantrip", "blessing")]
    return found[0] if found else None


# Schreibweisen der Professionsseiten → Name des Eintrags
TRADITION_ALIASES = {"Druide": "Druiden", "Elf": "Elfen", "Hexe": "Hexen", "Geode": "Geoden", "Scharlatan": "Scharlatane",
                     "Schelm": "Schelme", "Zauberbarde": "Zauberbarden", "Zaubertänzer": "Zaubertänzer"}


def prerequisite_entries(text, catalogs, unparsed):
    """Vor-/Nachteile und Sonderfertigkeiten aus den Voraussetzungen einer Profession („Vorteil Zauberer (25 AP),
    Sonderfertigkeit Tradition (Gildenmagier) (155 AP), Nachteil Prinzipientreue II (Ehrenhaftigkeit) (–10 AP)“).

    Sie gehören nicht zum Paket (der AP-Wert der Profession enthält sie nicht), werden aber als Vorauswahl übernommen.
    Eigenschaftswerte („KK 13 (für die SF Wuchtschlag I)“) stehen in attributeRequirements.
    """
    import import_advantages as adv
    text = re.sub(r"\s*\((?:[-–]?\s*\d+(?:\s*[-–/]\s*\d+)?\s*AP)\)", "", text or "")  # „(25 AP)“, „(–10 AP)“
    text = re.sub(r"\s*/\s*zusätzlich.*", "", text)
    structured, rest = adv.prerequisites(text)
    kinds = {"requiresAdvantage": "advantage", "requiresDisadvantage": "disadvantage",
             "requiresSpecialAbility": "specialAbility"}
    items = [(kinds[p["type"]], p["name"]) for p in structured if p["type"] in kinds]
    # Ohne „Vorteil“/„Nachteil“ davor („Prinzipientreue I (Rahjakirche)“): in allen Katalogen suchen
    items += [(None, r) for r in rest if not re.match(r"(?:Kultur|Spezies|Geschlecht|MU|KL|IN|CH|FF|GE|KO|KK)\b", r)]
    result = []
    for kind, name in items:
        name = re.sub(r"\s*\(für (?:die )?(?:SF )?[^)]*\)$", "", name).strip()
        if m := re.fullmatch(r"Tradition \((.+)\)", name):
            name = f"Tradition ({TRADITION_ALIASES.get(m.group(1), m.group(1))})"
        for k in [kind] if kind else ["disadvantage", "advantage", "specialAbility"]:
            if entry := resolve_entry(name, catalogs[k]):
                entry["kind"] = k
                result.append(entry)
                break
        else:
            if kind:
                unparsed.append(f"Voraussetzung {name}")
    return result


def resolve_entry(name, catalog):
    """„Prinzipientreue II (Ehrenhaftigkeit, Loyalität)“ → {id, level: 2, specification}; „Adel“ → {id}."""
    spec = level = None
    if name not in catalog and (m := re.fullmatch(r"(.+?)\s*\(([^()]+)\)", name)):
        name, spec = m.group(1).strip(), m.group(2).strip()
    if name not in catalog and (m := re.fullmatch(r"(.+?)\s+(I{1,3}|IV|V)", name)):
        name, level = m.group(1), ROMAN[m.group(2)]
    if name not in catalog:
        return None
    entry = {"id": catalog[name]["id"]}
    if level:
        entry["level"] = level
    if spec:
        entry["specification"] = spec
    return entry


ROMAN = {"I": 1, "II": 2, "III": 3, "IV": 4, "V": 5}


def sf_spelling(p, sf_ids):
    """Schreibvarianten der Professionsseiten („Fedorino- Stil“, „Neersand Stil“, „abu‘l“, „Finte 1“, „Scholar Akademie
    der …“) auf den SF-Namen abbilden; sonst unverändert."""
    import difflib
    q = re.sub(r"^Sonderfertigkeit\s+", "", p).replace("‘", "’").replace("- ", "-")
    q = re.sub(r"(\w) Stil$", r"\1-Stil", q)
    q = re.sub(r"\s+1$", " I", q)
    if q in sf_ids or re.sub(r"\s+(I{1,3}|IV|V)$", "", q) in sf_ids:
        return q
    close = difflib.get_close_matches(q, list(sf_ids), 1, 0.93)
    return close[0] if close else p


def special_abilities(text, sf_ids):
    """„Sprachen und Schriften für insgesamt 8 Abenteuerpunkte, Fertigkeitsspezialisierung Gassenwissen, Finte I“
    → (Einträge, Sprach-/Schrift-Budget, nicht zugeordnete Teile)."""
    entries, budget, unparsed = [], None, []
    for part in re.split(r",(?![^(]*\))", (text or "").replace("\n", ", ")):
        p = sf_spelling(part.strip().rstrip("."), sf_ids)
        if not p or p.lower() == "keine":
            continue
        if m := re.match(r"Sprachen(?: und Schriften)? für insgesamt (\d+)", p):
            budget = int(m.group(1))
            continue
        if m := re.fullmatch(r"Fertigkeitsspezialisierung\s+(.+?)(?:\s*\((.+)\))?", p):
            if sid := skillnames.skill_id(m.group(1)):
                entry = {"id": "sf.fertigkeitsspezialisierung-talente", "option": sid}
                if m.group(2):
                    entry["specification"] = m.group(2).strip()
                entries.append(entry)
                continue
        m = re.fullmatch(r"(.+?)\s+(I{1,3}|IV|V)", p)
        if p in sf_ids:
            entries.append({"id": sf_ids[p]})
        elif m and m.group(1) in sf_ids:
            entries.append({"id": sf_ids[m.group(1)], "level": ROMAN[m.group(2)]})
        elif (m := re.fullmatch(r"(.+?)\s*\((.+)\)", p)) and m.group(1) in sf_ids:
            entries.append({"id": sf_ids[m.group(1)], "specification": m.group(2).strip()})
        else:
            unparsed.append(p)
    return entries, budget, unparsed


def category_labels():
    """Seite → Anzeigename der Kategorieseiten (Weltliche › Kämpfer, Zauberer › Gildenmagier …)."""
    labels = {"Pro_Weltlich.html": "Weltliche Professionen", "Pro_Zauberer.html": "Zauberer",
              "Pro_Geweihte.html": "Geweihte"}
    pages = json.load(open(PAGES, encoding="utf-8"))
    for category in {c for info in pages for c in info["category"]}:
        for url, label in pagetext.article_links(category):
            labels.setdefault(url, label)
    return labels


def main():
    pages = json.load(open(PAGES, encoding="utf-8"))
    labels = category_labels()
    sf_ids = special_ability_names()
    catalogs = {"advantage": names("advantages"), "disadvantage": names("disadvantages"),
                "specialAbility": names("special-abilities")}
    spell_names = spell_catalog()
    entries, stats = [], {"unparsed": 0}
    for info in pages:
        lines = pagetext.article_lines(info["page"])
        f = pagetext.labeled(lines, LABELS)
        unparsed = []
        # Einige Seiten haben keine Überschrift; dann den Linktext der Übersicht verwenden.
        name = info["label"] if lines[0].startswith("AP-Wert") or lines[0].startswith("(") else lines[0]
        columns = level_columns(lines[0] + " " + name, f.get("AP-Wert"))
        name = re.sub(r"\s*\((?:[a-zäöü]+/)+[a-zäöü]+\)", "", name).strip()
        entry = {"id": "profession." + wiki.slug(name), "name": name, "page": info["page"],
                 "group": " › ".join(labels.get(p, p) for p in info["category"]),
                 # AP-Wert laut Wiki – nur zum Vergleich und für noch nicht strukturierte Teile (SF, Zauber, Sprachen).
                 "wikiAp": ap_value(f, lines)}
        for label, key in TEXT_FIELDS.items():
            if f.get(label) and f[label].strip().lower() != "keine":
                entry[key] = f[label]
        sf_text = f.get("Sonderfertigkeiten", "")
        if columns:
            sf_text = re.split(r"\s*/\s*", sf_text)[0]  # Spalte „erfahren“; weitere Spalten als Text
        sf_entries, budget, sf_unparsed = special_abilities(sf_text, sf_ids)
        if sf_entries:
            entry["specialAbilities"] = sf_entries
        if budget:
            entry["languageAp"] = budget
        if sf_unparsed:
            entry["unparsedSpecialAbilities"] = sf_unparsed
        attrs = {}
        for a, v in re.findall(ATTR, f.get("Voraussetzungen", "")):
            attrs[a] = max(attrs.get(a, 0), int(v))
        if attrs:
            entry["attributeRequirements"] = attrs
        if required := prerequisite_entries(f.get("Voraussetzungen"), catalogs, unparsed):
            entry["prerequisiteEntries"] = required
        fixed, choices = combat_techniques(f.get("Kampftechniken", ""), unparsed)
        # Kampftechniken, die im Wiki unter „Talente:“ stehen
        for v in skillnames.value_list(f.get("Talente", "").replace("\n", ", "), skillnames.combat_id, []):
            if not any(x["combatTechnique"] == v["id"] for x in fixed):
                fixed.append({"combatTechnique": v["id"], "value": v["value"]})
        entry["combatTechniques"] = fixed
        if choices:
            entry["combatTechniqueChoices"] = choices
        entry["skills"] = skills(f, unparsed)
        magic = "\n".join(f"{l}: {f[l]}" for l in MAGIC_FIELDS if f.get(l))
        karma = "\n".join(f"{l}: {f[l]}" for l in KARMA_FIELDS if f.get(l))
        styles = "\n".join(f"{l}: {f[l]}" for l in STYLE_FIELDS if f.get(l))
        if magic:
            entry["spellsText"] = magic
        if karma:
            entry["liturgiesText"] = karma
        spell_unparsed = []
        spell_entries, spell_choices = spells("\n".join(x for x in (magic, karma) if x), spell_names, spell_unparsed)
        if spell_choices:
            entry["spellChoices"] = spell_choices
        # Unter „Sonderfertigkeiten“ stehen manchmal Flüche-Budgets und magische Handlungen („Freundschaftslied 4“)
        for item in list(entry.get("unparsedSpecialAbilities", [])):
            if m := re.fullmatch(r"Flüche für insgesamt (\d+)(?: Abenteuerpunkte| AP)?", item):
                entry["curseAp"] = int(m.group(1))
                entry["unparsedSpecialAbilities"].remove(item)
                continue
            found, choices = spells(item, spell_names, [])
            if found and not choices and all("value" in f for f in found):
                spell_entries += found
                entry["unparsedSpecialAbilities"].remove(item)
        if not entry.get("unparsedSpecialAbilities"):
            entry.pop("unparsedSpecialAbilities", None)
        if spell_entries:
            entry["spells"] = spell_entries
        if spell_unparsed:
            entry["unparsedSpells"] = spell_unparsed
        if styles:
            entry["stylesText"] = styles
        variants_text = f.get("Varianten") or f.get("Variante") or f.get("Varianten (eine davon muss gewählt werden)")
        if entry["wikiAp"] is None:
            del entry["wikiAp"]
        if variants_text:
            variants = []
            for line in variants_text.split("\n"):
                v = variant(line, unparsed) if line.strip() else None
                if v:
                    variants.append(v)
                elif line.strip() and variants:
                    variants[-1]["text"] = (variants[-1].get("text", "") + " " + line.strip()).strip()
            if variants:
                entry["variants"] = variants
                entry["variantRequired"] = ("eine davon muss gewählt werden" in "\n".join(lines)
                                            or "eine Variante muss gewählt werden" in "\n".join(lines)
                                            or "wikiAp" not in entry)
        if columns:
            # Werte je Erfahrungsgrad: jede Spalte einzeln auswerten; Voraussetzungen „/ zusätzlich …“ kumulieren
            entry.pop("unparsed", None)
            unparsed = []
            aps = [int(x) for x in re.findall(r"\d+", f.get("AP-Wert", ""))]
            segments = re.split(r"\s*/\s*zusätzlich\s+", f.get("Voraussetzungen", ""))
            levels = []
            for i, level in enumerate(columns):
                lf = {k: pick_level(v, i) for k, v in f.items()}
                fixed, choices = combat_techniques(lf.get("Kampftechniken", ""), unparsed)
                attrs = {}
                for a, v in re.findall(ATTR, " ".join(segments[:i + 1])):
                    attrs[a] = max(attrs.get(a, 0), int(v))
                item = {"level": level, "wikiAp": aps[i] if i < len(aps) else None,
                        "skills": skills(lf, unparsed), "combatTechniques": fixed}
                if choices:
                    item["combatTechniqueChoices"] = choices
                if attrs:
                    item["attributeRequirements"] = attrs
                # Zauber/Liturgien je Spalte („Manifesto 10/11/12“, „Geisterbeschwörung – / 8 / 12“)
                level_text = pick_level("\n".join(f"{l}: {f[l]}" for l in MAGIC_FIELDS + KARMA_FIELDS if f.get(l)), i)
                level_unparsed = []
                level_spells, level_choices = spells(level_text, spell_names, level_unparsed)
                item["spells"] = level_spells
                if level_choices:
                    item["spellChoices"] = level_choices
                if level_unparsed:
                    item["unparsedSpells"] = level_unparsed
                levels.append(item)
            entry["byExperienceLevel"] = levels
            first = levels[0]
            entry["skills"], entry["combatTechniques"] = first["skills"], first["combatTechniques"]
            entry.pop("combatTechniqueChoices", None)
            if "combatTechniqueChoices" in first:
                entry["combatTechniqueChoices"] = first["combatTechniqueChoices"]
            if "attributeRequirements" in first:
                entry["attributeRequirements"] = first["attributeRequirements"]
            if first.get("spells"):
                entry["spells"] = first["spells"]
                entry.pop("unparsedSpells", None)
                if "unparsedSpells" in first:
                    entry["unparsedSpells"] = first["unparsedSpells"]
        if unparsed:
            entry["unparsed"] = unparsed
            stats["unparsed"] += 1
        entry["publications"] = books.publications(pagetext.publication_text(lines))
        entries.append(entry)

    seen = {}
    for e in entries:
        if e["id"] in seen:
            e["id"] += "-" + wiki.slug(e["page"].removesuffix(".html").split("/")[-1])
        seen[e["id"]] = e
    for e in entries:
        e.pop("page")
    crawl_lists.write_by_book("professions", entries)
    print(f"{stats['unparsed']} von {len(entries)} Professionen mit nicht zugeordneten Teilen")


if __name__ == "__main__":
    main()
