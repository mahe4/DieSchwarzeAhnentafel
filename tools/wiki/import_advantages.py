"""Erzeugt data/advantages/*.json und data/disadvantages/*.json aus dem Regel-Wiki.

Strukturiert: Stufen (aus „I-III“ im Namen), Kosten (fest, pro Stufe, je Stufe, je Steigerungsfaktor der gewählten
Fertigkeit/Kampftechnik, je Ausprägung) und einfache Voraussetzungen. Nicht Erkennbares bleibt Wiki-Text
(cost.type = "manual" bzw. "unparsedPrerequisites").
"""
import re
from urllib.parse import unquote_plus

import books
import crawl_lists
import wiki

# Auswirkungen auf Basiswerte: Name → (Effekte, Satz aus dem Wiki-Regeltext, der sie belegt). Der Import bricht ab,
# wenn der Satz im Wiki nicht mehr vorkommt.
EFFECTS = {
    "Hohe Lebenskraft": ([("lifePoints", 1, True)], "LE-Grundwert steigt durch diesen Vorteil um 1 Punkt pro Stufe"),
    "Niedrige Lebenskraft": ([("lifePoints", -1, True)], "LE-Grundwert sinkt durch diesen Nachteil um 1 Punkt pro Stufe"),
    "Hohe Seelenkraft": ([("spirit", 1, False)], "SK-Grundwert des Helden ist um 1 besser"),
    "Niedrige Seelenkraft": ([("spirit", -1, False)], "SK-Grundwert des Helden ist um 1 schlechter"),
    "Hohe Zähigkeit": ([("toughness", 1, False)], "ZK-Grundwert des Helden ist um 1 besser"),
    "Niedrige Zähigkeit": ([("toughness", -1, False)], "ZK-Grundwert des Helden ist um 1 schlechter"),
    "Flink": ([("speed", 1, False)], "1 Punkt auf seinen Geschwindigkeit-Grundwert hinzu"),
    "Behäbig": ([("speed", -1, False)], "verliert ein Held 1 Punkt von seinem Geschwindigkeit-Grundwert"),
    "Glück": ([("fatePoints", 1, True)], "steigt hierdurch um 1 je Stufe"),
    "Pech": ([("fatePoints", -1, True)], "1 Schicksalspunkt weniger als üblich"),
    "Zauberer": ([("astralBase", 20, False)], "Astralenergie- Grundwert 20 AsP"),
    "Geweihter": ([("karmaBase", 20, False)], "Karmaenergie-Grundwert 20 KaP"),
    "Hohe Astralkraft": ([("astral", 1, True)], "AE-Grundwert steigt durch diesen Vorteil um 1 Punkt pro Stufe"),
    "Niedrige Astralkraft": ([("astral", -1, True)], "AE-Grundwert sinkt durch diesen Nachteil um 1 Punkt pro Stufe"),
    "Hohe Karmalkraft": ([("karma", 1, True)], "KE-Grundwert steigt durch diesen Vorteil um 1 Punkt pro Stufe"),
    "Niedrige Karmalkraft": ([("karma", -1, True)], "KE-Grundwert sinkt durch diesen Nachteil um 1 Punkt pro Stufe"),
}

ROMAN = {r: i + 1 for i, r in enumerate(["I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII", "XIII"])}
ATTR = r"(MU|KL|IN|CH|FF|GE|KO|KK)"


def list_names(page, param):
    h = wiki.fetch(page)
    return list(dict.fromkeys(unquote_plus(n) for n in re.findall(rf"href=[\"']?[^\"'>]*\?{param}=([^\"'>]+)", h)))


def split_name(raw):
    """„Adel I-III“ → („Adel“, 3); „Altersresistenz (*)“ → („Altersresistenz“, None)."""
    name = re.sub(r"\s*\(\*\)\s*$", "", raw).strip()
    m = re.match(r"(.+?)\s+I\s*[-–]\s*(II|III|IV|V|VI|VII|VIII|IX|X|XI|XII|XIII)$", name)
    if m:
        return m.group(1).strip(), ROMAN[m.group(2)]
    m = re.match(r"(.+?)\s+I/II(?:/(III))?$", name)  # „Rudelerschaffung I/II/III“
    if m:
        return m.group(1).strip(), 3 if m.group(2) else 2
    # „Rudelbeschwörung I-III (Elementare)“, „Rudelbeschwörung I/II/III (Dämonen)“
    m = re.match(r"(.+?)\s+I\s*(?:[-–]\s*|/II/)(II|III|IV|V)\s+(\(.+\))$", name)
    if m:
        return f"{m.group(1).strip()} {m.group(3)}", ROMAN[m.group(2)]
    return name, None


def number(text):
    return int(text.replace("–", "-").replace(" ", ""))


def cost(text, sign):
    """AP-Wert-Text → strukturierte Kosten. sign = -1 für Nachteile (AP-Werte immer negativ gespeichert)."""
    t = re.sub(r"\s+", " ", text.replace("\n", " ")).strip()
    ap = lambda v: sign * abs(number(v))
    if m := re.fullmatch(r"([-–]?\s*\d+)(?: Abenteuerpunkte?)?", t):
        return {"type": "fixed", "ap": ap(m.group(1))}
    # „-2 Abenteuerpunkte (pro Schlechter Angewohnheit)“, „-10 Abenteuerpunkte (Je nach Kampagne …)“: fester Wert je Eintrag
    if m := re.fullmatch(r"([-–]?\s*\d+) Abenteuerpunkte? \((?:pro Schlechter|Je nach Kampagne)[^)]*\)?.*", t):
        return {"type": "fixed", "ap": ap(m.group(1))}
    if m := re.fullmatch(r"([-–]?\s*\d+) Abenteuerpunkte? \(?[Pp]ro Stufe\)?", t):
        return {"type": "perLevel", "ap": ap(m.group(1))}
    if t.startswith("Stufe I:"):
        steps = re.findall(r"Stufe ([IV]+): (gratis|\d+)", t)
        if [ROMAN[r] for r, _ in steps] == list(range(1, len(steps) + 1)):
            return {"type": "levels", "levels": [0 if v == "gratis" else ap(v) for _, v in steps]}
    if m := re.fullmatch(r"(\d+)/(\d+) Abenteuerpunkte für Stufe I/II", t):
        return {"type": "levels", "levels": [ap(m.group(1)), ap(m.group(2))]}
    if m := re.search(r"([-–]?\d+)/([-–]?\d+)/([-–]?\d+)/([-–]?\d+) Abenteuerpunkte? \(A-/B-/C-/D-Fertigkeit\)", t) or \
            re.search(r"A-/B-/C-/D-\s*(?:Talent|Fertigkeit): ([-–]?\d+)/([-–]?\d+)/([-–]?\d+)/([-–]?\d+)", t):
        return {"type": "bySkillFactor", "target": "skill",
                "byFactor": {k: ap(v) for k, v in zip("ABCD", m.groups())},
                **({"perUse": True} if "pro Anwendung" in t else {})}
    if m := re.search(r"(\d+)/(\d+)/(\d+) Abenteuerpunkte \(\(?B-/C-/D-Kampftechnik\)?\)", t) or \
            re.search(r"B-/C-/D-Kampftechnik: (\d+)/(\d+)/(\d+)", t):
        return {"type": "bySkillFactor", "target": "combatTechnique",
                "byFactor": {k: ap(v) for k, v in zip("BCD", m.groups())}}
    options = re.findall(r"([-–]?\d+) Abenteuerpunkte \(([^)]+)\)", t)
    if len(options) >= 2 and all(not o[1].lower().startswith("pro") for o in options):
        result = []
        for value, names in options:
            for name in re.split(r"/|\boder\b", names):
                result.append({"name": name.strip(), "ap": ap(value)})
        return {"type": "byOption", "options": result}
    return {"type": "manual"}


# Tippfehler in Wiki-Voraussetzungen → Schreibweise des Eintrags (nur eindeutige Fälle)
PREREQUISITE_TYPOS = [
    ("Nachtteil", "Nachteil"),
    ("Aninmisten", "Animisten"),                            # Große Zauberauswahl
    ("Aninisten", "Animisten"),                             # Kleine Zauberauswahl
    ("Intuitive Zauberer*", "Intuitive Zauberer"),
    ("Holde* oder", "Holde oder"),                          # Gabe der Sonne
    ("Gjaslkerschamanen", "Gjalskerschamanen"),             # Keulenweihe (Gjalsker)
    ("Intuition 13", "IN 13"),                              # Blutrunen, Improvisierte Bannzeichen
    ("Geschichtswisse 8", "Geschichtswissen 8"),            # Weg des Echsenkundigen
    ("Präzisen Schuss/Wurf I", "Präziser Schuss/Wurf I"),   # Drehwurf
    ("Präziser Schuss I", "Präziser Schuss/Wurf I"),        # Präziser Schuss/Wurf
    ("Präziser Schuss II", "Präziser Schuss/Wurf II"),
    ("Gespür für das Geld", "Gespür für Geld"),             # Sinn für den Wert
    ("Rudelbeschwörung II (Feen\n", "Rudelbeschwörung II (Feen)\n"),
    ("Tradition (Tairachschamanen)", "Tradition (Tairachschamane)"),  # Keulenweihe (Tairach); SF heißt im Wiki so
    ("Sonderfertigkeit Anführer und Formation", "Sonderfertigkeit Anführer, Sonderfertigkeit Formation"),
    # Ortskenntnis für die jeweilige Stadt/Gegend: Die App prüft nur, ob eine Ortskenntnis vorhanden ist
    ("Ortskenntnis in der jeweiligen Stadt", "Ortskenntnis"),
    ("Ortskenntnis in der jeweiligen Gegend", "Ortskenntnis"),
    ("Tradition (Animisten in der Ausprägung Tierkrieger)", "Tradition (Animisten)"),
]


def fix_typos(text):
    for wrong, right in PREREQUISITE_TYPOS:
        text = text.replace(wrong, right)
    return re.sub(r"\(Feen$", "(Feen)", text)  # Rudelbeschwörung (Feen): Klammer fehlt am Textende


def prerequisites(text, own_name=""):
    """Voraussetzungstext → (strukturierte Liste, nicht erkannte Teile).

    Aufzählungen setzen den letzten Typ fort: „Kein Nachteil Behäbig, Fettleibig oder Verstümmelt (Einbeinig)“
    schließt alle drei aus.
    """
    result, unparsed = [], []
    if not text or text.strip().lower() in ("keine", "-", "–"):
        return result, unparsed
    text = fix_typos(text)
    # „Spezies, Kultur[, Profession] oder Profession/Tier muss X als automatischen oder empfohlenen Vor-/Nachteil …“
    if m := re.search(r"Spezies, Kultur(?:, Profession)? oder (?:Profession|Tier) muss (.+?) als automatischen oder "
                      r"empfohlenen (?:Vorteil|Nachteil) (?:aufweisen|besitzen)\.?", text):
        # „… muss Vorteil/Nachteil als …“ meint den Eintrag selbst
        named = m.group(1).strip()
        result.append({"type": "originRecommends", "name": own_name if named in ("Vorteil", "Nachteil") else named})
        text = text.replace(m.group(0), "")

    last = None  # Typ des letzten Eintrags, für Aufzählungen

    def add(kind, name):
        nonlocal last
        last = kind
        result.append({"type": kind, "name": name.strip()})

    for part in re.split(r"[;,](?![^(]*\))", text.replace("\n", ", ")):
        p = part.strip().rstrip(".")
        if not p:
            continue
        names = [n.strip() for n in re.split(r"\s+oder\s+(?![^(]*\))", p)]
        if m := re.fullmatch(rf"{ATTR}\s+(\d+)", p):
            result.append({"type": "attribute", "attribute": m.group(1), "min": int(m.group(2))})
            last = None
        elif m := re.fullmatch(r"(?:kein|keine)\s+(Vorteil|Nachteil|Sonderfertigkeit|Schlechte Eigenschaft)\s+(.+)", p, flags=re.I):
            kind = {"vorteil": "forbidsAdvantage", "nachteil": "forbidsDisadvantage",
                    "sonderfertigkeit": "forbidsSpecialAbility"}.get(m.group(1).lower(), "forbidsDisadvantage")
            prefix = "Schlechte Eigenschaft " if m.group(1).lower() == "schlechte eigenschaft" else ""
            # „Kein Vorteil A oder Nachteil B“
            for item in re.split(r"\s+oder\s+(?![^(]*\))", m.group(2)):
                if n := re.fullmatch(r"(Vorteil|Nachteil)\s+(.+)", item):
                    add("forbidsAdvantage" if n.group(1) == "Vorteil" else "forbidsDisadvantage", n.group(2))
                else:
                    add(kind, prefix + item)
        elif (m := re.fullmatch(r"(?:kein|keine)\s+(?!anderer)(?!Ahnenblut)(.+)", p, flags=re.I)) and "Ahnenblut" not in p:
            add("forbidsDisadvantage", m.group(1))  # „Kein Eingeschränkter Sinn (Geruch)“
        elif m := re.fullmatch(r"(Vorteil|Nachteil)\s+(.+)", p):
            kind = "requires" + ("Advantage" if m.group(1) == "Vorteil" else "Disadvantage")
            items = re.split(r"\s+oder\s+(?![^(]*\))", m.group(2))
            if len(items) > 1:  # „Vorteil Geweihter oder Prediger“: eines genügt
                result.append({"type": "anyOf", "options": [{"type": kind, "name": i.strip()} for i in items]})
                last = None
            else:
                add(kind, items[0])
        elif m := re.fullmatch(r"Leiteigenschaft(?: der)? Tradition\s+(\d+)", p):
            result.append({"type": "primaryAttribute", "min": int(m.group(1))})
            last = None
        elif m := re.fullmatch(r"Schlechte Eigenschaft\s+(.+)", p):
            add("requiresDisadvantage", "Schlechte Eigenschaft " + m.group(1))
        elif re.fullmatch(r"Sonderfertigkeit\s+.+\s+bisher nicht vorhanden", p):
            # „Sonderfertigkeit Lieblingszauber bisher nicht vorhanden“: nicht zweimal für dieselbe Auswahl – das prüft die
            # App über die Auswahl; als Voraussetzung gelesen, würde der Eintrag sich selbst ausschließen.
            last = None
        elif m := re.fullmatch(r"(?:Sonderfertigkeit\s+)?(Tradition \(.+\))", p) or re.fullmatch(r"Sonderfertigkeit\s+(.+)", p):
            add("requiresSpecialAbility", m.group(1))
        elif p == "Kultur muss über einen passenden Adel verfügen":
            result.append({"type": "cultureSocialStatus", "name": "Adel"})
            last = None
        elif m := re.fullmatch(r"(?:Kultur|Der Held muss der Kultur)\s+(.+?)(?:\s+angehören)?", p):
            result.append({"type": "culture", "names": [n.strip() for n in re.split(r"\boder\b|/", m.group(1))]})
            last = None
        elif re.fullmatch(r"(?:nicht mehr als|maximal)\s+\w+\s+\w+(?:\s+pro Person)?", p, flags=re.I):
            m = re.fullmatch(r"(?:nicht mehr als|maximal)\s+(\w+)\s+\w+(?:\s+pro Person)?", p, flags=re.I)
            words = {"zwei": 2, "drei": 3, "vier": 4, "fünf": 5}
            count = int(m.group(1)) if m.group(1).isdigit() else words.get(m.group(1).lower())
            if count is None:
                unparsed.append(p)
            else:
                result.append({"type": "maxCount", "count": count})
            last = None
        elif re.fullmatch(r"kein(?: anderer)? Ahnenblut-Vorteil", p, flags=re.I):
            result.append({"type": "exclusiveGroup", "group": "ahnenblut"})
            last = None
        elif m := re.fullmatch(r"(\w+) können diesen Vorteil nicht wählen(?:, da .*)?", p):
            result.append({"type": "forbidsSpecies", "names": [m.group(1)]})
            last = None
        elif m := re.fullmatch(r"Spezies\s+(.+)", p):
            result.append({"type": "species", "names": [n.strip() for n in re.split(r"\boder\b|/", m.group(1))]})
            last = None
        elif last in ("forbidsAdvantage", "forbidsDisadvantage", "requiresAdvantage", "requiresDisadvantage") \
                and not re.search(r"\b(muss|müssen|kann|können|wählen|ein|eine|da|der|die)\b", p, flags=re.I):
            for item in names:  # Fortsetzung einer Aufzählung
                add(last, item)
        else:
            unparsed.append(p)
            last = None
    return result, unparsed


KIND_OF = {"requiresAdvantage": "advantage", "forbidsAdvantage": "advantage", "requiresDisadvantage": "disadvantage",
           "forbidsDisadvantage": "disadvantage", "requiresSpecialAbility": "specialAbility",
           "forbidsSpecialAbility": "specialAbility"}
LEVEL = r"(?:I|II|III|IV|V|VI|VII)"


def known_name(name, names):
    """Wie die App (CharacterCalculator.NameMatches): genauer Name, Name mit Stufe („Finte II“) oder mit Auswahl
    („Herausragender Sinn (Sicht)“) – oder der Grundname mehrerer Ausprägungen („Keulenweihe“ → „Keulenweihe (Gjalsker)“)."""
    if name in names:
        return True
    for d in names:
        if name.startswith(d) and re.fullmatch(rf"\s*(?:{LEVEL}\b.*|\(.*)?", name[len(d):]) or d.startswith(name + " ("):
            return True
    return False


def expand_choices(prereqs, names):
    """„Vorteil Feenblut (Dryade oder Satyr)“, „Drachenblut (alle außer Höhlendrache)“, „Tradition (Druiden, Elfen
    oder Hexen)“, „Tradition (Gildenmagier) oder Tradition (Kristallomanten)“: als ein Name gelesen passt das auf
    keinen Eintrag → eine Voraussetzung je Ausprägung (anyOf). `names`: Art → bekannte Namen."""
    result = []
    for p in prereqs:
        kind = KIND_OF.get(p["type"])
        if kind is None or known_name(p["name"], names[kind]):
            result.append(p)
            continue
        options = None
        if p["name"] in ("Tradition (Schamane)", "Tradition (eine Schamanentradition)"):
            p = {**p, "name": "Tradition (" + ", ".join(sorted(re.sub(r"^Tradition \((.+)\)$", r"\1", n) for n in names[kind]
                                                              if re.fullmatch(r"Tradition \(\w*schamanen?\)", n))) + ")"}
        parts = [x.strip() for x in re.split(r"\s+oder\s+(?![^(]*\))", p["name"])]
        if len(parts) > 1:
            options = parts
        elif m := re.fullmatch(r"(.+?) \((.+)\)", p["name"]):
            base, inner = m.groups()
            if m2 := re.fullmatch(r"alle(?: außer (.+))?", inner):
                excluded = {f"{base} ({x.strip()})" for x in re.split(r",|\s+oder\s+|\s+und\s+", m2.group(1) or "")}
                options = sorted(n for n in names[kind] if n.startswith(base + " (") and n not in excluded)
            else:
                options = [f"{base} ({x.strip()})" for x in re.split(r",|\s+oder\s+", inner) if x.strip()]
        if options and all(known_name(o, names[kind]) for o in options):
            result.append({**{k: v for k, v in p.items() if k != "name"}, "name": options[0]} if len(options) == 1 else
                          {"type": "anyOf", "options": [{"type": p["type"], "name": o} for o in options],
                           **({"fromLevel": p["fromLevel"]} if "fromLevel" in p else {})})
        else:
            result.append(p)
    return result


def data_entries(category):
    import glob
    import json
    import os
    folder = os.path.join(books.ROOT, "src", "DSACharacterCreator.Content", "data", category)
    return [e for f in sorted(glob.glob(os.path.join(folder, "*.json"))) for e in json.load(open(f, encoding="utf-8"))]


def data_names(category):
    return {e["name"] for e in data_entries(category)}


COUNT_WORDS = {"einmal": 1, "zweimal": 2, "dreimal": 3, "viermal": 4, "fünfmal": 5}


def rule_limit(rule):
    """Anzahlgrenze aus dem Regeltext: „kann bis zu viermal gewählt werden, wobei keine Fertigkeit mehr als zweimal
    gewählt werden kann“ (Herausragende Fertigkeit), „bis zu zweimal …, wobei jede Kampftechnik nur einmal …“."""
    m = re.search(r"kann bis zu (\w+) gewählt werden, wobei (?:keine \w+ mehr als|jede \w+ nur) (\w+) gewählt", rule)
    if not m or m.group(1) not in COUNT_WORDS or m.group(2) not in COUNT_WORDS:
        return None
    return {"type": "maxCount", "count": COUNT_WORDS[m.group(1)], "perOption": COUNT_WORDS[m.group(2)]}


def entries(kind):
    page, param, prefix, sign = (("vorteilauswahl.html", "vorteil", "adv", 1) if kind == "advantages"
                                 else ("nachteilauswahl.html", "nachteil", "disadv", -1))
    result = []
    for raw in list_names(page, param):
        d = wiki.fields(wiki.fetch(page.replace("auswahl", ""), **{param: raw}))
        name, levels = split_name(raw)
        entry = {"id": f"{prefix}.{wiki.slug(name)}", "name": name}
        if levels:
            entry["maxLevel"] = levels
        entry["ruleText"] = d.get("Regel", "")
        if d.get("Voraussetzung"):
            entry["prerequisitesText"] = d["Voraussetzung"]
            structured, unparsed = prerequisites(d["Voraussetzung"], name)
            if structured:
                entry["prerequisites"] = structured
            if unparsed:
                entry["unparsedPrerequisites"] = unparsed
        if limit := rule_limit(entry["ruleText"]):
            entry.setdefault("prerequisites", []).append(limit)
        entry["apValueText"] = d.get("AP-Wert", "")
        entry["cost"] = cost(entry["apValueText"], sign)
        if entry["cost"]["type"] == "perLevel" and not levels:
            entry["cost"] = {"type": "fixed", "ap": entry["cost"]["ap"]}
        if name in EFFECTS:
            effects, proof = EFFECTS.pop(name)
            if proof not in re.sub(r"\s+", " ", entry["ruleText"]):
                raise SystemExit(f"{name}: Regeltext im Wiki geändert, Effekt prüfen ({proof!r})")
            entry["effects"] = [{"target": t, "value": v, **({"perLevel": True} if p else {})} for t, v, p in effects]
        for key, label in [("range", "Reichweite"), ("note", "Anmerkung"), ("errata", "Errata")]:
            if d.get(label):
                entry[key] = d[label]
        entry["publications"] = books.publications(d.get("Publikation(en)", ""))
        result.append(entry)
    return result


if __name__ == "__main__":
    import collections
    lists = {kind: entries(kind) for kind in ("advantages", "disadvantages")}
    if EFFECTS:
        raise SystemExit(f"Effekte ohne Wiki-Eintrag: {list(EFFECTS)}")
    names = {"advantage": {e["name"] for e in lists["advantages"]},
             "disadvantage": {e["name"] for e in lists["disadvantages"]},
             "specialAbility": data_names("special-abilities")}
    for kind, items in lists.items():
        for e in items:
            if "prerequisites" in e:
                e["prerequisites"] = expand_choices(e["prerequisites"], names)
        crawl_lists.write_by_book(kind, items)
        print("  Kosten:", dict(collections.Counter(e["cost"]["type"] for e in items)),
              "| mit unerkannten Voraussetzungen:", sum(1 for e in items if "unparsedPrerequisites" in e))
