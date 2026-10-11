"""Erzeugt data/spells/*.json aus dem Regel-Wiki: Zaubersprüche, Rituale, Zaubertricks, magische Handlungen,
Liturgien, Zeremonien und Segnungen (Seitenliste aus crawl_spells.py).

Magische Handlungen (Flüche, Elfenlieder, Zaubermelodien …) werden laut Wiki wie Zauber aktiviert und gesteigert
(„alle Grundregeln für Fertigkeiten gelten“); sie unterscheiden sich beim Wirken.

Strukturiert: Probe (drei Eigenschaften), Steigerungsfaktor, Merkmal bzw. Aspekt, Verbreitung (Traditionen bzw.
Gottheiten). Die übrigen Felder (Dauer, Kosten, Reichweite …) bleiben Wiki-Text.
"""
import collections
import difflib
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
        page_html = wiki.fetch(info["page"], **{info["param"]: info["value"]})
        f = wiki.fields(page_html)
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
    if info["kind"] != "article" and (found := enhancements(page_html)):
        e["enhancements"] = found
    e["publications"] = publications
    return e


ENHANCEMENT_SECTION = re.compile(
    r'<div class="body_einzeln">\s*(?:Zauber|Liturgie|Ritual|Zeremonie)erweiterungen\s*</div>(.*?)'
    r'(?=<div class="body_einzeln">|<div class="line_separator">|$)', re.S)
ENHANCEMENT_HEAD = re.compile(r"#\s*(.+?)\s*\(FW\s*(\d+)\s*,\s*(\d+)\s*AP\)\s*:?")
ENHANCEMENT_PREREQUISITE = re.compile(r"Voraussetzung:\s*(.+?)\s*(?:\.|$)")
ENHANCEMENT_WORD = re.compile(r"^(?:Zauber|Liturgie|Ritual|Zeremonie)erweiterung(?:en|s)?\s*[:;,]?\s*")


def enhancements(page_html):
    """Zauber-/Liturgieerweiterungen (Fokusregel): „#Intensiver Strahl (FW 8, 3 AP):“ + Regeltext. Voraussetzungen
    („Voraussetzung: Zaubererweiterung Zwei Ziele.“) stehen im Regeltext; passen sie zu Erweiterungen desselben Eintrags,
    stehen sie in `requires`, sonst (andere Bedingungen) als `prerequisiteText`."""
    result = []
    for section in ENHANCEMENT_SECTION.finditer(page_html):
        current = None
        for div in re.findall(r"<div>(.*?)</div>", section.group(1), flags=re.S):
            text = wiki._text(div)
            if m := ENHANCEMENT_HEAD.fullmatch(text):
                current = {"id": wiki.slug(m.group(1)), "name": m.group(1), "minValue": int(m.group(2)),
                           "ap": int(m.group(3)), "ruleText": ""}
                result.append(current)
            elif current is not None and text and not text.startswith("#"):
                current["ruleText"] = (current["ruleText"] + "\n" + text).strip()
            else:
                current = None  # leerer Platzhalter „# (, 0 AP):“
    seen = collections.Counter()
    for e in result:  # gleicher Name zweimal auf einer Seite (Wiki-Fehler): eindeutige ID
        seen[e["id"]] += 1
        if seen[e["id"]] > 1:
            e["id"] += f"-{seen[e['id']]}"
    for e in result:
        if not (m := ENHANCEMENT_PREREQUISITE.search(e["ruleText"])):
            continue
        text = ENHANCEMENT_WORD.sub("", m.group(1))
        whole = find_enhancement(result, text, e)
        names = [whole] if whole else split_and(result, text, e)
        if all(names):
            e["requires"] = [n for n in names]
        else:
            e["prerequisiteText"] = m.group(1)
    return result


def split_and(result, text, own):
    """„A und B“, wobei Namen selbst „und“ enthalten können („Waffen- und Rüstungsreparatur“)."""
    parts = text.split(" und ")
    for i in range(1, len(parts)):
        a, b = " und ".join(parts[:i]), " und ".join(parts[i:])
        if (x := find_enhancement(result, a, own)) and (y := find_enhancement(result, b, own)):
            return [x, y]
    return [None]


def find_enhancement(result, name, own):
    """Erweiterung desselben Eintrags zu einem Namen aus „Voraussetzung“; „Längere Wirkungsdauer 1“ findet auch
    „Längere Wirkungsdauer“ (und umgekehrt), „Längere Wirkungsdauer2“ auch „… 2“."""
    key = norm_enhancement(name)
    candidates = [key, re.sub(r" 1$", "", key), key + " 1"]
    others = {norm_enhancement(e["name"]): e["id"] for e in result if e is not own}
    for c in candidates:
        if c in others:
            return others[c]
    # Tippfehler im Wiki („Heilsamer Zuflucht 1“, „Großer Radius 1“ für „Größerer Radius 1“): nur sehr ähnliche Namen
    close = difflib.get_close_matches(key, list(others), n=1, cutoff=0.88)
    return others[close[0]] if close else None


def norm_enhancement(name):
    """„Gute Illusion I“ = „Gute Illusion 1“, „Wirkungsdauer2“ = „Wirkungsdauer 2“; Groß-/Kleinschreibung egal."""
    roman = {"I": "1", "II": "2", "III": "3", "IV": "4", "V": "5"}
    name = re.sub(r"(\D)(\d)$", r"\1 \2", name.strip())
    words = [roman.get(w, w) for w in re.sub(r"[^\w ]", " ", name).split()]
    return " ".join(words).lower()


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
    enh = [x for e in entries for x in e.get("enhancements", [])]
    open_prerequisites = [(e["name"], x["name"], x["prerequisiteText"]) for e in entries for x in e.get("enhancements", [])
                          if "prerequisiteText" in x]
    duplicates = [e["name"] for e in entries if len({x["id"] for x in e.get("enhancements", [])}) != len(e.get("enhancements", []))]
    print("   Erweiterungen:", len(enh), "| mit Voraussetzung:", sum(1 for x in enh if "requires" in x),
          "| Voraussetzung nur als Text:", len(open_prerequisites), "| doppelte Namen:", duplicates)
    for o in open_prerequisites:
        print("     ", o)


if __name__ == "__main__":
    main()
