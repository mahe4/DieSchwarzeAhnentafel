"""data/books.json lesen, Quellenangaben des Wikis normalisieren und neue Publikationen ergänzen.

Das Wiki schreibt Buchtitel uneinheitlich (Tippfehler, Untertitel, Jahreszahlen, Zusätze wie „[Als …]“).
CANONICAL bildet bekannte Varianten auf einen Titel ab; clean() entfernt Zusätze.
"""
import json
import os
import re

import wiki

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
PATH = os.path.join(ROOT, "src", "DSACharacterCreator.Content", "data", "books.json")

GMH = "Glaube, Macht & Heldenmut"
CANONICAL = {
    "Regelwerk (3. Edition)": "Regelwerk",
    "Regelwerk (3. überarb. Aufl.)": "Regelwerk",
    "Regelwerk (4. Auflage)": "Regelwerk",
    "Aventurisches Götterwirken I": "Aventurisches Götterwirken",
    "Aventurisches Kompendium 2": "Aventurisches Kompendium II",
    "Aventurische Magie 2": "Aventurische Magie II",
    "Aventurisches Magie 2": "Aventurische Magie II",
    "Aventurische Magie III Bonusmaterial und Errata": "Aventurische Magie 3 Errata und Bonusmaterial",
    "Wege der Vereinigung": "Wege der Vereinigungen",
    "Die Flusslande -": "Die Flusslande",
    "Flusslande -": "Die Flusslande",
    "Die Siebenwindküste - Albernia und der Windhag": "Die Siebenwindküste",
    "Die Sonnenküste - Der wilde Süden & die Zyklopeninseln": "Die Sonnenküste",
    "Die Gestade des Gottwals - Thorwal und das Gjalskerland": "Die Gestade des Gottwals",
    "Die Gestade des Gottwals – Thorwal & das Gjalskerland": "Die Gestade des Gottwals",
    "Kompedium des Gottwals": "Kompendium des Gottwals",
    "Die Streitenden Königreiche - Nostria und Andergast": "Die Streitenden Königreiche",
    "Heldend es Dornenreichs": "Helden des Dornenreichs",
    "Helden des Frostwolfs": "Helden des Wolfsfrosts",
    "Glaube Macht und Heldenmut: Das Dornenreich": f"{GMH} – Das Dornenreich",
    "Glaube Macht und Heldenmut - Das Dornenreich": f"{GMH} – Das Dornenreich",
    "Glaube, Macht & Heldenmut - Die Streitenden Königreiche": f"{GMH} – Die Streitenden Königreiche",
    "Glaube, Macht & Heldenmut - Die Sonnenküste": f"{GMH} – Die Sonnenküste",
    "Glaube, Macht & Heldenmut - Die Flusslande": f"{GMH} – Die Flusslande",
    "Glaube, Macht und Heldenmut - Die Flusslande": f"{GMH} – Die Flusslande",
    "Glaube, Macht & Heldenmut - Havena": f"{GMH} – Havena",
    "Glaube, Macht & Heldenmut - Die Gestade des Gottwals": f"{GMH} – Die Gestade des Gottwals",
}


def clean(name: str) -> str:
    """Entfernt Zusätze wie „[Als Reisende Sangara]“, „(2018)“, „(für 401 AP …)“, „(aktuelle Version)“, Punkt am Ende."""
    name = re.sub(r"\s*\[[^\]]*\]", "", name)
    name = re.sub(r"\s*\((?:\d{4}|für [^)]*|aktuelle Version)\)", "", name)
    name = name.strip().rstrip(".").strip()
    name = re.sub(r",?\s+Seite(?:\s+[A-Za-zÄÖÜäöü]+)?$", "", name)  # „Seite“ ohne Zahl, „Seite Vorsatz“
    return CANONICAL.get(name, name)


def load() -> list[dict]:
    with open(PATH, encoding="utf-8") as f:
        return json.load(f)


def save(books: list[dict]) -> None:
    books.sort(key=lambda b: (b["id"] != "grw", b["name"]))
    with open(PATH, "w", encoding="utf-8") as f:
        f.write("[\n" + ",\n".join("  " + json.dumps(b, ensure_ascii=False) for b in books) + "\n]\n")


def name_map() -> dict[str, str]:
    return {name: b["id"] for b in load() for name in [b["name"], *b["wikiNames"]]}


def add(names: set[str]) -> None:
    """Hängt unbekannte Publikationen an (nicht Ü18; Ü18-Werke von Hand mit "adult": true markieren)."""
    books = load()
    known = {n for b in books for n in [b["name"], *b["wikiNames"]]}
    for name in sorted(names - known):
        books.append({"id": wiki.slug(name), "name": name, "wikiNames": [name]})
        print("Neue Publikation:", name)
    save(books)


def _pairs(text: str) -> list[tuple[str, int | None]]:
    import pagetext
    known = name_map()
    result = []
    for name, page in pagetext.split_publications(text):
        if name.startswith("Publikation"):
            continue
        name = clean(name)
        # „Wege der Vereinigungen 210“: Seitenzahl ohne „Seite“ – nur, wenn der Rest ein bekanntes Buch ist.
        m = re.match(r"(.+?)\s+(\d+)$", name)
        if page is None and m and clean(m.group(1)) in known:
            name, page = clean(m.group(1)), int(m.group(2))
        result.append((name, page))
    return result


def publications(text: str) -> list[dict]:
    """Quellenangaben → [{"book": id, "page": n}]; unbekannte Bücher werden ergänzt.

    Nennt das Wiki keine Quelle, wird die Pseudo-Quelle „wiki“ (Regel-Wiki ohne Quellenangabe) eingetragen.
    """
    pairs = _pairs(text)
    if not pairs:
        return [{"book": "wiki"}]
    add({name for name, _ in pairs})
    ids = name_map()
    result = []
    for name, page in pairs:
        entry = {"book": ids[name]} | ({"page": page} if page is not None else {})
        if entry not in result:
            result.append(entry)
    return result
