"""Erzeugt data/items/*.json (sonstige Ausrüstung) und data/equipment-packages/*.json (Ausrüstungspakete) aus der
Rüstkammer des Regel-Wikis (Seitenliste aus crawl_items.py).

Gegenstand: Wertetabelle Gegenstand/Gewicht/Strukturpunkte/Preis/Komplexität (mehrere Zeilen = mehrere Gegenstände)
oder Textzeilen „Kosten:“/„Gewicht:“ (Waffenausrüstung). Regeltechnik und Anmerkung bleiben Wiki-Text.
Paket: Tabelle Ausrüstung/Gewicht/Preis mit Zeile „Gesamt“. Zeilen werden über den Namen mit Gegenständen, Waffen und
Rüstungen verknüpft; der Paketpreis ergibt sich aus den Zeilen, „Gesamt“ bleibt nur zum Vergleich.
"""
import glob
import json
import os
import re

import books
import crawl_lists
import import_equipment
import pagetext
import units
import wiki

PAGES = os.path.join(os.path.dirname(__file__), ".cache", "_item_pages.json")
DATA = os.path.join(books.ROOT, "src", "DSACharacterCreator.Content", "data")
LABELS = ["Regeltechnik", "Regel", "Anmerkung", "Kosten", "Preis", "Gewicht", "Waffen", "Publikation", "Publikation(en)",
          "Publikationen", "Errata", "Hinweis"]
EMPTY = ("", "-", "–", "keine", "keiner")


def tables(page):
    for t in re.findall(r"<table.*?</table>", wiki.fetch(page), flags=re.S):
        rows = [import_equipment.cells(r) for r in re.findall(r"<tr.*?</tr>", t, flags=re.S)]
        rows = [r for r in rows if any(r)]
        if rows:
            yield rows[0], [r for r in rows[1:] if len(r) == len(rows[0])]


def value(text):
    return None if text is None or text.strip() in EMPTY else text.strip()


def items(info):
    lines = pagetext.article_lines(info["page"])
    f = pagetext.labeled(lines, LABELS)
    publications = books.publications(pagetext.publication_text(lines))
    texts = {}
    for key, labels in [("ruleText", ("Regeltechnik", "Regel")), ("note", ("Anmerkung",))]:
        if v := next((f[l] for l in labels if value(f.get(l))), None):
            texts[key] = v.replace("\n", " ")
    found = []
    for head, rows in tables(info["page"]):
        if "Rüstungsschutz" in head or "TP" in head:
            return []  # Waffe oder Rüstung (eigener Import), z. B. über Rüstungsausstattung verlinkt
        if "Gewicht" not in head or "Preis" not in head:
            continue
        for row in rows:
            col = dict(zip(head, row))
            name = row[0]
            e = {"id": "item." + wiki.slug(name), "name": name, "category": info["category"]}
            for key, label in [("weight", "Gewicht"), ("price", "Preis"), ("structurePoints", "Strukturpunkte"),
                               ("complexity", "Komplexität")]:
                if v := value(col.get(label)):
                    e[key] = v
            found.append(e)
        break
    if not found:  # Waffenausrüstung: „Kosten: 5 Silbertaler“
        e = {"id": "item." + wiki.slug(info["label"]), "name": info["label"], "category": info["category"]}
        if v := value(f.get("Kosten") or f.get("Preis")):
            e["price"] = v.split("\n")[0]
        if v := value(f.get("Gewicht")):
            e["weight"] = v.split("\n")[0]
        found.append(e)
    return [units.numbers(e) | texts | {"publications": publications} for e in found]


def known_names():
    """Name → Eintrag aller Waffen und Rüstungen (bereits importiert)."""
    result = {}
    for folder in ("weapons", "armor"):
        for path in glob.glob(os.path.join(DATA, folder, "*.json")):
            for e in json.load(open(path, encoding="utf-8")):
                result.setdefault(e["name"].lower(), e)
    return result


def package(info, names):
    for head, rows in tables(info["page"]):
        if head[:3] != ["Ausrüstung", "Gewicht", "Preis"]:
            continue
        total = next((r for r in rows if r[0] == "Gesamt"), None)
        lines = []
        for row in rows:
            if row[0] == "Gesamt":
                continue
            name = row[0]
            line = {"name": name}
            # „Kleidung (normal)“, „Kletterseil, 10 Schritt“: auch ohne Zusatz suchen
            base = re.sub(r"\s*[(,].*$", "", name).lower()
            ref = names.get(name.lower()) or names.get(base)
            if ref:
                line["ref"] = ref["id"]
            # „–“ in der Paketliste: Wert des verknüpften Gegenstands (z. B. Waffenpflegeset 1 S)
            w = units.weight(value(row[1])) if value(row[1]) else (ref or {}).get("weightStone")
            p = units.price(value(row[2])) if value(row[2]) else (ref or {}).get("priceSilver")
            if w is not None:
                line["weightStone"] = units.tidy(w)
            if p is not None:
                line["priceSilver"] = units.tidy(p)
            lines.append(line)
        lines_text = pagetext.article_lines(info["page"])
        e = {"id": "package." + wiki.slug(info["label"]), "name": info["label"], "items": lines}
        if total:
            e["weightText"], e["priceText"] = value(total[1]), value(total[2])
            if (p := units.price(total[2])) is not None:
                e["wikiPrice"] = units.tidy(p)
        e["publications"] = books.publications(pagetext.publication_text(lines_text))
        return e
    print("  keine Pakettabelle:", info["page"])
    return None


def main():
    pages = json.load(open(PAGES, encoding="utf-8"))
    entries = []
    for info in pages:
        if info["kind"] == "item":
            entries += items(info)
    seen = {}
    for e in entries:
        if e["id"] in seen:
            same = seen[e["id"]]
            if same["name"] == e["name"] and same.get("price") == e.get("price"):
                e["duplicate"] = True  # gleicher Gegenstand auf zwei Sammelseiten
                continue
            e["id"] += "-" + wiki.slug(e["category"])
        seen[e["id"]] = e
    entries = [e for e in entries if not e.pop("duplicate", False)]
    crawl_lists.write_by_book("items", entries)

    names = known_names()
    for e in entries:
        names.setdefault(e["name"].lower(), e)
    packages = [p for info in pages if info["kind"] == "package" if (p := package(info, names))]
    crawl_lists.write_by_book("equipment-packages", packages)
    print("  ohne Preis:", sum(1 for e in entries if "priceSilver" not in e),
          "| ohne Gewicht:", sum(1 for e in entries if "weightStone" not in e),
          "| Paketzeilen ohne Verweis:", sum(1 for p in packages for l in p["items"] if "ref" not in l))


if __name__ == "__main__":
    main()
