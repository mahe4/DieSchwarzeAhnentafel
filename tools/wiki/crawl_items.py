"""Sammelt sonstige Ausrüstung und Ausrüstungspakete aus der Rüstkammer des Regel-Wikis (Meilenstein 7b).

Ein Gegenstand ist eine Seite mit Wertetabelle (Gewicht, Preis) oder mit „Kosten:“-Zeile (Waffenausrüstung).
Sammelseiten wie „Kleidung (Arten & Einzelstücke)“ oder „Lichtquellen“ verlinken weitere Gegenstände; ihr Titel wird
zur Kategorie. Pakete stehen unter „Ausrüstungspakete“.
Ergebnis: .cache/_item_pages.json mit {page, label, kind, category}.
"""
import json
import os
import re

import pagetext
import wiki

OUT = os.path.join(os.path.dirname(__file__), ".cache", "_item_pages.json")
ROOTS = {"RK_Ausruestung.html": "Ausrüstung", "RS_Waffenausruestung.html": "Waffenausrüstung",
         "RS_Ruestungsausstattung.html": "Rüstungsausstattung"}
PACKAGES = "ruestkammer/ausruestungspakete.html"
# Bereiche der Rüstkammer, die keine Gegenstände sind oder eigene Importe haben
FOREIGN = ("RS_Waffen", "RS_Ruestung.html", "RS_Helme", "ruestkammer.html", "RK_Artefakte")


def item_table(page):
    for t in re.findall(r"<table.*?</table>", wiki.fetch(page), flags=re.S):
        head = re.search(r"<tr.*?</tr>", t, flags=re.S)
        if head and re.search(r">\s*Gewicht\s*<", head.group(0)) and re.search(r">\s*Preis\s*<", head.group(0)):
            return True
    return False


def is_item(page):
    return item_table(page) or any(re.match(r"(Kosten|Preis)\s*:", l) for l in pagetext.article_lines(page))


def group_name(label):
    """„Kleidung (Arten & Einzelstücke)“ → „Kleidung“, „Lichtquellen (profan, magisch, karmal)“ → „Lichtquellen“."""
    return re.sub(r"\s*\((?:Arten|profan|Werkzeuge|Instrumente|Labore)[^)]*\)$", "", label)


def crawl():
    found, seen = [], set(ROOTS) | {PACKAGES}

    def visit(page, label, category, depth):
        if page in seen or page.startswith(FOREIGN) or not page.endswith(".html"):
            return
        seen.add(page)
        try:
            item = is_item(page)
        except Exception as ex:  # noqa: BLE001
            print("FEHLER", page, ex, flush=True)
            return
        if item:
            found.append({"page": page, "label": label, "kind": "item", "category": category})
        if depth < 2:
            # Sammelseite (oder Gegenstand mit Unterseiten wie Proviant): verlinkte Gegenstände
            sub = category if item else group_name(label)
            for url, text in pagetext.article_links(page):
                if url not in ROOTS:
                    visit(url, text, sub, depth + 1)

    for root, category in ROOTS.items():
        for page, label in pagetext.article_links(root):
            visit(page, label, category, 1)
    for page, label in pagetext.article_links(PACKAGES):
        if page not in seen and page.startswith("ruestkammer/ausruestungspakete/"):
            seen.add(page)
            found.append({"page": page, "label": label, "kind": "package", "category": "Ausrüstungspakete"})
    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(found, f, ensure_ascii=False, indent=1)
    print(len(found), "Einträge,", sum(e["kind"] == "package" for e in found), "Pakete", flush=True)


if __name__ == "__main__":
    crawl()
