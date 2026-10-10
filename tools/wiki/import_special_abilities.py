"""Erzeugt data/special-abilities/*.json, data/languages/*.json und data/scripts/*.json aus dem Regel-Wiki
(profane, magische und karmale Sonderfertigkeiten; Seitenlisten aus crawl_special_abilities.py und crawl_magic_sf.py).

Kosten und Voraussetzungen nutzen die Parser aus import_advantages und ergänzen SF-Formen. Voraussetzungen, die
nur aus einem Namen bestehen („Finte I“, „Tierkunde 8“, „Dolche 10“), werden im zweiten Durchgang gegen die
bekannten Sonderfertigkeiten, Talente und Kampftechniken aufgelöst.
"""
import html
import json
import os
import re

import books
import crawl_lists
import import_advantages as adv
import pagetext
import skillnames
import wiki

PAGES = os.path.join(os.path.dirname(__file__), ".cache", "_sf_profane_pages.json")
MAGIC_PAGES = os.path.join(os.path.dirname(__file__), ".cache", "_sf_magic_pages.json")
ATTRIBUTE_IDS = {"Mut": "MU", "Klugheit": "KL", "Intuition": "IN", "Charisma": "CH", "Fingerfertigkeit": "FF",
                 "Gewandtheit": "GE", "Konstitution": "KO", "Körperkraft": "KK"}
# Feldnamen der Eintragsseiten (magisch/karmal) → Datenfeld; alles Übrige landet in detailsText.
FIELD_KEYS = {"Regel": "ruleText", "Regeln": "ruleText", "Wirkung": "ruleText",
              "Voraussetzung": "prerequisitesText", "Voraussetzungen": "prerequisitesText",
              "AP-Wert": "apValueText", "Anmerkung": "note", "Errata": "errata",
              "Erweiterte Zaubersonderfertigkeiten": "extendedAbilitiesText",
              "Erweiterte Liturgiesonderfertigkeiten": "extendedAbilitiesText",
              "Erweiterte Liturgiestilsonderfertigkeiten": "extendedAbilitiesText",
              "passender Zauberstil": "fittingStylesText", "Passender Zauberstil": "fittingStylesText",
              "passender Liturgiestil": "fittingStylesText", "Passender Liturgiestil": "fittingStylesText",
              "Leiteigenschaft": None, "Wohlgefällige Talente": None, "Publikation(en)": None, "Publikation": None,
              "Publikationen": None}
LABELS = ["➤ Passender Kampfstil", "➤ Passender Talentstil", "Passender Kampfstil", "Passender Talentstil",
          "Beispiel", "Regel", "Regeln", "Voraussetzungen", "Voraussetzung", "AP-Wert", "Kampftechniken", "Kampftechnik",
          "Erschwernis", "Anmerkung", "Errata", "Erweiterte Kampfsonderfertigkeiten",
          "Erweiterte Talentsonderfertigkeiten", "Publikation", "Publikation(en)", "Publikationen"]
FACTOR = {2: "A", 4: "B", 6: "C", 8: "D"}
ROMAN = {"I": 1, "II": 2, "III": 3, "IV": 4, "V": 5}


def sf_cost(text, levels):
    """Erst der ganze Text, dann nur die erste Zeile (Folgezeilen sind oft Hinweise, Tabellen, Beispiele)."""
    text = text.replace("Abenteuerpunke", "Abenteuerpunkte").replace("Abenteuerpunkte*", "Abenteuerpunkte")
    cost = _sf_cost(text, levels)
    if cost["type"] == "manual" and "\n" in text:
        cost = _sf_cost(text.split("\n")[0], levels)
    return cost


# „5 Abenteuerpunkte pro Stufe (Stufe I: 5 AP, Stufe II: 5 AP, Stufe III: 5 AP usw.)“: beliebig viele Stufen
OPEN_LEVELS = re.compile(r"(\d+) Abenteuerpunkte?(?: pro Stufe)? \(Stufe:? ?I:? \d+ AP, .*usw\.\)")
# Technische Obergrenze für Stufen ohne Ende („usw.“); openLevels kennzeichnet sie für die Anzeige.
OPEN_LEVEL_MAX = 99


def _sf_cost(text, levels):
    t = re.sub(r"\s+", " ", text.replace("\n", " ")).strip()
    t = re.sub(r"\s*\*+$", "", t).rstrip(".")  # „10 Abenteuerpunkte *“, „8 Abenteuerpunkte.“
    t = re.sub(r"\bAbenteuerpunkt\b", "Abenteuerpunkte", t)
    ap = lambda v: int(v)
    if m := OPEN_LEVELS.fullmatch(t):
        return {"type": "perLevel", "ap": ap(m.group(1)), "openLevels": True}
    if m := re.fullmatch(r"(\d+) (?:Abenteuerpunkte|AP)(?:;.*)?", t):
        return {"type": "fixed", "ap": ap(m.group(1))}
    if m := re.fullmatch(r"(\d+) (?:Abenteuerpunkte|AP) pro Stufe", t):
        return {"type": "perLevel", "ap": ap(m.group(1))}
    # „10 Abenteuerpunkte für Stufe I; Stufe II: 15 Abenteuerpunkte“, „15 … für Stufe I, 30 … für Stufe II“
    if m := re.fullmatch(r"(\d+) Abenteuerpunkte für Stufe I[;,] (?:Stufe II: )?(\d+) Abenteuerpunkte(?: für Stufe II)?", t):
        return {"type": "levels", "levels": [ap(m.group(1)), ap(m.group(2))]}
    if m := re.fullmatch(r"Stufe I/II(?:/III)?: (\d+)/(\d+)(?:/(\d+))? Abenteuerpunkte?(?: pro Stufe)?", t):
        return {"type": "levels", "levels": [ap(x) for x in m.groups() if x]}
    if m := re.fullmatch(r"(\d+)/(\d+)(?:/(\d+))?(?:/(\d+))? Abenteuerpunkte?(?: pro Stufe)?", t):
        return {"type": "levels", "levels": [ap(x) for x in m.groups() if x]}
    if m := re.search(r"A-/B-/C-/D-(Talent|Fertigkeit|Schrift):? (\d+)/(\d+)/(\d+)/(\d+)", t):
        target = {"Schrift": "script"}.get(m.group(1), "skill")
        cost = {"type": "bySkillFactor", "target": target, "byFactor": dict(zip("ABCD", map(int, m.groups()[1:])))}
        if "zweite Spezialisierung" in t:
            cost["escalating"] = True  # n-te Spezialisierung desselben Talents kostet das n-Fache
        return cost
    # Zauber/Liturgien sind noch nicht erfasst (Meilenstein 6): Steigerungsfaktor als Ausprägung wählen
    if m := re.fullmatch(r"A-/B-/C-/D-(Zauber|Liturgie): (\d+)/(\d+)/(\d+)/(\d+) Abenteuerpunkte", t):
        return {"type": "byOption", "options": [{"name": f"{f}-{m.group(1)}", "ap": int(v)}
                                                for f, v in zip("ABCD", m.groups()[1:])]}
    if m := re.fullmatch(r"A-/B-/C-(Talent): (\d+)/(\d+)/(\d+) Abenteuerpunkte", t):
        return {"type": "bySkillFactor", "target": "skill", "byFactor": dict(zip("ABC", map(int, m.groups()[1:])))}
    # „primitive/einfache/komplexe Golems: 5/10/15 Abenteuerpunkte“,
    # „10/15/20 Abenteuerpunkte für eine kleine/mittlere/große Tierverwandlung“
    if (m := re.fullmatch(r"([^:\d]+?):? (\d+(?:/\d+)+) Abenteuerpunkte", t)) and "/" in m.group(1):
        return by_option(m.group(1), m.group(2), whole=not m.group(1)[0].islower())
    if m := re.fullmatch(r"(\d+(?:/\d+)+) Abenteuerpunkte für (.+)", t):
        return by_option(m.group(2), m.group(1))
    if m := re.fullmatch(r"(\d+) Abenteuerpunkte? pro (?!Stufe)[\wäöüß ]+", t):
        return {"type": "fixed", "ap": ap(m.group(1))}
    pairs = re.findall(r"([A-ZÄÖÜ][^:;]*?): (\d+) Abenteuerpunkte?", t)
    if len(pairs) >= 2 and not t.startswith("Stufe"):
        return {"type": "byOption", "options": [{"name": n.strip(), "ap": ap(v)} for n, v in pairs]}
    c = adv.cost(t, 1)
    if c["type"] == "perLevel" and not levels:
        c = {"type": "fixed", "ap": c["ap"]}
    return c


def by_option(names_text, values_text, whole=False):
    """„niedere/mittlere/hohe Geister“ + „8/15/30“ → Ausprägungen; Namen: das Wort mit Schrägstrichen, ergänzt um
    den Rest des Textes („Bannschwerter der Kampftechniken Dolche/Schwerter/Zweihandschwerter“ → „Dolche“ …)."""
    values = [int(v) for v in values_text.split("/")]
    if whole:  # „Elementargeister/Dschinne/Elementare Meister“
        parts = [p.strip() for p in names_text.split("/")]
        if len(parts) == len(values):
            return {"type": "byOption", "options": [{"name": n, "ap": v} for n, v in zip(parts, values)]}
    words = names_text.split(" ")
    i = next(i for i, w in enumerate(words) if w.count("/") == len(values) - 1 or "/" in w)
    parts = words[i].split("/")
    if len(parts) != len(values):  # „niedere Dämonen/bis zu 5-gehörnte Dämonen/Dämonen ab 6 Hörnern“
        parts = [p.strip() for p in names_text.split("/")]
    if len(parts) != len(values):
        return {"type": "manual"}
    suffix = " ".join(words[i + 1:]) if len(words[i].split("/")) == len(values) else ""
    names = [(p + (" " + suffix if suffix and not suffix.startswith(("der ", "für ")) else "")).strip() for p in parts]
    return {"type": "byOption", "options": [{"name": n, "ap": v} for n, v in zip(names, values)]}


def table(page, index):
    h = wiki.fetch(page)
    t = re.findall(r"<table.*?</table>", h, flags=re.S)[index]
    rows = re.findall(r"<tr.*?</tr>", t, flags=re.S)
    cells = [[re.sub(r"\s+", " ", html.unescape(re.sub("<[^>]+>", "", c))).strip()
              for c in re.findall(r"<t[hd][^>]*>(.*?)</t[hd]>", r, flags=re.S)] for r in rows]
    return [c for c in cells[1:] if c and c[0]]


def languages_and_scripts():
    pubs = books.publications(pagetext.publication_text(pagetext.article_lines("Sprachen.html")))
    languages, scripts = [], []
    for region, (li, si) in {"Aventurien": (0, 1), "Uthuria": (2, 3), "Myranor": (4, 5)}.items():
        for name, dialects, script, note in table("Sprachen.html", li):
            entry = {"id": "language." + wiki.slug(name.rstrip("*")), "name": name.rstrip("*").strip(), "region": region,
                     "dialectsText": dialects, "scriptText": script}
            if note and note != "Anmerkung":
                entry["note"] = note
                if m := re.search(r"maximale Stufe von (I+)", note):
                    entry["maxLevel"] = len(m.group(1))
            if name.endswith("*"):
                entry["extinct"] = True
            entry["publications"] = pubs
            languages.append(entry)
        for name, cost, alphabet, assigned in table("Sprachen.html", si):
            if not cost.isdigit():
                continue
            entry = {"id": "script." + wiki.slug(name.rstrip("*")), "name": name.rstrip("*").strip(), "region": region,
                     "improvementCost": FACTOR[int(cost)], "alphabetText": alphabet, "languagesText": assigned}
            if name.endswith("*"):
                entry["extinct"] = True
            entry["publications"] = pubs
            scripts.append(entry)
    # gleiche Namen in mehreren Regionen eindeutig machen
    for items in (languages, scripts):
        seen = {}
        for e in items:
            if e["id"] in seen:
                e["id"] += "-" + wiki.slug(e["region"])
            seen[e["id"]] = e
    return languages, scripts


def structure(entry, f, name, levels, pre):
    """Gemeinsamer Teil für Artikel- und Feldseiten: Voraussetzungen und Kosten strukturieren."""
    if pre:
        entry["prerequisitesText"] = pre
        pre_clean = re.sub(r"(?:Je )?die Vorgängerstufe der Sonderfertigkeit(?: \(Ausnahme: Stufe I\))?\.?", "", pre)
        structured, unparsed = [], []
        # Abschnitte je Stufe: „Stufe I: GE 13, Stufe II: GE 15, Finte I; Stufe III: …“ – alles bis zur nächsten
        # Stufenangabe gilt ab dieser Stufe.
        parts = re.split(r"Stufe (I{1,3}|IV|V)\s*[:,]\s*", pre_clean)
        for i in range(0, len(parts), 2):
            segment = parts[i].strip(" ,;")
            level = ROMAN[parts[i - 1]] if i > 0 else None
            if not segment or segment.lower() == "keine":
                continue
            seg_structured, seg_unparsed = adv.prerequisites(segment, name)
            for p in seg_structured:
                if level:
                    p["fromLevel"] = level
            structured += seg_structured
            unparsed += [(level, u) for u in seg_unparsed]
        if structured:
            entry["prerequisites"] = structured
        if unparsed:
            entry["unparsedPrerequisites"] = unparsed
    entry["apValueText"] = f.get("AP-Wert", "")
    entry["cost"] = sf_cost(entry["apValueText"], levels)
    if entry["cost"].pop("openLevels", False) and not levels:
        entry["maxLevel"] = OPEN_LEVEL_MAX
        entry["openLevels"] = True
    if entry["cost"]["type"] == "perLevel" and not levels and "maxLevel" not in entry:
        entry["cost"] = {"type": "fixed", "ap": entry["cost"]["ap"]}
    if entry["cost"]["type"] == "levels" and not levels:
        entry["maxLevel"] = len(entry["cost"]["levels"])


def profane_entry(info):
    lines = pagetext.article_lines(info["page"])
    f = pagetext.labeled(lines, LABELS)
    raw = info["label"] if lines[0].startswith(("Regel", "AP-Wert")) else lines[0]
    name, levels = adv.split_name(raw)
    entry = {"id": "sf." + wiki.slug(name), "name": name, "category": " › ".join(info["category"])}
    if levels:
        entry["maxLevel"] = levels
    entry["ruleText"] = f.get("Regel") or f.get("Regeln") or ""
    structure(entry, f, name, levels, f.get("Voraussetzungen") or f.get("Voraussetzung"))
    extended = f.get("Erweiterte Kampfsonderfertigkeiten") or f.get("Erweiterte Talentsonderfertigkeiten")
    if extended:
        entry["extendedAbilitiesText"] = extended.replace("\n", ", ")
    styles = f.get("➤ Passender Kampfstil") or f.get("➤ Passender Talentstil") or f.get("Passender Kampfstil") \
        or f.get("Passender Talentstil")
    if styles:
        entry["fittingStylesText"] = styles.replace("\n", ", ")
    for key, label in [("combatTechniquesText", "Kampftechniken"), ("combatTechniquesText", "Kampftechnik"),
                       ("penalty", "Erschwernis"), ("note", "Anmerkung"), ("errata", "Errata")]:
        if f.get(label):
            entry[key] = f[label]
    if name == "Sprache":
        entry["optionTarget"] = "language"
    entry["publications"] = books.publications(pagetext.publication_text(lines))
    return entry


def half_primary_traditions():
    """Traditionen, die die Leiteigenschaft nur zur Hälfte in den AsP-Grundwert einrechnen (Stern in der Tabelle)."""
    rows = table("Heldenerschaffung/leiteigenschaften.html", 1)
    return {name.rstrip("*").strip() for name, _ in rows if name.endswith("*")}


def tradition_name(raw):
    """Magische Traditionen heißen in der Liste nur „Gildenmagier“, als SF und in Voraussetzungen „Tradition (…)“."""
    return raw if raw.startswith("Tradition") else f"Tradition ({raw})"


def permanent_cost(entry):
    """Traditionsartefakte: „Bindungskosten: 2 permanente AsP“ → permanentAstralCost (verringert den AsP-Höchstwert;
    zurückkaufen kostet 2 AP je Punkt, siehe Erfahrung)."""
    details = entry.get("detailsText", "")
    if m := re.search(r"Bindungskosten:\s*(\d+) permanente AsP", details):
        entry["permanentAstralCost"] = int(m.group(1))
    # „2/4/8 permanente AsP für Bannschwerter der Kampftechniken Dolche/Schwerter/Zweihandschwerter“: je Ausprägung
    elif (m := re.search(r"Bindungskosten:\s*(\d+(?:/\d+)+) permanente AsP", details)) and entry["cost"]["type"] == "byOption":
        values = [int(x) for x in m.group(1).split("/")]
        if len(values) == len(entry["cost"]["options"]):
            for option, value in zip(entry["cost"]["options"], values):
                option["permanentAstral"] = value


# Merkmals-/Aspektkenntnis: „10 Abenteuerpunkte für die erste, 20 … für die zweite, 40 … für die dritte“
KNOWLEDGE = {"Merkmalskenntnis": "property", "Aspektkenntnis": "aspect"}


def knowledge(entry):
    if entry["name"] not in KNOWLEDGE:
        return
    entry["optionTarget"] = KNOWLEDGE[entry["name"]]
    entry["cost"] = {"type": "byOccurrence", "levels": [int(x) for x in re.findall(r"(\d+) Abenteuerpunkte", entry["apValueText"])]}


def magic_pages():
    """Einträge aus crawl_magic_sf.py ohne Beifang: Der Crawl folgt in den Artikelseiten (Feenpakte) auch Links auf
    Regel- und Kreaturenseiten. Übernommen werden nur Einträge der Auswahllisten selbst (bei Traditionsartefakten eine
    Ebene tiefer) mit AP-Wert sowie Artikelseiten bis Tiefe 3."""
    seen, result = set(), []
    for info in json.load(open(MAGIC_PAGES, encoding="utf-8")):
        depth = len(info["category"])
        if info["kind"] == "fields":
            artefact = info["category"][0].startswith("Traditionsartefakt")
            if depth > (2 if artefact else 1):
                continue
            if "AP-Wert" not in wiki.fields(wiki.fetch(info["page"], **{info["param"]: info["value"]})):
                continue  # z. B. die Traditionsartefakte selbst (Magierstab, Kristallkugel …)
            key = (info["page"], info["value"])
        else:
            if depth > 3:
                continue
            key = (info["page"],)
        if key not in seen:
            seen.add(key)
            result.append(info)
    return result


def magic_entry(info, half):
    """Eintrag einer magischen/karmalen Feld- oder Artikelseite."""
    domain = {"magisch": "magical", "karmal": "karmal"}[info["domain"]]
    category = " › ".join(info["category"])
    if info["kind"] == "article":
        entry = profane_entry(info)
        entry["domain"] = domain
        return entry
    f = wiki.fields(wiki.fetch(info["page"], **{info["param"]: info["value"]}))
    is_tradition = "Traditionen" in category
    raw = tradition_name(info["value"]) if is_tradition else info["value"]
    name, levels = adv.split_name(raw)
    entry = {"id": "sf." + wiki.slug(name), "name": name, "category": category, "domain": domain}
    if levels:
        entry["maxLevel"] = levels
    details = []
    for label, value in f.items():
        key = FIELD_KEYS.get(label, "details")
        if key == "details":
            details.append(f"{label}: {value}")
        elif key and value:
            entry.setdefault(key, value if key not in ("extendedAbilitiesText", "fittingStylesText")
                             else re.sub(r",?\n", ", ", value).strip(" ,"))
    entry.setdefault("ruleText", "")
    if details:
        entry["detailsText"] = "\n".join(details)
    if is_tradition:
        primary = f.get("Leiteigenschaft", "").strip()
        tradition = {"kind": domain, "primaryAttribute": ATTRIBUTE_IDS.get(primary)}
        if primary and primary not in ATTRIBUTE_IDS and "keine" not in primary:
            print("  Leiteigenschaft unbekannt:", name, repr(primary))
        if info["value"] in half:
            tradition["halfPrimaryAttribute"] = True
        if f.get("Wohlgefällige Talente"):
            tradition["favouredSkillsText"] = f["Wohlgefällige Talente"]
        entry["tradition"] = tradition
    pre = entry.pop("prerequisitesText", None)
    structure(entry, f, name, levels, pre)
    permanent_cost(entry)
    knowledge(entry)
    entry["publications"] = books.publications(f.get("Publikation(en)") or f.get("Publikation") or f.get("Publikationen") or "")
    return entry


def main():
    pages = [p for p in json.load(open(PAGES, encoding="utf-8")) if "Hier" not in p["category"]]
    entries = [profane_entry(info) for info in pages]
    half = half_primary_traditions()
    for info in magic_pages():
        entries.append(magic_entry(info, half))

    # IDs eindeutig machen (gleicher Name in verschiedenen Kategorien)
    seen = {}
    for e in entries:
        if e["id"] in seen:
            e["id"] += "-" + wiki.slug(e["category"].split(" › ")[-1])
        seen[e["id"]] = e

    # Zweiter Durchgang: reine Namen als SF/Talent/Kampftechnik auflösen
    sf_names = {e["name"]: e for e in entries}
    for e in entries:
        rest = []
        for level, part in e.get("unparsedPrerequisites", []):
            resolved = resolve(part, sf_names, e["name"])
            if resolved is not None:
                for p in resolved:
                    if level and "fromLevel" not in p:
                        p["fromLevel"] = level
                e.setdefault("prerequisites", []).extend(resolved)
            else:
                rest.append(part)
        # Fertigkeitsspezialisierung: FW 6/12/18 für die 1./2./3. Spezialisierung prüft der Core direkt.
        if e["cost"].get("escalating"):
            rest = [r for r in rest if not re.match(r"Fertigkeitswert \d+", r)]
        # Verweise auf die eigene niedrigere Stufe („Finte I“ bei Finte II) sind durch die Stufe erfüllt.
        if "prerequisites" in e:
            e["prerequisites"] = [p for p in e["prerequisites"] if not (
                p["type"] == "requiresSpecialAbility" and re.fullmatch(re.escape(e["name"]) + r"\s+(I{1,3}|IV|V)", p["name"]))]
            if not e["prerequisites"]:
                del e["prerequisites"]
        if rest:
            e["unparsedPrerequisites"] = rest
        else:
            e.pop("unparsedPrerequisites", None)

    crawl_lists.write_by_book("special-abilities", entries)
    languages, scripts = languages_and_scripts()
    crawl_lists.write_by_book("languages", languages)
    crawl_lists.write_by_book("scripts", scripts)
    import collections
    print("  Kosten:", dict(collections.Counter(e["cost"]["type"] for e in entries)),
          "| mit unerkannten Voraussetzungen:", sum(1 for e in entries if "unparsedPrerequisites" in e))


STYLE = re.compile(r"passende[rn]? (?:Kampf|Talent|Zauber|Liturgie)(?:stil)?(?:-? oder (?:Kampf|Talent)stil)?"
                   r"(?:sonderfertigkeit|-SF)?", re.I)


SELF_LEVEL = re.compile(r"(?:bei höheren Stufen|jeweils|jede andere Stufe)\b.*\b(?:Stufe|Vorstufe)\b.*", re.I)
TRADITION_ALIASES = {"Hexe": "Hexen", "Druide": "Druiden", "Elf": "Elfen"}


def resolve(part, sf_names, own_name=""):
    """Einzelteil → Liste strukturierter Voraussetzungen oder None.

    „Finte II“, „Tierkunde 8“, „Dolche 10“, „SF Berittener Kampf“, „KKN“, „Stufe II: KK 15“ (gilt ab Stufe II),
    „MU 13 oder KL 13“ (eine genügt), „passende Kampfstilsonderfertigkeit“.
    """
    part = part.strip().rstrip(".")
    if STYLE.fullmatch(part):
        return [{"type": "fittingStyle"}]
    # „bei höheren Stufen die jeweils vorige Stufe der SF“: durch die Stufe selbst erfüllt
    if SELF_LEVEL.fullmatch(part) or part.lower() == "keine":
        return []
    # „Rudelbeschwörung I“ bei „Rudelbeschwörung (Elementare)“: Verweis auf die eigene niedrigere Stufe
    if (m := re.fullmatch(r"(?:SF\s+)?(.+?)\s+(I{1,3}|IV|V)", part)) and own_name.startswith(m.group(1) + " ("):
        return []
    # „3 Zauber des Merkmals auf 10“, „3 Liturgien und Zeremonien des Aspekts auf 10“: für die gewählte Kenntnis
    if m := re.fullmatch(r"(\d+) (?:Zauber|Liturgien und Zeremonien) des (?:Merkmals|Aspekts) auf (\d+)", part):
        return [{"type": "spellsWithOption", "count": int(m.group(1)), "min": int(m.group(2))}]
    if m := re.fullmatch(r"Leiteigenschaft(?: der Tradition)?\s+(\d+)", part):
        return [{"type": "primaryAttribute", "min": int(m.group(1))}]
    if part in ("Zauberer", "Geweihter"):
        return [{"type": "requiresAdvantage", "name": part}]
    if m := re.fullmatch(r"Stufe (I|II|III|IV|V):\s*(.+)", part):
        if m.group(2).strip().lower() == "keine":
            return []
        inner = resolve(m.group(2), sf_names)
        if inner is None:
            return None
        return [dict(p, fromLevel=ROMAN[m.group(1)]) for p in inner]
    options = [o.strip() for o in re.split(r"\s+oder\s+(?![^(]*\))", part)]
    if len(options) > 1:
        resolved = [single(o, sf_names) for o in options]
        return None if None in resolved else [{"type": "anyOf", "options": resolved}]
    items = [single(o.strip(), sf_names) for o in re.split(r"\s+und\s+(?![^(]*\))", part)]
    return None if None in items else items


def single(item, sf_names):
    item = re.sub(r"^(?:SF|Sonderfertigkeit(?:en)?|Sonderferigkeit)\s+", "", item.strip())
    if m := re.fullmatch(r"Tradition \(?([^()]+?)\)?(?: \(gegebenenfalls andere Tradition\))?", item):
        name = TRADITION_ALIASES.get(m.group(1), m.group(1))
        if f"Tradition ({name})" in sf_names:
            return {"type": "requiresSpecialAbility", "name": f"Tradition ({name})"}
    item = re.sub(r"^(MU|KL|IN|CH|FF|GE|KO|KK)(\d+)$", r"\1 \2", item)  # Tippfehler „KKN“
    if m := re.fullmatch(r"(MU|KL|IN|CH|FF|GE|KO|KK)\s+(\d+)", item):
        return {"type": "attribute", "attribute": m.group(1), "min": int(m.group(2))}
    m = re.fullmatch(r"(.+?)\s+(I{1,3}|IV|V)$", item)
    if item in sf_names or (m and m.group(1) in sf_names):
        return {"type": "requiresSpecialAbility", "name": item}
    m = re.fullmatch(r"(.+?)\s+(\d+)", item)
    if m and (sid := skillnames.skill_id(m.group(1))):
        return {"type": "skill", "name": sid, "min": int(m.group(2))}
    if m and (cid := skillnames.combat_id(m.group(1))):
        return {"type": "combatTechnique", "name": cid, "min": int(m.group(2))}
    return None


if __name__ == "__main__":
    main()
