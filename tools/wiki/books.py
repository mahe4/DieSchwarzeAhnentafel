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
    "Kodex des Schwerter": "Kodex des Schwertes",
    "Regelwerk (3. überar. Auflage)": "Regelwerk",
    "Basaltschmelze": "Auf ins Abenteuer: Basaltschmelze",
    "Ruf des Berges": "Der Ruf des Berges",
    "Die Dampfenden Dschungel - Der Tiefe Süden & die Waldinseln": "Die Dampfenden Dschungel",
    "Aventurische Magie III Eratta": "Aventurische Magie 3 Errata und Bonusmaterial",
    "Aventurische Magie 3 Bonusmaterial und Errata": "Aventurische Magie 3 Errata und Bonusmaterial",
    "Heldenwerk – Archiv II": "Heldenwerk-Archiv II",
    "Aventurisches Rüstkammer 2": "Aventurische Rüstkammer II",
    "Aventurische Rüstkammer 2": "Aventurische Rüstkammer II",
    "Brandans Pakt 4: Tiefen aus Toschkril": "Tiefen aus Toschkril",
    "Auf ins Abenteuer - Havena": "Auf ins Abenteuer: Havena",
    "Aventurische Magie 3 Bonusmaterial un Errata": "Aventurische Magie 3 Errata und Bonusmaterial",
    "Mysterien des Goldenen Kaiserszeit": "Mysterien der Goldenen Kaiserzeit",
    "Mysterien der Siebenwindküse": "Mysterien der Siebenwindküste",
    "Aventurisches Götterwirken II (2. Auflage)": "Aventurisches Götterwirken II",
    "Glaube, Macht & Heldenmut: Die Flusslande": f"{GMH} – Die Flusslande",
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
    "Studierstube der Gildenmagie, vordere Umschlag &": "Studierstube der Gildenmagie",
    "Sternenträger II: Der Klang des Feyraasal": "Der Klang des Feyraasal (Sternenträger II)",
    "Sternenträger IV: Das Sturmgeheul von Shiyadur": "Das Sturmgeheul von Shiyadur (Sternenträger IV)",
    "Zähne des Kaimans - Rabenkrieg 1": "Die Zähne des Kaimans (Rabenkrieg I)",
}


# Häufige Tippfehler und Varianten in Titeln (nach der Grundnormalisierung)
TYPOS = {
    "Sonneküste": "Sonnenküste",
    "Siebenwindküsten": "Siebenwindküste",
    "Gestaden des Gottwals": "Die Gestade des Gottwals",
}
SERIES_ADVENTURES = "Rabenkrieg|Sternenträger|Theaterritter"
# „Reihe 2“ → „Reihe II“ (nur für Reihen, die im Wiki römisch nummeriert sind)
SERIES = ["Aventurische Rüstkammer", "Aventurisches Bestiarium", "Aventurisches Götterwirken", "Aventurisches Herbarium",
          "Aventurisches Pandämonium", "Aventurisches Kompendium", "Aventurische Magie"]


def clean(name: str) -> str:
    """Entfernt Zusätze wie „[Als Reisende Sangara]“, „(2018)“, „(für 401 AP …)“, „(aktuelle Version)“, Punkt am Ende,
    und vereinheitlicht Schreibweisen (GMH-Bände, römische Bandnummern, bekannte Tippfehler)."""
    name = re.sub(r"\s*\[[^\]]*\]", "", name)
    name = re.sub(r"\s*\((?:\d{4}|für [^)]*|aktuelle Version)\)", "", name)
    name = name.strip().rstrip(".").strip()
    name = re.sub(r",?\s+Seite(?:\s+[A-Za-zÄÖÜäöü]+)?$", "", name)  # „Seite“ ohne Zahl, „Seite Vorsatz“
    name = re.sub(r"\s*\(2\. Auflage\)$", "", name)
    # „Glaube, Macht & Heldenmut“ in allen Varianten („Heldentum“, „und“, fehlendes Komma, „-“/„:“ statt „–“)
    name = re.sub(r"^GM&H\b", "Glaube, Macht & Heldenmut", name)
    m = re.match(r"^Glauben?,?\s*Macht\s*(?:und\s*)?&?\s*Helden(?:mut|tum)\s*[-–—:]*\s*(.*)$", name)
    if m:
        name = "Glaube, Macht & Heldenmut" + (" – " + m.group(1).strip() if m.group(1).strip() else "")
    for wrong, right in TYPOS.items():
        name = re.sub(rf"\b{re.escape(wrong)}\b", right, name)
    name = re.sub(r"\s*\((?:[^)]*alte (?:Version|Auflage)[^)]*)\)?$", "", name)  # „(alte Version)“, „(2018, alte Auflage)“
    name = re.sub(r"\s*\(\d\.\s*(?:überarb?\.?\s*)?Aufl(?:age|\.)\)$", "", name)  # „(6. Auflage)“
    # Abenteuerreihen: „Rabenkrieg 1: Die Zähne des Kaimans“ → „Die Zähne des Kaimans (Rabenkrieg I)“
    roman = ["", "I", "II", "III", "IV", "V", "VI", "VII"]
    if m := re.match(rf"^({SERIES_ADVENTURES}) (\d): (.+)$", name):
        name = f"{m.group(3)} ({m.group(1)} {roman[int(m.group(2))]})"
    # sonstige Klammerzusätze („(Meisterschirmbegleitheft)“, „(Die Gestade des Gottwals)“) – außer Reihenangaben
    elif (m := re.match(r"^(.+?)\s*\(([^)]*)\)$", name)) and not re.search(SERIES_ADVENTURES, m.group(2)) \
            and not m.group(1).endswith(("Feenblut", "Drachenblut")):
        name = m.group(1)
    for series in SERIES:
        if name == series + " 1":
            name = series
        elif name in (series + " 2", series + " 3"):
            name = series + (" II" if name.endswith("2") else " III")
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


def _numbering(name: str) -> tuple:
    """Bandnummern („II“, „2“, „Rabenkrieg IV“) – Titel mit verschiedener Nummer sind verschiedene Bücher."""
    return tuple(re.findall(r"\b(?:[IVX]+|\d+)\b", name))


def similar(name: str, books: list[dict]) -> dict | None:
    """Fast gleicher bekannter Titel (Tippfehler im Wiki), aber nie ein Band mit anderer Nummer."""
    import difflib
    candidates = {n: b for b in books for n in [b["name"], *b["wikiNames"]]}
    # „Der schwarze Forst“ ↔ „Der schwarze Forst (Theaterritter III)“
    for known, book in candidates.items():
        if known.startswith(name + " (") and re.search(SERIES_ADVENTURES, known):
            return book
    for match in difflib.get_close_matches(name, list(candidates), n=3, cutoff=0.88):
        if _numbering(match) == _numbering(name):
            return candidates[match]
    return None


def add(names: set[str]) -> None:
    """Hängt unbekannte Publikationen an (nicht Ü18; Ü18-Werke von Hand mit "adult": true markieren).

    Fast gleiche Titel werden als weitere Schreibweise des bekannten Buchs eingetragen.
    """
    books = load()
    known = {n for b in books for n in [b["name"], *b["wikiNames"]]}
    for name in sorted(names - known):
        if (match := similar(name, books)) is not None:
            match["wikiNames"].append(name)
            print("Schreibvariante:", name, "→", match["name"])
            continue
        same = next((b for b in books if b["id"] == wiki.slug(name)), None)
        if same:
            # nur andere Schreibweise desselben Titels (z. B. „und“ statt „&“)
            same["wikiNames"].append(name)
            print("Weitere Schreibweise:", name, "→", same["name"])
            continue
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
    known = name_map()
    # Unbekannte Namen ohne Seitenangabe sind meist keine Bücher (z. B. Tabellenzellen auf Sammelseiten).
    pairs = [(n, p) for n, p in _pairs(text) if p is not None or n in known]
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
