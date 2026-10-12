"""Prüft Kultur- und Professionspakete gegen die Kosten ihres Inhalts und schreibt docs/WIKI_ABWEICHUNGEN.md.

Grundsatz: Ein Paket kostet genau so viel wie sein Inhalt. Weicht der AP-Wert im Wiki ab, ist das ein Fehler im Wiki;
die App rechnet mit den tatsächlichen Kosten (siehe Core/Rules/PackageCosts.cs – gleiche Logik).

Dazu weitere Plausibilitätsprüfungen der importierten Daten (`other_findings`): Dort steht, was im Wiki so steht, aber
vermutlich falsch ist. Die Werte bleiben wie im Wiki; korrigiert werden im Import nur eindeutige Tippfehler in Namen
(Tabellen am Ende der Datei).
"""
import glob
import re
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
        elif c["type"] == "levels":  # jede Stufe einzeln erworben: Stufe II kostet Stufe I + II
            total += sum(c["levels"][min(i, len(c["levels"]) - 1)] for i in range(lvl))
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
    lines += equipment_packages()
    lines += other_findings()
    lines += corrected_typos()
    with open(OUT, "w", encoding="utf-8") as f:
        f.write("\n".join(lines) + "\n")
    print(f"{len(rows)} Abweichungen → {os.path.relpath(OUT, books.ROOT)}")


def equipment_packages():
    """Ausrüstungspakete: Preis und Gewicht der Zeilen gegen die Zeile „Gesamt“ im Wiki."""
    import units
    rows = []
    for p in load("equipment-packages"):
        price = round(sum(l.get("priceSilver", 0) for l in p["items"]), 3)
        weight = round(sum(l.get("weightStone", 0) for l in p["items"]), 3)
        if p.get("wikiPrice") is not None and p["wikiPrice"] != price:
            rows.append((p["name"], "Preis", p["priceText"], f"{price:g} S".replace(".", ",")))
        if (w := units.weight(p.get("weightText"))) is not None and round(w, 3) != weight:
            rows.append((p["name"], "Gewicht", p["weightText"], f"{weight:g} Stein".replace(".", ",")))
    return ["", "## Ausrüstungspakete", "",
            "Preis und Gewicht eines Pakets ergeben sich aus seinen Zeilen (Zeilen mit „–“ übernehmen den Wert des",
            "gleichnamigen Gegenstands). Die App rechnet mit der Summe.", "",
            f"**{len(rows)} Abweichungen**", "",
            "| Paket | Wert | Wiki „Gesamt“ | Summe der Zeilen |", "|---|---|---|---|"] + \
        [f"| {n} | {k} | {w} | {c} |" for n, k, w, c in rows]


FACTOR = {"A": 1, "B": 2, "C": 3, "D": 4}
FW_STEP = {8: 1, 10: 1, 12: 2, 14: 2, 16: 3}


def _base(name):
    return re.sub(r"\s*\(.*|\s+[IV]+$|\s*….*", "", name).strip()


def other_findings():
    """Werte, die im Wiki so stehen, aber nicht zusammenpassen. Jede Zeile: (Art, Eintrag, Feld, Wiki-Text, Hinweis)."""
    import difflib
    rows = []
    # Zauber-/Liturgieerweiterungen: AP = Steigerungsfaktor (A=1 … D=4) × 1/2/3 für FW 8–10/12–14/16
    for e in load("spells"):
        fac = e.get("improvementCost")
        for x in e.get("enhancements", []):
            if fac and FW_STEP.get(x["minValue"]) and x["ap"] != FACTOR[fac] * FW_STEP[x["minValue"]]:
                rows.append(("Erweiterung", e["name"], x["name"], f"FW {x['minValue']}, {x['ap']} AP",
                             f"erwartet {FACTOR[fac] * FW_STEP[x['minValue']]} AP (Faktor {fac})"))
        if (c := e.get("check")) and len(set(c)) == 1:
            rows.append(("Probe", e["name"], "Probe", e["checkText"], "dreimal dieselbe Eigenschaft"))
        for line in e.get("detailsText", "").split("\n"):
            if re.fullmatch(r"Wirkungsdauer: \d+", line):
                rows.append(("Zauber/Liturgie", e["name"], "Wirkungsdauer", line.split(": ")[1], "Einheit fehlt"))
    # Stufen und Kosten
    traits = [(k, e) for k, f in (("Vorteil", "advantages"), ("Nachteil", "disadvantages"),
                                  ("Sonderfertigkeit", "special-abilities")) for e in load(f)]
    for kind, e in traits:
        text, cost = e["apValueText"].replace("\n", " "), e["cost"]
        if e.get("maxLevel") and cost["type"] == "fixed" and not e.get("openLevels"):
            rows.append((kind, e["name"], "AP-Wert", text, f"{e['maxLevel']} Stufen, aber nur ein AP-Wert (je Stufe?)"))
        if not e.get("maxLevel") and cost["type"] == "fixed" and re.search(r"pro Stufe", text):
            rows.append((kind, e["name"], "AP-Wert", text, "„pro Stufe“, aber keine Stufen im Namen"))
        if kind == "Nachteil" and re.match(r"\d", text):
            rows.append((kind, e["name"], "AP-Wert", text, "Minuszeichen fehlt (die App rechnet negativ)"))
    # Voraussetzungen, die einem bekannten Namen fast gleichen
    names = {e["name"] for _, e in traits}
    for kind, e in traits:
        for u in e.get("unparsedPrerequisites", []):
            u2 = re.sub(r"^(?:SF|Sonderfertigkeit|Vorteil|Nachteil)\s+", "", u)
            m = difflib.get_close_matches(u2, names, n=1, cutoff=0.78)
            # „Meteoreisen verarbeiten 18“ (Name + Wert) und „Ein heldenhafter Name“ sind keine Tippfehler
            if m and not re.fullmatch(re.escape(m[0]) + r"(?:\s+(?:\d+|[IV]+))?", u2, flags=re.I) \
                    and not u2.lower().endswith(m[0].lower()) and _base(u2) != _base(m[0]):
                rows.append((kind, e["name"], "Voraussetzung", u, f"gemeint vermutlich „{m[0]}“"))
    # Vor-/Nachteile in der falschen Liste
    adv = {_base(e["name"]) for k, e in traits if k == "Vorteil"}
    dis = {_base(e["name"]) for k, e in traits if k == "Nachteil"}
    lists = [("Profession", "professions", {"recommendedAdvantagesText": "Empfohlene Vorteile",
                                            "unsuitableAdvantagesText": "Ungeeignete Vorteile",
                                            "recommendedDisadvantagesText": "Empfohlene Nachteile",
                                            "unsuitableDisadvantagesText": "Ungeeignete Nachteile"}),
             ("Kultur", "cultures", {"typicalAdvantagesText": "Typische Vorteile",
                                     "untypicalAdvantagesText": "Untypische Vorteile",
                                     "typicalDisadvantagesText": "Typische Nachteile",
                                     "untypicalDisadvantagesText": "Untypische Nachteile"})]
    for kind, folder, fields in lists:
        for e in load(folder):
            for key, label in fields.items():
                wrong, right = (dis, adv) if "Advantages" in key else (adv, dis)
                for item in re.split(r",(?![^(]*\))|\n", e.get(key, "")):
                    if (b := _base(item.strip())) in wrong and b not in right:
                        rows.append((kind, e["name"], label, item.strip(),
                                     "ist ein " + ("Nachteil" if "Advantages" in key else "Vorteil")))
    # Professionen: Kampftechnik auf dem Grundwert 6
    for e in load("professions"):
        for c in e.get("combatTechniques", []):
            if c["value"] <= 6:
                rows.append(("Profession", e["name"], "Kampftechniken", f"{COMBAT[c['combatTechnique']]['name']} {c['value']}",
                             "nicht über dem Grundwert 6"))
    # Preise ohne Einheit
    for kind, folder in (("Waffe", "weapons"), ("Rüstung", "armor"), ("Gegenstand", "items")):
        for e in load(folder):
            if re.fullmatch(r"\d+(?:[.,]\d+)?", e.get("price", "").strip()):
                rows.append((kind, e["name"], "Preis", e["price"], "Einheit fehlt (S?)"))
    return ["", "## Weitere Auffälligkeiten", "",
            "Werte, die so im Wiki stehen, aber nicht zueinander passen. Die App übernimmt sie unverändert.", "",
            f"**{len(rows)} Auffälligkeiten**", "",
            "| Art | Eintrag | Feld | Wiki | Hinweis |", "|---|---|---|---|---|"] + \
        [f"| {a} | {n} | {k} | {w} | {h} |" for a, n, k, w, h in rows]


def corrected_typos():
    """Eindeutige Tippfehler in Namen, die der Import korrigiert (Tabellen in den Import-Skripten)."""
    import import_advantages
    import import_professions
    import import_species
    import skillnames
    pairs = [("Voraussetzungen (import_advantages.PREREQUISITE_TYPOS)", import_advantages.PREREQUISITE_TYPOS),
             ("Zauber in Professionen (import_professions.SPELL_TYPOS)", list(import_professions.SPELL_TYPOS.items())),
             ("Zauberlisten in Professionen (import_professions.SPELL_TEXT_FIXES)", import_professions.SPELL_TEXT_FIXES),
             ("Spezies (import_species.TRAIT_ALIASES)", list(import_species.TRAIT_ALIASES.items())),
             ("Talente/Kampftechniken (skillnames.ALIASES)", list(skillnames.ALIASES.items()))]
    lines = ["", "## Im Import korrigierte Schreibweisen", "",
             "Tippfehler in Namen, die sonst auf keinen Eintrag passen. Die Wiki-Texte (`prerequisitesText` usw.) bleiben",
             "unverändert; korrigiert wird nur die Zuordnung.", ""]
    for title, items in pairs:
        lines += [f"**{title}:** " + "; ".join(f"„{w.strip()}“ → „{r.strip()}“" for w, r in items), ""]
    return lines[:-1]


if __name__ == "__main__":
    main()
