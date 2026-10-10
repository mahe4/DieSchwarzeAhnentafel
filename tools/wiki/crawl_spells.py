"""Sammelt Zauber, Rituale, Zaubertricks, magische Handlungen, Liturgien, Zeremonien und Segnungen (Meilenstein 6).

Alle Bereiche sind Auswahllisten mit Eintragsseiten `x.html?param=Name` (Feldpaare). Magische Handlungen ohne eigene
Liste (Bannzeichen, Goblinrituale) sind Artikelseiten und werden mit erfasst.
Ergebnis: .cache/_spell_pages.json mit {kind, page, param?, value?, label, group, category}.
"""
import json
import os

import crawl_magic_sf
import pagetext
import wiki

OUT = os.path.join(os.path.dirname(__file__), ".cache", "_spell_pages.json")
# Liste → (Gruppe in den Daten, Kategorie für die Anzeige)
LISTS = {
    "zauberauswahl.html": ("spell", "Zaubersprüche"),
    "ritualauswahl.html": ("ritual", "Rituale"),
    "zaubertrickauswahl.html": ("cantrip", "Zaubertricks"),
    "liturgieauswahl.html": ("liturgy", "Liturgien"),
    "zeremonieauswahl.html": ("ceremony", "Zeremonien"),
    "segenauswahl.html": ("blessing", "Segnungen"),
}


def crawl():
    found = []
    for page, (group, category) in LISTS.items():
        for target, param, value in crawl_magic_sf.list_entries(page):
            found.append({"kind": "fields", "page": target, "param": param, "value": value, "label": value,
                          "group": group, "category": category})
    # Magische Handlungen: eigene Listen je Art, wenige als Artikelseiten
    for url, label in pagetext.article_links("Z_MagHandlungen.html"):
        entries = crawl_magic_sf.list_entries(url)
        if entries:
            for target, param, value in entries:
                found.append({"kind": "fields", "page": target, "param": param, "value": value, "label": value,
                              "group": "magicalAction", "category": label})
            continue
        for sub, sublabel in pagetext.article_links(url):
            if sub.endswith(".html") and "?" not in sub and sub not in ("magie.html", "Z_MagHandlungen.html", url):
                try:
                    if any(l.startswith(("AP-Wert", "Steigerungsfaktor")) for l in pagetext.article_lines(sub)):
                        found.append({"kind": "article", "page": sub, "label": sublabel, "group": "magicalAction",
                                      "category": label})
                except Exception as ex:  # noqa: BLE001
                    print("FEHLER", sub, ex, flush=True)
    for e in found:
        if e["kind"] == "fields":
            wiki.fetch(e["page"], **{e["param"]: e["value"]})
    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(found, f, ensure_ascii=False, indent=1)
    print(len(found), "Einträge", flush=True)


if __name__ == "__main__":
    crawl()
