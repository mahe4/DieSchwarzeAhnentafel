"""Erzeugt data/species/*.json aus dem Regel-Wiki.

Die Eigenschaftsänderungen werden hier strukturiert festgelegt und gegen den Wiki-Text geprüft
(der Text ist Freitext, z. B. „KL oder KK –2“).
"""
import json
import os
import re
from collections import defaultdict

import books
import pagetext
import wiki

DATA = os.path.join(books.ROOT, "src", "DSACharacterCreator.Content", "data")
ALL = ["MU", "KL", "IN", "CH", "FF", "GE", "KO", "KK"]


def fixed(attr, value):
    return {"options": [attr], "value": value}


def choice(options, value):
    return {"options": options, "value": value}


# Seite → (ID, Wiki-Text der Eigenschaftsänderungen, strukturierte Änderungen)
SPECIES = {
    "Spez_Achaz.html": ("achaz", "IN und KO +1; MU oder KK –1",
                        [fixed("IN", 1), fixed("KO", 1), choice(["MU", "KK"], -1)]),
    "Spez_Elfen.html": ("elf", "IN und GE +1; KL oder KK –2",
                        [fixed("IN", 1), fixed("GE", 1), choice(["KL", "KK"], -2)]),
    "spezies/goblins.html": ("goblin", "FF +1 und GE +1, MU -1 oder KL -1",
                             [fixed("FF", 1), fixed("GE", 1), choice(["MU", "KL"], -1)]),
    "Spez_Halbelfen.html": ("halbelf", "eine beliebige Eigenschaft nach Wahl +1", [choice(ALL, 1)]),
    "Spez_Halborks.html": ("halbork", "KO +1, CH -1", [fixed("KO", 1), fixed("CH", -1)]),
    "Spez_Holberker.html": ("holberker", "KL –1, KO +1", [fixed("KL", -1), fixed("KO", 1)]),
    "Spez_Menschen.html": ("mensch", "eine beliebige Eigenschaft nach Wahl +1", [choice(ALL, 1)]),
    "spezies/nachtalb.html": ("nachtalb", "IN und GE +1; KL oder KK -2",
                              [fixed("IN", 1), fixed("GE", 1), choice(["KL", "KK"], -2)]),
    "Spez_Orks.html": ("ork", "MU +1 und KO +1, KL –1 oder CH –1",
                       [fixed("MU", 1), fixed("KO", 1), choice(["KL", "CH"], -1)]),
    "Spez_Zwerge.html": ("zwerg", "KO und KK +1; CH oder GE –2",
                         [fixed("KO", 1), fixed("KK", 1), choice(["CH", "GE"], -2)]),
}

LABELS = ["AP-Wert", "Lebensenergie-Grundwert", "Seelenkraft-Grundwert", "Zähigkeit-Grundwert",
          "Geschwindigkeit-Grundwert", "Eigenschaftsänderungen", "Übliche Kulturen",
          "Automatischer Vorteil", "Automatischer Vorteile", "Automatische Vorteile",
          "Automatische Nachteile", "Automatischer Nachteile", "Automatischer Nachteil",
          "Verbrauchte AP für Vorteile/Nachteile", "Dringend empfohlene Vor- und Nachteile",
          "Typische Vorteile", "Typische Nachteile", "Untypische Vorteile", "Untypische Nachteile",
          "Anmerkung", "Publikation", "Publikation(en)"]


def number(text):
    return int(re.search(r"[–-]?\s*\d+", text).group(0).replace("–", "-").replace(" ", ""))


def first(f, *keys):
    return next((f[k] for k in keys if f.get(k)), None)


def optional(entry, key, value):
    if value and value.lower() != "keine":
        entry[key] = value


def main():
    entries = []
    for page, (sid, changes_text, changes) in SPECIES.items():
        lines = pagetext.article_lines(page)
        f = pagetext.labeled(lines, LABELS)
        norm = lambda s: re.sub(r"\s+", " ", s.replace("–", "-")).strip()
        if norm(f["Eigenschaftsänderungen"]) != norm(changes_text):
            raise SystemExit(f"{page}: Eigenschaftsänderungen im Wiki geändert: {f['Eigenschaftsänderungen']!r}")
        entry = {
            "id": "species." + sid,
            "name": lines[0],
            "ap": number(f["AP-Wert"]),
            "lifeBase": number(f["Lebensenergie-Grundwert"]),
            "spiritBase": number(f["Seelenkraft-Grundwert"].replace("\n", "")),
            "toughnessBase": number(f["Zähigkeit-Grundwert"].replace("\n", "")),
            "speedBase": number(f["Geschwindigkeit-Grundwert"]),
            "attributeChangesText": f["Eigenschaftsänderungen"],
            "attributeChanges": changes,
        }
        optional(entry, "automaticAdvantagesText",
                 first(f, "Automatischer Vorteil", "Automatischer Vorteile", "Automatische Vorteile"))
        optional(entry, "automaticDisadvantagesText",
                 first(f, "Automatische Nachteile", "Automatischer Nachteile", "Automatischer Nachteil"))
        optional(entry, "advantageApText", f.get("Verbrauchte AP für Vorteile/Nachteile"))
        for key, label in [("commonCulturesText", "Übliche Kulturen"),
                           ("recommendedText", "Dringend empfohlene Vor- und Nachteile"),
                           ("typicalAdvantagesText", "Typische Vorteile"),
                           ("typicalDisadvantagesText", "Typische Nachteile"),
                           ("untypicalAdvantagesText", "Untypische Vorteile"),
                           ("untypicalDisadvantagesText", "Untypische Nachteile"),
                           ("note", "Anmerkung")]:
            optional(entry, key, f.get(label))
        entry["publications"] = books.publications(pagetext.publication_text(lines))
        entries.append(entry)

    by_book = defaultdict(list)
    for e in entries:
        by_book[e["publications"][0]["book"]].append(e)
    folder = os.path.join(DATA, "species")
    os.makedirs(folder, exist_ok=True)
    for book, items in by_book.items():
        with open(os.path.join(folder, book + ".json"), "w", encoding="utf-8") as fh:
            json.dump(sorted(items, key=lambda e: e["name"]), fh, ensure_ascii=False, indent=2)
            fh.write("\n")
    print(f"species: {len(entries)} Einträge in {len(by_book)} Datei(en)")


if __name__ == "__main__":
    main()
