"""Erzeugt data/special-abilities/*.json, data/languages/*.json und data/scripts/*.json aus dem Regel-Wiki
(profane Sonderfertigkeiten; magische/karmale folgen mit Meilenstein 4c/6).

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
LABELS = ["➤ Passender Kampfstil", "➤ Passender Talentstil", "Passender Kampfstil", "Passender Talentstil",
          "Beispiel", "Regel", "Regeln", "Voraussetzungen", "Voraussetzung", "AP-Wert", "Kampftechniken", "Kampftechnik",
          "Erschwernis", "Anmerkung", "Errata", "Erweiterte Kampfsonderfertigkeiten",
          "Erweiterte Talentsonderfertigkeiten", "Publikation", "Publikation(en)", "Publikationen"]
FACTOR = {2: "A", 4: "B", 6: "C", 8: "D"}


def sf_cost(text, levels):
    """Erst der ganze Text, dann nur die erste Zeile (Folgezeilen sind oft Hinweise, Tabellen, Beispiele)."""
    text = text.replace("Abenteuerpunke", "Abenteuerpunkte")
    cost = _sf_cost(text, levels)
    if cost["type"] == "manual" and "\n" in text:
        cost = _sf_cost(text.split("\n")[0], levels)
    return cost


def _sf_cost(text, levels):
    t = re.sub(r"\s+", " ", text.replace("\n", " ")).strip()
    ap = lambda v: int(v)
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
    if m := re.fullmatch(r"(\d+) Abenteuerpunkte? pro (?!Stufe)[\wäöüß]+", t):
        return {"type": "fixed", "ap": ap(m.group(1))}
    pairs = re.findall(r"([A-ZÄÖÜ][^:;]*?): (\d+) Abenteuerpunkte?", t)
    if len(pairs) >= 2 and not t.startswith("Stufe"):
        return {"type": "byOption", "options": [{"name": n.strip(), "ap": ap(v)} for n, v in pairs]}
    c = adv.cost(t, 1)
    if c["type"] == "perLevel" and not levels:
        c = {"type": "fixed", "ap": c["ap"]}
    return c


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


def main():
    pages = [p for p in json.load(open(PAGES, encoding="utf-8")) if "Hier" not in p["category"]]
    entries = []
    for info in pages:
        lines = pagetext.article_lines(info["page"])
        f = pagetext.labeled(lines, LABELS)
        raw = info["label"] if lines[0].startswith(("Regel", "AP-Wert")) else lines[0]
        name, levels = adv.split_name(raw)
        entry = {"id": "sf." + wiki.slug(name), "name": name, "category": " › ".join(info["category"])}
        if levels:
            entry["maxLevel"] = levels
        entry["ruleText"] = f.get("Regel") or f.get("Regeln") or ""
        pre = f.get("Voraussetzungen") or f.get("Voraussetzung")
        if pre:
            entry["prerequisitesText"] = pre
            pre_clean = re.sub(r"(?:Je )?die Vorgängerstufe der Sonderfertigkeit(?: \(Ausnahme: Stufe I\))?\.?", "", pre)
            structured, unparsed = adv.prerequisites(pre_clean, name)
            if structured:
                entry["prerequisites"] = structured
            if unparsed:
                entry["unparsedPrerequisites"] = unparsed
        entry["apValueText"] = f.get("AP-Wert", "")
        entry["cost"] = sf_cost(entry["apValueText"], levels)
        if entry["cost"]["type"] == "levels" and not levels:
            entry["maxLevel"] = len(entry["cost"]["levels"])
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
        entry["publications"] = books.publications(pagetext.publication_text(lines))
        entries.append(entry)

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
        for part in e.get("unparsedPrerequisites", []):
            resolved = resolve(part, sf_names)
            if resolved is not None:
                e.setdefault("prerequisites", []).extend(resolved)
            else:
                rest.append(part)
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


ROMAN = {"I": 1, "II": 2, "III": 3, "IV": 4, "V": 5}
STYLE = re.compile(r"passende[rn]? (?:Kampf|Talent)(?:stil)?(?:-? oder (?:Kampf|Talent)stil)?(?:sonderfertigkeit|-SF)?", re.I)


def resolve(part, sf_names):
    """Einzelteil → Liste strukturierter Voraussetzungen oder None.

    „Finte II“, „Tierkunde 8“, „Dolche 10“, „SF Berittener Kampf“, „KKN“, „Stufe II: KK 15“ (gilt ab Stufe II),
    „MU 13 oder KL 13“ (eine genügt), „passende Kampfstilsonderfertigkeit“.
    """
    part = part.strip().rstrip(".")
    if STYLE.fullmatch(part):
        return [{"type": "fittingStyle"}]
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
    item = re.sub(r"^(?:SF|Sonderfertigkeit)\s+", "", item.strip())
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
