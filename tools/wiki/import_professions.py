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
    result = []
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
    entry = {"id": wiki.slug(name), "name": name, "ap": ap, "skills": [], "combatTechniques": []}
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


def ap_value(f, lines):
    """„AP-Wert: 387 Abenteuerpunkte“, vereinzelt ohne Doppelpunkt."""
    text = f.get("AP-Wert") or next(l for l in lines if l.startswith("AP-Wert"))
    return int(re.search(r"\d+", text).group(0))


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
    entries, stats = [], {"unparsed": 0}
    for info in pages:
        lines = pagetext.article_lines(info["page"])
        f = pagetext.labeled(lines, LABELS)
        unparsed = []
        name = lines[0]
        entry = {"id": "profession." + wiki.slug(name), "name": name, "page": info["page"],
                 "group": " › ".join(labels.get(p, p) for p in info["category"]),
                 "ap": ap_value(f, lines)}
        for label, key in TEXT_FIELDS.items():
            if f.get(label) and f[label].strip().lower() != "keine":
                entry[key] = f[label]
        attrs = {}
        for a, v in re.findall(ATTR, f.get("Voraussetzungen", "")):
            attrs[a] = max(attrs.get(a, 0), int(v))
        if attrs:
            entry["attributeRequirements"] = attrs
        fixed, choices = combat_techniques(f.get("Kampftechniken", ""), unparsed)
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
        if styles:
            entry["stylesText"] = styles
        variants_text = f.get("Varianten") or f.get("Variante") or f.get("Varianten (eine davon muss gewählt werden)")
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
                entry["variantRequired"] = "eine davon muss gewählt werden" in "\n".join(lines)
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
