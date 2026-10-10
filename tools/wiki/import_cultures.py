"""Erzeugt data/cultures/*.json aus dem Regel-Wiki (Kulturseiten inkl. Kulturpaket)."""
import json
import os
import re
from collections import defaultdict

import books
import crawl_lists
import pagetext
import skillnames
import wiki

LABELS = ["Sprache", "Schrift", "Ortskenntnis", "Ortkenntnis", "Sozialstatus", "Übliche Professionen",
          "Übliche Profession", "Typische Vorteile", "Typische Nachteile", "Untypische Vorteile",
          "Untypische Nachteile", "Typische Talente", "Untypische Talente", "Typische Namen",
          "Kulturpaket", "Publikation", "Publikation(en)", "Errata"]
GROUPS = {
    "Kul_Achaz.html": "Achaz-Kulturen", "Kul_Elfen.html": "Elfische Kulturen",
    "kulturen/goblin-kulturen.html": "Goblin-Kulturen", "Kul_Menschen.html": "Menschliche Kulturen",
    "kulturen/nachtalb-kulturen.html": "Nachtalb-Kulturen", "Kul_Orks.html": "Orkische Kulturen",
    "Kul_SpezielleKulturen.html": "Spezielle Kulturen", "Kul_Zwerge.html": "Zwergische Kulturen",
}


def optional(entry, key, value):
    if value and value.strip().lower() not in ("keine", "-", "–"):
        entry[key] = value.strip()


def main():
    entries, problems = [], []
    for group_page, group in GROUPS.items():
        for url, label in pagetext.article_links(group_page):
            if not url.endswith(".html") or url in GROUPS or url == "kulturen.html":
                continue
            lines = pagetext.article_lines(url)
            if not lines:
                problems.append(f"{url}: Seite im Wiki leer")
                continue
            # „Kulturpaket Mittelreich (12 AP)“ steht ohne Doppelpunkt im Wiki.
            lines = [("Kulturpaket: " + l[len("Kulturpaket"):].strip()) if re.match(r"Kulturpaket\b.*\(\s*\d+\s*AP\)", l) else l
                     for l in lines]
            f = pagetext.labeled(lines, LABELS)
            entry = {"id": "culture." + wiki.slug(lines[0]), "name": lines[0], "group": group}
            for key, lab in [("languageText", "Sprache"), ("scriptText", "Schrift"),
                             ("localKnowledgeText", f.get("Ortskenntnis") and "Ortskenntnis" or "Ortkenntnis"),
                             ("socialStatusText", "Sozialstatus"),
                             ("commonProfessionsText", f.get("Übliche Professionen") is not None
                              and "Übliche Professionen" or "Übliche Profession"),
                             ("typicalAdvantagesText", "Typische Vorteile"),
                             ("typicalDisadvantagesText", "Typische Nachteile"),
                             ("untypicalAdvantagesText", "Untypische Vorteile"),
                             ("untypicalDisadvantagesText", "Untypische Nachteile"),
                             ("typicalSkillsText", "Typische Talente"),
                             ("untypicalSkillsText", "Untypische Talente"),
                             ("namesText", "Typische Namen")]:
                optional(entry, key, f.get(lab))
            package = f.get("Kulturpaket")
            if package:
                head, _, body = package.partition("\n")
                m = re.match(r"(.*?)\s*\((\d+)\s*AP\)", head)
                unparsed = []
                entry["package"] = {
                    "name": "Kulturpaket " + m.group(1).strip(),
                    # AP-Wert laut Wiki – nur zum Vergleich; die App berechnet die Kosten aus den Talenten.
                    "wikiAp": int(m.group(2)),
                    "skills": [{"skill": v["id"], "value": v["value"]}
                               for v in skillnames.value_list(body.replace("\n", ", "), skillnames.skill_id, unparsed)],
                }
                if unparsed:
                    problems.append(f"{url}: nicht zugeordnet: {unparsed}")
            else:
                problems.append(f"{url}: kein Kulturpaket")
            entry["publications"] = books.publications(pagetext.publication_text(lines))
            entries.append(entry)

    ids = defaultdict(int)
    for e in entries:
        ids[e["id"]] += 1
    dupes = [i for i, n in ids.items() if n > 1]
    if dupes:
        raise SystemExit(f"Doppelte IDs: {dupes}")
    crawl_lists.write_by_book("cultures", entries)
    for p in problems:
        print("HINWEIS", p)


if __name__ == "__main__":
    main()
