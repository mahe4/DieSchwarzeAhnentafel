"""Sammelt alle Professionsseiten ausgehend von professionen.html (Kategorien werden rekursiv durchsucht)."""
import json
import os

import pagetext
import wiki

ROOTS = ["Pro_Weltlich.html", "Pro_Zauberer.html", "Pro_Geweihte.html"]
OUT = os.path.join(os.path.dirname(__file__), ".cache", "_profession_pages.json")


def is_profession(page):
    return any(l.startswith("AP-Wert") for l in pagetext.article_lines(page))


def crawl():
    seen, professions, categories = set(ROOTS), [], {}
    queue = [(r, [r]) for r in ROOTS]
    while queue:
        page, path = queue.pop(0)
        for url, label in pagetext.article_links(page):
            if url in seen or not url.endswith(".html"):
                continue
            seen.add(url)
            try:
                if is_profession(url):
                    professions.append({"page": url, "label": label, "category": path})
                elif len(path) < 4:
                    queue.append((url, path + [url]))
            except Exception as ex:  # noqa: BLE001 - Seite fehlt o. Ä.
                print("FEHLER", url, ex)
    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(professions, f, ensure_ascii=False, indent=1)
    print(len(professions), "Professionsseiten")


if __name__ == "__main__":
    crawl()
