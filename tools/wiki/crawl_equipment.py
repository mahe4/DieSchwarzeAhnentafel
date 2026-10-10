"""Sammelt Waffen und Rüstungen aus der Rüstkammer des Regel-Wikis (Meilenstein 7a).

Je Kategorie (Schwerter, Bögen, Kettenrüstung …) gibt es eine Liste „Reguläre …“ mit Artikelseiten und eine Liste
„Seltene & Einmalige …“ (benannte Einzelstücke, vorerst nicht übernommen). Manche Kategorien verlinken ihre Einträge
direkt. Ein Eintrag ist eine Artikelseite mit Wertetabelle (TP bzw. Rüstungsschutz).
Ergebnis: .cache/_equipment_pages.json mit {page, label, kind, category}.
"""
import json
import os
import re

import pagetext
import wiki

OUT = os.path.join(os.path.dirname(__file__), ".cache", "_equipment_pages.json")
ROOTS = {"RS_Waffen.html": "weapon", "RS_Ruestung.html": "armor"}
# Keine eigene Waffengattung, sondern eine Abwandlung (Holzversionen bestehender Waffen)
SKIP_CATEGORIES = {"Holzwaffen"}


def is_entry(page):
    h = wiki.fetch(page)
    tables = re.findall(r"<table.*?</table>", h, flags=re.S)
    return any(re.search(r">\s*(TP|Rüstungsschutz)\s*<", t) for t in tables)


def crawl():
    found, seen = [], set(ROOTS)
    for root, kind in ROOTS.items():
        for category_page, category in pagetext.article_links(root):
            if category in SKIP_CATEGORIES or category_page in seen:
                continue
            seen.add(category_page)
            # Die Kategorieseite selbst hat bei Rüstungen eine Wertezeile (Grundrüstung der Rüstungstabelle)
            if is_entry(category_page):
                found.append({"page": category_page, "label": category, "kind": kind, "category": category})
            links = [(u, l) for u, l in pagetext.article_links(category_page) if u not in ROOTS and u.endswith(".html")]
            regular = [(u, l) for u, l in links if l.startswith("Reguläre")]
            sources = []
            for list_page, _ in regular:
                sources += [(u, l) for u, l in pagetext.article_links(list_page) if u.endswith(".html")]
            if not regular:  # Einträge direkt auf der Kategorieseite
                sources = [(u, l) for u, l in links if not l.startswith("Seltene")]
            for page, label in sources:
                if page in seen or page == category_page:
                    continue
                seen.add(page)
                try:
                    if is_entry(page):
                        found.append({"page": page, "label": label, "kind": kind, "category": category})
                except Exception as ex:  # noqa: BLE001
                    print("FEHLER", page, ex, flush=True)
    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(found, f, ensure_ascii=False, indent=1)
    print(len(found), "Einträge,", sum(e["kind"] == "weapon" for e in found), "Waffen", flush=True)


if __name__ == "__main__":
    crawl()
