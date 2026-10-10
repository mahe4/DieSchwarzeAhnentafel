"""Erzeugt data/skills/*.json (Talente) und data/combat-techniques/*.json aus dem Regel-Wiki.

Aufruf aus dem Repo-Wurzelverzeichnis:  python3 tools/wiki/import_skills.py
"""
import json
import os
import re
from collections import defaultdict

import books as bookdb
import wiki

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
DATA = os.path.join(ROOT, "src", "DSACharacterCreator.Content", "data")

ATTRIBUTES = {
    "Mut": "MU", "Klugheit": "KL", "Intuition": "IN", "Charisma": "CH",
    "Fingerfertigkeit": "FF", "Gewandtheit": "GE", "Gewandheit": "GE",  # Schreibfehler im Wiki
    "Konstitution": "KO", "Körperkraft": "KK",
}
SKILL_GROUPS = {
    "Körpertalente": "body", "Gesellschaftstalente": "social", "Naturtalente": "nature",
    "Wissenstalente": "knowledge", "Handwerkstalente": "craft",
}


def load_books():
    with open(os.path.join(DATA, "books.json"), encoding="utf-8") as f:
        return {name: b["id"] for b in json.load(f) for name in b["wikiNames"]}


def list_page(page, param):
    """Einträge einer Auswahlseite; die Liste wird im Wiki per JavaScript gefüllt, steht aber im HTML."""
    html_text = wiki.fetch(page)
    names = re.findall(rf"href=[\"']?[^\"'>]*\?{param}=([^\"'>]+)", html_text)
    from urllib.parse import unquote_plus
    return list(dict.fromkeys(unquote_plus(n) for n in names))


def encumbrance(text):
    t = text.lower()
    if t == "ja":
        return "yes"
    if t == "nein":
        return "no"
    return "maybe"


def write(category, entries):
    """Eine Datei je Buch (erste Publikation des Eintrags)."""
    by_book = defaultdict(list)
    for e in entries:
        by_book[e["publications"][0]["book"]].append(e)
    folder = os.path.join(DATA, category)
    os.makedirs(folder, exist_ok=True)
    for book, items in by_book.items():
        items.sort(key=lambda e: e["name"])
        with open(os.path.join(folder, book + ".json"), "w", encoding="utf-8") as f:
            json.dump(items, f, ensure_ascii=False, indent=2)
            f.write("\n")
    print(f"{category}: {len(entries)} Einträge in {len(by_book)} Datei(en)")


def skills(books, names):
    result = []
    for name in names:
        d = wiki.fields(wiki.fetch("talent.html", talent=name))
        entry = {
            "id": "skill." + wiki.slug(name),
            "name": name,
            "group": SKILL_GROUPS[d["Talentkategorie"]],
            "check": d["Probe"].split("/"),
            "improvementCost": d["Steigerungsfaktor"],
            "encumbrance": encumbrance(d["Belastung"]),
            "encumbranceText": d["Belastung"],
            "applications": d["Anwendungsgebiet"],
        }
        if "Werkzeuge" in d:
            entry["tools"] = d["Werkzeuge"]
        entry |= {
            "quality": d["Qualität"],
            "failedCheck": d["Misslungene Probe"],
            "criticalSuccess": d["Kritischer Erfolg"],
            "botch": d["Patzer"],
            "publications": bookdb.publications(d["Publikation(en)"]),
        }
        assert all(a in ATTRIBUTES.values() for a in entry["check"]), name
        result.append(entry)
    return result


def combat_techniques(books, names):
    result = []
    for name in names:
        d = wiki.fields(wiki.fetch("kampftechnik.html", kampftechnik=name))
        ranged = d["Kampftechnikkategorie"] == "Fernkampf"
        notes = d.get("Besonderheiten", "")
        primary = [ATTRIBUTES[a.strip()] for a in d["Leiteigenschaft"].split(",")]
        entry = {
            "id": "ct." + wiki.slug(name),
            "name": name,
            "category": "ranged" if ranged else "melee",
            "primaryAttributes": primary,
            "improvementCost": d["Steigerungsfaktor"],
            # Leiteigenschaften-Tabelle (Kodex der Helden): Attacke mit MU, bei Peitschen mit FF; Fernkampf mit FF.
            "attackAttribute": "FF" if ranged or name == "Peitschen" else "MU",
            "canParry": not ranged and "keine Parade möglich" not in notes,
        }
        if notes:
            entry["special"] = notes
        entry["publications"] = bookdb.publications(d["Publikation(en)"])
        result.append(entry)
    return result


if __name__ == "__main__":
    books = load_books()
    write("skills", skills(books, list_page("talentauswahl.html", "talent")))
    write("combat-techniques", combat_techniques(books, list_page("kampftechnikauswahl.html", "kampftechnik")))
