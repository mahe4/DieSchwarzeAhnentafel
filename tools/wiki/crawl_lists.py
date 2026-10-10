"""Gemeinsames Schreiben der Datendateien: eine Datei je Buch (erste Publikation des Eintrags)."""
import json
import os
from collections import defaultdict

import books

DATA = os.path.join(books.ROOT, "src", "DSACharacterCreator.Content", "data")


def write_by_book(category: str, entries: list[dict]) -> None:
    by_book = defaultdict(list)
    for e in entries:
        by_book[e["publications"][0]["book"]].append(e)
    folder = os.path.join(DATA, category)
    os.makedirs(folder, exist_ok=True)
    for old in os.listdir(folder):
        os.remove(os.path.join(folder, old))
    for book, items in by_book.items():
        with open(os.path.join(folder, book + ".json"), "w", encoding="utf-8") as fh:
            json.dump(sorted(items, key=lambda e: e["name"]), fh, ensure_ascii=False, indent=2)
            fh.write("\n")
    print(f"{category}: {len(entries)} Einträge in {len(by_book)} Datei(en)")
