"""Entfernt Bücher aus books.json, auf die keine Datendatei verweist (außer grw, wdv, wiki),
und Einträge, deren Name nur eine Schreibvariante eines kanonischen Titels ist."""
import json
import os

import books

DATA = os.path.dirname(books.PATH)
KEEP = {"grw", "wdv", "wiki"}


def referenced() -> set[str]:
    ids = set()
    for root, _, files in os.walk(DATA):
        for name in files:
            if name.endswith(".json") and name != "books.json":
                text = open(os.path.join(root, name), encoding="utf-8").read()
                ids |= {p["book"] for e in json.loads(text) if isinstance(e, dict) for p in e.get("publications", [])} \
                    if text.lstrip().startswith("[") else {p["book"] for p in json.loads(text).get("publications", [])}
    return ids


if __name__ == "__main__":
    used = referenced()
    all_books = books.load()
    kept = [b for b in all_books if (b["id"] in used or b["id"] in KEEP) and books.CANONICAL.get(b["name"], b["name"]) == b["name"]]
    for b in all_books:
        if b not in kept:
            print("entfernt:", b["name"])
    books.save(kept)
