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

ROMAN = {"I": 1, "II": 2, "III": 3, "IV": 4, "V": 5, "VI": 6, "VII": 7}
ATTR = r"(MU|KL|IN|CH|FF|GE|KO|KK)"


def list_names(page, param):
    h = wiki.fetch(page)
    return list(dict.fromkeys(unquote_plus(n) for n in re.findall(rf"href=[\"']?[^\"'>]*\?{param}=([^\"'>]+)", h)))


def split_name(raw):
    """„Adel I-III“ → („Adel“, 3); „Altersresistenz (*)“ → („Altersresistenz“, None)."""
    name = re.sub(r"\s*\(\*\)\s*$", "", raw).strip()
    m = re.match(r"(.+?)\s+I\s*[-–]\s*(II|III|IV|V|VI|VII)$", name)
    if m:
        return m.group(1).strip(), ROMAN[m.group(2)]
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


def prerequisites(text, own_name=""):
    """Voraussetzungstext → (strukturierte Liste, nicht erkannte Teile)."""
    result, unparsed = [], []
    if not text or text.strip().lower() in ("keine", "-", "–"):
        return result, unparsed
    text = text.replace("Nachtteil", "Nachteil")
    # „Spezies, Kultur oder Profession muss X als automatischen oder empfohlenen Vorteil aufweisen/besitzen“
    if m := re.search(r"Spezies, Kultur oder Profession muss (.+?) als automatischen oder empfohlenen Vorteil "
                      r"(?:aufweisen|besitzen)\.?", text):
        # „… muss Vorteil als …“ meint den Vorteil selbst
        named = m.group(1).strip()
        result.append({"type": "originRecommends", "name": own_name if named == "Vorteil" else named})
        text = text.replace(m.group(0), "")
    for part in re.split(r"[;,](?![^(]*\))", text.replace("\n", ", ")):
        p = part.strip().rstrip(".")
        if not p:
            continue
        if m := re.fullmatch(rf"{ATTR}\s+(\d+)", p):
            result.append({"type": "attribute", "attribute": m.group(1), "min": int(m.group(2))})
        elif m := re.fullmatch(r"(?:kein|keine)\s+(Vorteil|Nachteil)\s+(.+?)\s+oder\s+(Vorteil|Nachteil)\s+(.+)", p, flags=re.I):
            for kind, target in ((m.group(1), m.group(2)), (m.group(3), m.group(4))):
                result.append({"type": "forbids" + ("Advantage" if kind == "Vorteil" else "Disadvantage"),
                               "name": target.strip()})
        elif m := re.fullmatch(r"(?:kein|keine)\s+(Vorteil|Nachteil)\s+(.+)", p, flags=re.I):
            result.append({"type": "forbids" + ("Advantage" if m.group(1) == "Vorteil" else "Disadvantage"),
                           "name": m.group(2).strip()})
        elif m := re.fullmatch(r"(Vorteil|Nachteil)\s+(.+)", p):
            result.append({"type": "requires" + ("Advantage" if m.group(1) == "Vorteil" else "Disadvantage"),
                           "name": m.group(2).strip()})
        elif m := re.fullmatch(r"Sonderfertigkeit\s+(.+)", p):
            result.append({"type": "requiresSpecialAbility", "name": m.group(1).strip()})
        elif re.fullmatch(r"kein(?: anderer)? Ahnenblut-Vorteil", p, flags=re.I):
            result.append({"type": "exclusiveGroup", "group": "ahnenblut"})
        elif m := re.fullmatch(r"(\w+) können diesen Vorteil nicht wählen", p):
            result.append({"type": "forbidsSpecies", "names": [m.group(1)]})
        elif m := re.fullmatch(r"Spezies\s+(.+)", p):
            result.append({"type": "species", "names": [n.strip() for n in re.split(r"\boder\b|/", m.group(1))]})
        else:
            unparsed.append(p)
    return result, unparsed


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
    for kind in ("advantages", "disadvantages"):
        items = entries(kind)
        if kind == "disadvantages" and EFFECTS:
            raise SystemExit(f"Effekte ohne Wiki-Eintrag: {list(EFFECTS)}")
        crawl_lists.write_by_book(kind, items)
        print("  Kosten:", dict(collections.Counter(e["cost"]["type"] for e in items)),
              "| mit unerkannten Voraussetzungen:", sum(1 for e in items if "unparsedPrerequisites" in e))
