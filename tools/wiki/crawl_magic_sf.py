"""Sammelt magische und karmale Sonderfertigkeiten (Meilenstein 4c).

Zwei Seitenarten:
- Auswahllisten (…auswahl.html) mit Eintragsseiten `x.html?param=Name` (Feldpaare, `wiki.fields`),
- Artikelseiten (Paktgeschenke, Vampirische Gaben, Zauberzeichen …) mit „AP-Wert“ im Fließtext.
Ergebnis: .cache/_sf_magic_pages.json mit {kind, page, param?, value?, label, category, domain}.
"""
import json
import os
import re
from urllib.parse import unquote_plus

import pagetext
import wiki

OUT = os.path.join(os.path.dirname(__file__), ".cache", "_sf_magic_pages.json")
ROOTS = {"rgergegregre.html": "magisch", "karmale_SF.html": "karmal"}
SKIP = set(ROOTS) | {"sonderfertigkeiten.html", "frefre.html"}


def list_entries(page):
    """Eintragslinks einer Auswahlliste: [(Seite, Parameter, Wert)]."""
    h = wiki.fetch(page)
    found = []
    for target, param, value in re.findall(r"href=[\"']?([\w./-]+\.html)\?(\w+)=([^\"'>\s&]+)", h):
        if target.startswith("assets"):
            continue
        item = (target, param, unquote_plus(value))
        if item not in found:
            found.append(item)
    return found


def crawl():
    seen, found = set(SKIP), []
    queue = [(root, [], domain) for root, domain in ROOTS.items()]
    while queue:
        page, path, domain = queue.pop(0)
        for url, label in pagetext.article_links(page):
            if url in seen or not url.endswith(".html"):
                continue
            seen.add(url)
            try:
                entries = list_entries(url)
                if entries:
                    for target, param, value in entries:
                        found.append({"kind": "fields", "page": target, "param": param, "value": value,
                                      "label": value, "category": path + [label], "domain": domain})
                    continue
                lines = pagetext.article_lines(url)
            except Exception as ex:  # noqa: BLE001
                print("FEHLER", url, ex, flush=True)
                continue
            if any(l.startswith("AP-Wert") for l in lines):
                found.append({"kind": "article", "page": url, "label": label, "category": path, "domain": domain})
            elif len(path) < 4:
                queue.append((url, path + [label], domain))
    for e in found:
        if e["kind"] == "fields":
            wiki.fetch(e["page"], **{e["param"]: e["value"]})
    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(found, f, ensure_ascii=False, indent=1)
    print(len(found), "Einträge,", sum(e["kind"] == "article" for e in found), "davon Artikelseiten", flush=True)


if __name__ == "__main__":
    crawl()
