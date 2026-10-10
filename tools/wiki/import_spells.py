"""Erzeugt data/spells/*.json aus dem Regel-Wiki: Zaubersprüche, Rituale, Zaubertricks, magische Handlungen,
Liturgien, Zeremonien und Segnungen (Seitenliste aus crawl_spells.py).

Magische Handlungen (Flüche, Elfenlieder, Zaubermelodien …) werden laut Wiki wie Zauber aktiviert und gesteigert
(„alle Grundregeln für Fertigkeiten gelten“); sie unterscheiden sich beim Wirken.

Strukturiert: Probe (drei Eigenschaften), Steigerungsfaktor, Merkmal bzw. Aspekt, Verbreitung (Traditionen bzw.
Gottheiten). Die übrigen Felder (Dauer, Kosten, Reichweite …) bleiben Wiki-Text.
"""
import collections
import json
import os
import re

import books
import crawl_lists
import pagetext
import wiki

PAGES = os.path.join(os.path.dirname(__file__), ".cache", "_spell_pages.json")
ATTR = r"(MU|KL|IN|CH|FF|GE|KO|KK)"
PREFIX = {"spell": "spell", "ritual": "ritual", "cantrip": "cantrip", "liturgy": "liturgy", "ceremony": "ceremony",
          "blessing": "blessing", "magicalAction": "action"}
# Arten ohne eigenen Steigerungsfaktor im Eintrag: Faktor laut Übersichtsseite (Satz wird im Wiki geprüft).
CATEGORY_FACTOR = {
    "Hexenflüche": ("B", "hexenfluchauswahl.html", "Flüche funktionieren in allen Belangen wie Spruchzauber der Steigerungskategorie B"),
    "Herrschaftsrituale": ("B", "herrschaftsritualauswahl.html",
                           "Herrschaftsrituale funktionieren in allen Belangen wie Rituale der Steigerungskategorie B"),
    "Geodenrituale": ("B", "geodenritualauswahl.html",
                      "werden aber ansonsten wie Rituale der Steigerungskategorie B behandelt"),
}
# Faktor als Auswahl: „- (je nach Tierart)“ → A–D selbst wählen; „Bannzeichen wider (Wesenheit)“ → je Wesenheit
ANY_FACTOR = [{"name": f"Steigerungsfaktor {f}", "factor": f} for f in "ABCD"]
# Voraussetzungen je Art (Wiki-Übersichtsseiten bzw. Vorteil „Zweistimmiger Gesang“: „Möglichkeit, Elfenlieder zu erlernen“)
CATEGORY_PREREQUISITES = {
    "Elfenlieder": [{"type": "requiresAdvantage", "name": "Zweistimmiger Gesang"}],
    "Verzerrte Elfenlieder": [{"type": "requiresAdvantage", "name": "Zweistimmiger Gesang"}],
}
SKIP_GROUPS = set()
FIELDS = {
    "Probe": "checkText", "Wirkung": "ruleText", "Regel": "ruleText", "Steigerungsfaktor": None, "Merkmal": "property",
    "Aspekt": "aspect", "Verbreitung": "traditionsText", "Anmerkung": "note", "Errata": "errata",
    "Publikation(en)": None, "Publikation": None, "Publikationen": None,
}


def page_text(page):
    t = re.sub(r"<script.*?</script>|<style.*?</style>", "", wiki.fetch(page), flags=re.S)
    import html
    return re.sub(r"\s+", " ", html.unescape(re.sub(r"<[^>]+>", " ", t)))


def check_category_factors():
    for category, (factor, page, sentence) in CATEGORY_FACTOR.items():
        if sentence not in page_text(page):
            raise SystemExit(f"Wiki-Satz zum Steigerungsfaktor von {category} nicht mehr gefunden: {sentence}")


def entry(info):
    if info["kind"] == "article":
        lines = pagetext.article_lines(info["page"])
        labels = list(FIELDS) + ["AP-Wert", "AsP-Kosten", "Reichweite", "Wirkungsdauer", "Zielkategorie", "Talent"]
        f = pagetext.labeled(lines, labels)
        name = info["label"]
        publications = books.publications(pagetext.publication_text(lines))
    else:
        f = wiki.fields(wiki.fetch(info["page"], **{info["param"]: info["value"]}))
        name = info["value"]
        publications = books.publications(f.get("Publikation(en)") or f.get("Publikation") or f.get("Publikationen") or "")
    group = info["group"]
    e = {"id": f"{PREFIX[group]}.{wiki.slug(name)}", "name": name, "group": group, "category": info["category"]}
    details = []
    for label, value in f.items():
        key = FIELDS.get(label, "details")
        if key == "details":
            details.append(f"{label}: {value}")
        elif key and value:
            e.setdefault(key, value)
    e.setdefault("ruleText", "")
    if m := re.search(rf"{ATTR}\s*/\s*{ATTR}\s*/\s*{ATTR}", e.get("checkText", "")):
        e["check"] = list(m.groups())
    factor = (f.get("Steigerungsfaktor") or "").strip()
    if factor in ("A", "B", "C", "D"):
        e["improvementCost"] = factor
    elif info["category"] in CATEGORY_FACTOR:
        e["improvementCost"] = CATEGORY_FACTOR[info["category"]][0]
    elif "je nach" in factor:
        e["factorOptions"] = ANY_FACTOR
    elif m := re.match(r"A \((.+?)\), B \((.+?)\) und C \((.+?)\)", factor):
        e["factorOptions"] = [{"name": re.sub(r"^gegen ", "", n), "factor": f} for f, n in zip("ABC", m.groups())]
    if info["category"] in CATEGORY_PREREQUISITES:
        e["prerequisites"] = CATEGORY_PREREQUISITES[info["category"]]
    if m := re.search(r"(\d+)\s+Abenteuerpunkte?", f.get("AP-Wert", "")):
        e["ap"] = int(m.group(1))
    if "traditionsText" in e:
        e["traditions"] = traditions(e["traditionsText"])
        if group in ("liturgy", "ceremony") and (found := aspects(e["traditionsText"])):
            e["aspects"] = found
    elif group == "blessing" and (m := re.search(r"Verbreitung:\s*(.+)", e.get("note", ""))):
        e["traditionsText"] = m.group(1)
        e["traditions"] = traditions(m.group(1))
    if details:
        e["detailsText"] = "\n".join(details)
    e["publications"] = publications
    return e


def traditions(text):
    """„Druiden, Geoden, Gildenmagier“ → Liste; „Praios (Ordnung)“ → „Praios“; „Allgemein“ bzw. „allgemein“ bleibt."""
    result = []
    for part in re.split(r"[,;]\s*|\s+und\s+", text):
        part = re.sub(r"\s*\(.*?\)", "", part).strip(" .")
        if part:
            result.append("Allgemein" if part.lower() == "allgemein" else part)
    return list(dict.fromkeys(result))


def aspects(text):
    """„Praios (Ordnung), Rondra (Schild)“ → [„Ordnung“, „Schild“]: Aspekte stehen bei Liturgien in Klammern."""
    found = []
    for inner in re.findall(r"\(([^)]*)\)", text):
        found += [a.strip() for a in re.split(r",|/|\s+und\s+", inner) if a.strip()]
    return list(dict.fromkeys(found))


def main():
    check_category_factors()
    seen, entries = set(), []
    for info in json.load(open(PAGES, encoding="utf-8")):
        key = (info["page"], info.get("value"))
        if key in seen:
            continue
        seen.add(key)
        if info["group"] in SKIP_GROUPS:
            continue
        e = entry(info)
        # „Zweistimmiger Gesang“ steht als Erklärseite in der Liste der verzerrten Elfenlieder
        if not e["publications"] or e["group"] == "magicalAction" and "check" not in e:
            continue
        entries.append(e)
    ids = collections.Counter(e["id"] for e in entries)
    for e in entries:
        if ids[e["id"]] > 1:
            e["id"] += "-" + wiki.slug(e["category"])
    crawl_lists.write_by_book("spells", entries)
    stats = collections.Counter(e["group"] for e in entries)
    no_factor = collections.Counter(e["category"] for e in entries if "improvementCost" not in e and "factorOptions" not in e
                                    and e["group"] not in ("cantrip", "blessing") and "ap" not in e)
    no_check = sum(1 for e in entries if "check" not in e and e["group"] not in ("cantrip", "blessing"))
    print("  ", dict(stats), "| ohne Steigerungsfaktor:", dict(no_factor), "| ohne Probe:", no_check)


if __name__ == "__main__":
    main()
