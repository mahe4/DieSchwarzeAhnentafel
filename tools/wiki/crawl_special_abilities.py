"""Sammelt alle profanen Sonderfertigkeiten (Artikelseiten mit „AP-Wert“) ausgehend von frefre.html."""
import json
import os

import pagetext

ROOT = "frefre.html"
OUT = os.path.join(os.path.dirname(__file__), ".cache", "_sf_profane_pages.json")
SKIP = {"frefre.html", "sonderfertigkeiten.html"}


def is_entry(lines):
    return any(l.startswith("AP-Wert") for l in lines)


def crawl():
    seen, found = {ROOT}, []
    queue = [(ROOT, [])]
    while queue:
        page, path = queue.pop(0)
        for url, label in pagetext.article_links(page):
            if url in seen or url in SKIP or not url.endswith(".html") or "?" in url:
                continue
            seen.add(url)
            try:
                lines = pagetext.article_lines(url)
            except Exception as ex:  # noqa: BLE001
                print("FEHLER", url, ex, flush=True)
                continue
            if is_entry(lines):
                found.append({"page": url, "label": label, "category": path})
            elif len(path) < 4:
                queue.append((url, path + [label]))
    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(found, f, ensure_ascii=False, indent=1)
    print(len(found), "SF-Seiten", flush=True)


if __name__ == "__main__":
    crawl()
