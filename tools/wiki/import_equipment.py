"""Erzeugt data/weapons/*.json und data/armor/*.json aus der Rüstkammer des Regel-Wikis (Seitenliste aus
crawl_equipment.py).

Strukturiert: Nahkampf TP, Leiteigenschaft + Schadensschwelle („GE/KK 15“), AT/PA-Mod, Reichweite; Fernkampf TP,
Ladezeit, Reichweiten, Munition; Rüstung RS, Belastungsstufe, zusätzliche Abzüge („-1 GS, -1 INI“). Waffen-/
Rüstungsvorteil und -nachteil, Anmerkung und Verbreitung bleiben Wiki-Text.
"""
import collections
import html
import json
import os
import re

import books
import crawl_lists
import pagetext
import skillnames
import units
import wiki

PAGES = os.path.join(os.path.dirname(__file__), ".cache", "_equipment_pages.json")
TEXT_LABELS = ["Waffenvorteil", "Waffennachteil", "Rüstungsvorteil", "Rüstungsnachteil", "Anmerkung", "Verbreitung",
               "Publikation", "Publikation(en)", "Publikationen", "Errata"]
ATTR = r"(MU|KL|IN|CH|FF|GE|KO|KK)"


def cells(row):
    return [re.sub(r"\s+", " ", html.unescape(re.sub(r"<[^>]+>", " ", c))).replace("﻿", "").strip()
            for c in re.findall(r"<t[hd][^>]*>(.*?)</t[hd]>", row, flags=re.S)]


def value_table(page):
    """Erste Tabelle mit „TP“ bzw. „Rüstungsschutz“: (Kopfzeile, Wertezeilen)."""
    for t in re.findall(r"<table.*?</table>", wiki.fetch(page), flags=re.S):
        rows = [cells(r) for r in re.findall(r"<tr.*?</tr>", t, flags=re.S)]
        rows = [r for r in rows if any(r)]
        if rows and any(c in ("TP", "Rüstungsschutz") for c in rows[0]):
            return rows[0], [r for r in rows[1:] if len(r) == len(rows[0])]
    return None, []


def number(text):
    text = text.replace("–", "-").replace("−", "-").replace(" ", "")
    return int(text) if re.fullmatch(r"[+-]?\d+", text) else None


def damage(text):
    """„1W6+3“ → {dice: 1, sides: 6, bonus: 3}; „2W6“; „1W6-1“."""
    m = re.fullmatch(r"(\d*)W(\d*)\s*([+-–]\s*\d+)?", text.replace(" ", "").replace("–", "-"))
    if not m:
        return None
    # „1W+6“ (Tippfehler im Wiki): W6
    return {"dice": int(m.group(1) or 1), "sides": int(m.group(2) or 6), "bonus": number(m.group(3) or "0")}


def thresholds(text):
    """Leiteigenschaft und Schadensschwelle: „GE/KK 15“ → {GE: 15, KK: 15}; „GE/KK 15/14“, „GE 15/KK 14“ → je
    Eigenschaft eigene Schwelle; „keine“ → None."""
    t = re.sub(r"\s*/\s*", "/", text.strip())
    if m := re.fullmatch(rf"{ATTR}(?:/{ATTR})?\s+(\d+)", t):
        return {a: int(m.group(3)) for a in m.groups()[:2] if a}
    if m := re.fullmatch(rf"{ATTR}/{ATTR}\s+(\d+)/(\d+)", t):
        return {m.group(1): int(m.group(3)), m.group(2): int(m.group(4))}
    if m := re.fullmatch(rf"{ATTR}\s+(\d+)/{ATTR}\s+(\d+)", t):
        return {m.group(1): int(m.group(2)), m.group(3): int(m.group(4))}
    return None


def modifier(text):
    """„-1“ → -1; „-“ → None (z. B. keine Parade möglich)."""
    return number(text) if text.strip() not in ("", "-", "–", "keine") else None


def split_name(raw):
    """„Bogen (2H, i)“ → („Bogen“, zweihändig, improvisiert)."""
    flags = ""
    if m := re.search(r"\s*\(((?:2H|i)(?:,\s*(?:2H|i))*)\)\s*$", raw):
        flags = m.group(1)
        raw = raw[:m.start()]
    return raw.strip(), "2H" in flags, bool(re.search(r"\bi\b", flags))


def weapon(info, head, row, texts):
    col = dict(zip(head, row))
    name, two_handed, improvised = split_name(row[0])
    # Eigene Spalte „Kampftechnik“ (z. B. Waffen, die unter einer anderen Gattung geführt werden) geht vor
    ct = skillnames.combat_id(col.get("Kampftechnik", "")) or skillnames.combat_id(info["category"]) or \
        skillnames.combat_id(info["category"].rstrip("n"))
    e = {"id": "weapon." + wiki.slug(name), "name": name, "category": info["category"], "combatTechnique": ct,
         "damageText": col.get("TP", "")}
    if d := damage(col.get("TP", "")):
        e["damage"] = d
    if two_handed:
        e["twoHanded"] = True
    if improvised:
        e["improvised"] = True
    if "L+S" in col:  # Nahkampf
        if found := thresholds(col["L+S"]):
            e["damageThresholds"] = found
        if "AT/PA-Mod" in col and (m := re.fullmatch(r"(.+?)\s*/\s*(.+)", col["AT/PA-Mod"])):
            at, pa = modifier(m.group(1)), modifier(m.group(2))
        else:
            at, pa = modifier(col.get("AT-Mod", "")), modifier(col.get("PA-Mod", ""))
        if at is not None:
            e["attackModifier"] = at
        if pa is not None:
            e["parryModifier"] = pa
        elif at is not None:
            e["noParry"] = True  # „0/-“: mit dieser Waffe ist keine Parade möglich
        e["reach"] = col.get("RW", "")
    else:  # Fernkampf
        e["ranged"] = True
        for key, label in [("loadTime", "LZ"), ("ranges", "RW"), ("ammunition", "Munition"), ("ammunition", "Munitionstyp")]:
            if col.get(label) and col[label] != "-":
                e[key] = col[label]
    for key, label in [("weight", "Gewicht"), ("length", "Länge"), ("price", "Preis"), ("complexity", "Komplexität")]:
        if col.get(label) and col[label] != "-":
            e[key] = col[label]
    return e | texts | parry_bonus(e, texts.get("note") or "")


def parry_bonus(e, note):
    """Wiki „Parierwaffen und Schilde“: passiver Parade-Bonus für die Hauptwaffe. Bei Schilden ist es der PA-Mod,
    bei Parierwaffen steht er in der Anmerkung („Parierwaffe (PA-Bonus +1 für die Hauptwaffe)“). Große Schilde geben
    „zusätzlich –2 AT auf die Hauptwaffe“."""
    result = {}
    shield = e["combatTechnique"] == "ct.schilde"
    m = re.search(r"Parierwaffe\s*\(PA-Bonus\s*([+-–]?\s*\d+)", note)
    bonus = number(m.group(1)) if m else None
    if shield:
        result["shield"] = True
        if (e.get("parryModifier") or 0) > 0:
            bonus = e["parryModifier"]
    if bonus:
        result["parryBonus"] = bonus
    if m := re.search(r"zusätzlich\s*([+-–]\s*\d+)\s*AT auf die Hauptwaffe", note):
        result["mainWeaponAttackModifier"] = number(m.group(1))
    return result


def armor(info, head, row, texts):
    col = dict(zip(head, row))
    name = re.sub(r"\s*/\s*", "/", row[0])  # „Schwere Kleidung /Winterkleidung“
    e = {"id": "armor." + wiki.slug(name), "name": name, "category": info["category"],
         "protection": number(col.get("Rüstungsschutz", "")) or 0,
         "encumbrance": number(col.get("Belastungsstufe", col.get("Belastung (Stufe)", ""))) or 0}
    extra = col.get("zusätzliche Abzüge", "")
    if extra and extra not in ("-", "keine"):
        e["extraPenaltiesText"] = extra
        for value, what in re.findall(r"([+-–]\s*\d+)\s*(GS|INI)", extra):
            e["speedPenalty" if what == "GS" else "initiativePenalty"] = number(value)
    for key, label in [("weight", "Gewicht"), ("price", "Preis"), ("complexity", "Komplexität")]:
        if col.get(label) and col[label] != "-":
            e[key] = col[label]
    return e | texts


def main():
    entries = []
    for info in json.load(open(PAGES, encoding="utf-8")):
        head, rows = value_table(info["page"])
        if not rows:
            print("  keine Werte:", info["page"])
            continue
        lines = pagetext.article_lines(info["page"])
        f = pagetext.labeled(lines, TEXT_LABELS)
        texts = {}
        for key, labels in [("advantageText", ("Waffenvorteil", "Rüstungsvorteil")),
                            ("disadvantageText", ("Waffennachteil", "Rüstungsnachteil")),
                            ("note", ("Anmerkung",)), ("distribution", ("Verbreitung",))]:
            value = next((f[l] for l in labels if f.get(l)), None)
            if value and value.strip() not in ("-", "keine"):
                texts[key] = value.replace("\n", " ")
        texts["publications"] = books.publications(pagetext.publication_text(lines))
        if not texts["publications"]:
            continue
        for row in rows:  # mehrere Zeilen = Varianten (z. B. Bannschwert klein/mittel/groß)
            entries.append(units.numbers((weapon if info["kind"] == "weapon" else armor)(info, head, row, texts)))
    seen = {}
    for e in entries:
        if e["id"] in seen:
            e["id"] += "-" + wiki.slug(e["category"])
        if e["id"] in seen:
            e["id"] += "-" + str(sum(1 for x in seen if x.startswith(e["id"])))
        seen[e["id"]] = e
    weapons = [e for e in entries if e["id"].startswith("weapon.")]
    armors = [e for e in entries if e["id"].startswith("armor.")]
    crawl_lists.write_by_book("weapons", weapons)
    crawl_lists.write_by_book("armor", armors)
    print("  ohne Kampftechnik:", collections.Counter(e["category"] for e in weapons if not e["combatTechnique"]),
          "| ohne TP:", sum(1 for e in weapons if "damage" not in e),
          "| Nahkampf ohne L+S:", sum(1 for e in weapons if not e.get("ranged") and "damageThresholds" not in e),
          "| ohne AT/PA-Mod:", sum(1 for e in weapons if not e.get("ranged") and "attackModifier" not in e))


if __name__ == "__main__":
    main()
