"""Prüft Kultur- und Professionspakete gegen die Kosten ihres Inhalts und schreibt docs/WIKI_ABWEICHUNGEN.md.

Grundsatz: Ein Paket kostet genau so viel wie sein Inhalt. Weicht der AP-Wert im Wiki ab, ist das ein Fehler im Wiki;
die App rechnet mit den tatsächlichen Kosten (siehe Core/Rules/PackageCosts.cs – gleiche Logik).
"""
import glob
import json
import os

import books

DATA = os.path.join(books.ROOT, "src", "DSACharacterCreator.Content", "data")
OUT = os.path.join(books.ROOT, "docs", "WIKI_ABWEICHUNGEN.md")


def load(folder):
    return [e for f in sorted(glob.glob(os.path.join(DATA, folder, "*.json"))) for e in json.load(open(f, encoding="utf-8"))]


SKILL = {e["id"]: e for e in load("skills")}
COMBAT = {e["id"]: e for e in load("combat-techniques")}
ROWS = json.load(open(os.path.join(DATA, "rules", "improvement-costs.json")))["rows"]


def step(col, value):
    return next(r[col] for r in ROWS if value <= r["upTo"])


def cost(col, frm, to):
    return sum(step(col, v) for v in range(frm + 1, to + 1)) if to >= frm else -cost(col, to, frm)


def skills_cost(values):
    return sum(cost(SKILL[k]["improvementCost"], 0, v) for k, v in values.items())


def combat_values(p, variant):
    result = {}
    for v in p["combatTechniques"]:
        result[v["combatTechnique"]] = max(result.get(v["combatTechnique"], 0), v["value"])
    for ch in p.get("combatTechniqueChoices", []):  # günstigste Option als Kostenbasis
        pick = min(ch["options"], key=lambda o: cost(COMBAT[o]["improvementCost"], 6, ch["value"]))
        result[pick] = max(result.get(pick, 0), ch["value"])
    for v in (variant or {}).get("combatTechniques", []):
        result[v["combatTechnique"]] = v["value"]
    return result


def combat_cost(values):
    return sum(cost(COMBAT[k]["improvementCost"], 6, v) for k, v in values.items())


def main():
    rows = []
    for c in load("cultures"):
        p = c.get("package")
        if p:
            computed = skills_cost({v["skill"]: v["value"] for v in p["skills"]})
            if computed != p["wikiAp"]:
                rows.append(("Kultur", c["name"], p["name"], p["wikiAp"], computed, "Talente des Pakets"))
    for p in load("professions"):
        base_skills = {v["skill"]: v["value"] for v in p["skills"]}
        base = skills_cost(base_skills) + combat_cost(combat_values(p, None))
        rest = p["wikiAp"] - base if "wikiAp" in p else None
        if rest is not None and rest < 0:
            rows.append(("Profession", p["name"], "", p["wikiAp"], base, "allein Talente und Kampftechniken"))
        for v in p.get("variants", []):
            skills = dict(base_skills) | {x["skill"]: x["value"] for x in v["skills"]}
            computed = skills_cost(skills) + combat_cost(combat_values(p, v))
            if "text" in v or rest is None:
                if v["wikiAp"] < computed:
                    rows.append(("Variante", p["name"], v["name"], v["wikiAp"], computed, "allein Talente und Kampftechniken"))
            elif computed + max(0, rest) != v["wikiAp"]:  # noqa: E501
                rows.append(("Variante", p["name"], v["name"], v["wikiAp"], computed + max(0, rest), "Paket + Änderungen"))

    lines = [
        "# Abweichungen im Regel-Wiki",
        "",
        "Erzeugt mit `python3 tools/wiki/check_packages.py`. Nicht von Hand bearbeiten.",
        "",
        "Ein Paket kostet genau so viel wie sein Inhalt. Wo der AP-Wert im Wiki davon abweicht, liegt vermutlich ein",
        "Fehler im Wiki vor. Die App rechnet immer mit den berechneten Kosten und zeigt beim Helden einen Hinweis an.",
        "",
        "Professionen enthalten außerdem Sonderfertigkeiten, Sprachen, Zauber und Liturgien, die noch nicht strukturiert",
        "erfasst sind. Deren Anteil wird bis Meilenstein 4/6 als Rest „Wiki-AP minus Talente/Kampftechniken“ angesetzt;",
        "geprüft werden können daher nur Varianten, die ausschließlich Talente/Kampftechniken ändern, und Pakete, deren",
        "Talente/Kampftechniken allein schon mehr kosten als der Wiki-Wert.",
        "",
        f"**{len(rows)} Abweichungen**",
        "",
        "| Art | Eintrag | Paket/Variante | Wiki-AP | berechnet | Grundlage |",
        "|---|---|---|---:|---:|---|",
    ]
    lines += [f"| {a} | {n} | {v} | {w} | {c} | {g} |" for a, n, v, w, c, g in rows]
    with open(OUT, "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")
    print(f"{len(rows)} Abweichungen → {os.path.relpath(OUT, books.ROOT)}")


if __name__ == "__main__":
    main()
