"""Sammelt Tiere aus dem Bestiarium und die tierischen Sonderfertigkeiten des Regel-Wikis (Tiergefährten).

Tiere: ab „Tiere“ (Best_Tiere.html) über die Sammelseiten („Hunde (Arten)“, „Vögel (Arten)“ › „Adler (Arten)“ …).
Eine Seite ist ein Tier, wenn sie einen Wertekasten hat (Zeilen „MU …“ und „LeP …“). Verwandlungsgestalten
(„Wolf (Wolfsgestalt)“) sind Werte für Zauber, keine Tiergefährten, und werden ausgelassen.
Fähigkeiten: Tricks, Ausbildungsaufsätze, Vertrautentricks, Tiersonderfertigkeiten, Kampfsonderfertigkeiten (Tiere),
Vor- und Nachteile (Tiere).
Ergebnis: .cache/_animal_pages.json mit {page, label, kind, group}.
"""
import json
import os
import re

import pagetext

OUT = os.path.join(os.path.dirname(__file__), ".cache", "_animal_pages.json")
ROOT = "Best_Tiere.html"
ABILITY_ROOTS = {
    "TSF_Tricks.html": ("trick", "Trick_"),
    "TSF_Ausbildungsaufsätze.html": ("training", "Aus_"),
    "VSF_Vertrautentricks.html": ("familiarTrick", ("VSF_", "SVF_")),
    "SF_Tiersonderfertigkeiten.html": ("specialAbility", "SF_"),
    "TSF_Kampfsonderfertigkeiten.html": ("combatSpecialAbility", "SF_"),
    "vor-und-nachteile/tiervor-und-nachteile/vorteile-tiere.html": ("advantage", "vor-und-nachteile/tiervor-und-nachteile/"),
    "vor-und-nachteile/tiervor-und-nachteile/nachteile-tiere.html": ("disadvantage", "vor-und-nachteile/tiervor-und-nachteile/"),
}
# Übersichtsseiten, auf die Einträge zurückverlinken
OVERVIEWS = {"VSF_Vertrautentricks.html", "TSF_Tricks.html", "TSF_Ausbildungsaufsätze.html",
             "TSF_Kampfsonderfertigkeiten.html", "SF_Tiersonderfertigkeiten.html", "SF_Tiere.html"}


STAT_KEYS = {"MU", "KL", "IN", "CH", "FF", "GE", "KO", "KK", "LeP", "AsP", "KaP", "INI", "VW", "AW", "PA", "SK", "ZK", "GS"}


def lines_of(page):
    """Textzeilen; steht ein Kürzel des Wertekastens allein in einer Zeile („MU“, „15“), wird der Wert angehängt."""
    result = []
    for line in pagetext.article_lines(page):
        if result and result[-1] in STAT_KEYS:
            result[-1] += " " + line
        else:
            result.append(line)
    return result


def is_animal(page):
    lines = lines_of(page)
    return any(re.match(r"MU -?\d", l) for l in lines) and any(l.startswith("LeP") for l in lines)


def group_name(label):
    """„Hunde (Arten)“ → „Hunde“."""
    return re.sub(r"\s*\(Arten\)$", "", label)


def crawl_animals():
    found, seen = [], {ROOT}

    def visit(page, label, group, depth):
        if page in seen or not page.endswith(".html") or "gestalt" in label.lower():
            return
        seen.add(page)
        try:
            animal = is_animal(page)
        except Exception as ex:  # noqa: BLE001
            print("FEHLER", page, ex, flush=True)
            return
        if animal:
            found.append({"page": page, "label": label, "kind": "animal", "group": group})
        elif depth < 3:
            # Gruppen nur aus Sammelseiten „… (Arten)“; andere Seiten (Khômgepard → Pardel) ändern sie nicht
            sub = group if not label.endswith("(Arten)") else \
                group_name(label) if group is None else f"{group} › {group_name(label)}"
            for url, text in pagetext.article_links(page):
                if url != ROOT:
                    visit(url, text, sub, depth + 1)

    for page, label in pagetext.article_links(ROOT):
        visit(page, label, None, 1)
    return found


def crawl_abilities():
    found, seen = [], set()
    for root, (kind, prefix) in ABILITY_ROOTS.items():
        for page, label in pagetext.article_links(root):
            if page in seen or page in OVERVIEWS or page == root or not page.startswith(prefix):
                continue
            seen.add(page)
            found.append({"page": page, "label": label, "kind": kind})
    return found


def main():
    animals = crawl_animals()
    abilities = crawl_abilities()
    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(animals + abilities, f, ensure_ascii=False, indent=1)
    print(len(animals), "Tiere,", len(abilities), "Fähigkeiten", flush=True)


if __name__ == "__main__":
    main()
