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
COSTS = json.load(open(os.path.join(DATA, "rules", "improvement-costs.json")))
ROWS = COSTS["rows"]
SPELL = {e["id"]: e for e in load("spells")}


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


SF = {e["id"]: e for e in load("special-abilities")}


def sf_cost(p):
    """Kosten der Sonderfertigkeiten der Vorlage; None, wenn nicht berechenbar."""
    total, seen = 0, []
    for t in p.get("specialAbilities", []):
        c, lvl = SF[t["id"]]["cost"], t.get("level") or 1
        n = 1 + sum(1 for x in seen if x["id"] == t["id"] and x.get("option") == t.get("option"))
        seen.append(t)
        if c["type"] == "fixed":
            total += c["ap"]
        elif c["type"] == "perLevel":
            total += c["ap"] * lvl
        elif c["type"] == "levels":
            total += c["levels"][lvl - 1]
        elif c["type"] == "byOption" and t.get("option"):
            option = next(o for o in c["options"] if o["name"] == t["option"])
            total += option["ap"] + 2 * option.get("permanentAstral", 0)
        elif c["type"] == "byOccurrence":
            total += c["levels"][min(n, len(c["levels"])) - 1]
        elif c["type"] == "bySkillFactor" and c.get("target") == "skill" and t.get("option"):
            total += c["byFactor"][SKILL[t["option"]]["improvementCost"]] * (n if c.get("escalating") else 1)
        else:
            return None
        # Traditionsartefakte der Vorlage sind gebunden, die permanenten AsP zurückgekauft (2 AP je Punkt)
        total += 2 * SF[t["id"]].get("permanentAstralCost", 0)
    return total


def spell_cost(p):
    """Zauber/Liturgien der Vorlage: Aktivierung + Steigerung; Zaubertricks/Segnungen 1 AP; je Auswahl 1 AP; Flüche-Budget."""
    total = 0
    for t in p.get("spells", []):
        s = SPELL[t["id"]]
        if "improvementCost" in s or "factorOptions" in s:
            f = s.get("improvementCost") or min(o["factor"] for o in s["factorOptions"])
            total += COSTS["activation"][f] + cost(f, 0, t.get("value", 0))
        else:
            total += s.get("ap", 1)
    for c in p.get("spellChoices", []):  # Zaubertricks 1 AP, sonst günstigste Option zum Wert der Vorlage
        if "value" in c:
            total += c["count"] * min(COSTS["activation"][SPELL[o]["improvementCost"]] +
                                      cost(SPELL[o]["improvementCost"], 0, c["value"]) for o in c["options"])
        else:
            total += c["count"]
    return total + p.get("curseAp", 0)


def complete(p):
    """Ohne nicht zugeordnete Teile ist der Inhalt vollständig strukturiert."""
    return not (p.get("unparsedSpells") or p.get("unparsedSpecialAbilities") or p.get("unparsed"))


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
        extra = sf_cost(p)
        if extra is None:
            continue
        extra += p.get("languageAp", 0) + spell_cost(p)
        base = skills_cost(base_skills) + combat_cost(combat_values(p, None)) + extra
        rest = p["wikiAp"] - base if "wikiAp" in p else None
        if rest is not None and (rest < 0 or (rest > 0 and complete(p))):
            rows.append(("Profession", p["name"], "", p["wikiAp"], base,
                         "Inhalt" if complete(p) else "allein der strukturierte Inhalt"))
        if complete(p) and rest is not None:
            rest = 0
        for v in p.get("variants", []):
            skills = dict(base_skills) | {x["skill"]: x["value"] for x in v["skills"]}
            computed = skills_cost(skills) + combat_cost(combat_values(p, v)) + extra
            if "text" in v or rest is None:
                if v["wikiAp"] < computed:
                    rows.append(("Variante", p["name"], v["name"], v["wikiAp"], computed, "allein der strukturierte Inhalt"))
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
        "Geprüft wird: Talente + Kampftechniken + Sonderfertigkeiten + Zauber/Liturgien der Vorlage (Aktivierung und",
        "Steigerung, Zaubertricks/Segnungen 1 AP) + Sprachen-/Schriften- und Flüche-Budget. Bei Professionen mit nicht",
        "zugeordneten Teilen erscheint nur, wenn schon der strukturierte Inhalt mehr kostet als der Wiki-Wert.",
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
