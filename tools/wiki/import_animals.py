"""Erzeugt data/animals/*.json (Tierarten aus dem Bestiarium) und data/animal-abilities/*.json (Tricks,
Ausbildungsaufsätze, Vertrautentricks, Tier-SF, Kampf-SF für Tiere, Vor- und Nachteile für Tiere) aus dem Regel-Wiki.
Seitenliste aus crawl_animals.py.

Wertekasten: Eigenschaften, LeP, AsP, KaP, INI, VW (bzw. AW/PA), SK, ZK, GS stehen je nach Seite in einer oder in
mehreren Zeilen; Angriffe als „Biss: AT 12 TP 1W3 RW kurz“. Die Tierkunde-Stufen (QS 1 …) werden nicht übernommen.
Die Mods und möglichen Ausbildungsaufsätze je Tierart stehen in der Tabelle „Tierarten und Modifikatoren“ auf der
Seite Ausbildungsaufsätze.
"""
import json
import os
import re

import books
import crawl_animals
import crawl_lists
import import_equipment
import import_special_abilities as sfi
import pagetext
import skillnames
import wiki

PAGES = os.path.join(os.path.dirname(__file__), ".cache", "_animal_pages.json")
TRAINING_PAGE = "TSF_Ausbildungsaufsätze.html"
ATTRS = ["MU", "KL", "IN", "CH", "FF", "GE", "KO", "KK"]
ANIMAL_LABELS = ["Größe", "Gewicht", "RS/BE", "Aktionen", "Vorteile/Nachteile", "Vorteile", "Vorteil", "Nachteile",
                 "Nachteil", "Sonderfertigkeiten", "Talente", "Zauber", "Liturgien", "Anzahl", "Größenkategorie",
                 "Typus", "Beute", "Kampfverhalten", "Flucht", "Schmerz +1 bei", "Anmerkung", "Sonderregeln", "Jagd",
                 "Vertrautentricks", "Tierkunde", "Publikation", "Publikation(en)", "Publikationen", "Errata"]
ABILITY_LABELS = ["Regel", "Regeln", "Wirkung", "Boni", "Tricks", "Tierarten", "AsP-Kosten", "Wirkungsdauer",
                  "Merkmal", "Erschwernis", "Voraussetzung", "Voraussetzungen", "AP-Wert", "Anmerkung", "Errata",
                  "Publikation", "Publikation(en)", "Publikationen", "Publikationen(en)"]
PREFIX = {"trick": "animal-trick", "training": "animal-training", "familiarTrick": "familiar-trick",
          "specialAbility": "animal-sf", "combatSpecialAbility": "animal-combat-sf", "advantage": "animal-advantage",
          "disadvantage": "animal-disadvantage"}
EMPTY = ("", "-", "–", "keine", "keiner")
STAT = re.compile(r"\b(MU|KL|IN|CH|FF|GE|KO|KK|LeP|AsP|KaP|INI|VW|AW|PA|SK|ZK|GS)\s+"
                  r"(-?–?\s?\d+(?:\+\d*W\d+)?(?:\s*\([tk]\))?(?:/\d+)?(?:\s*\([^)]*\))?|[-–])")
ATTACK = re.compile(r"^([^:]{2,60}?):\s*(?=(?:AT|FK|LZ|TP)\b)(.*)$")


def value(text):
    return None if text is None or text.strip().strip(".") in EMPTY else text.strip()


def num(text):
    m = re.match(r"\s*([-–]?)\s*(\d+)", text or "")
    return None if not m else (-1 if m.group(1) else 1) * int(m.group(2))


def split_lines(lines, labels):
    """„Sonderfertigkeiten: … Talente: …“ in einer Zeile: vor jedem weiteren bekannten Feldnamen umbrechen."""
    pattern = re.compile(r"\s+(?=(?:" + "|".join(re.escape(l) for l in labels) + r"):)")
    result = []
    for line in lines:
        result += [p for p in pattern.split(line) if p]
    return result


def split_top(text, sep=","):
    """Trennt an sep außerhalb von Klammern."""
    parts, depth, current = [], 0, ""
    for ch in text:
        depth += ch in "(["
        depth -= ch in ")]"
        if ch == sep and depth == 0:
            parts.append(current)
            current = ""
        else:
            current += ch
    parts.append(current)
    return [p.strip() for p in parts if p.strip()]


def names(text):
    """„Dunkelsicht I, Herausragender Sinn (Gehör), (empfohlen: Loyal, Schnell)“ → (fest, empfohlen)."""
    fixed, recommended = [], []
    for part in split_top(text or ""):
        if m := re.fullmatch(r"\(?\s*empfohlen:?\s*(.*?)\)?", part, flags=re.I):
            recommended += split_top(m.group(1))
        elif value(part):
            fixed.append(part)
    return fixed, recommended


def stats(lines):
    """Wertekasten → Eigenschaften und Grundwerte (Rohtext je Kürzel)."""
    found = {}
    for line in lines:
        if ":" in line.split(" ")[0]:
            continue
        for m in STAT.finditer(line):
            found.setdefault(m.group(1), m.group(2).strip())
    return found


def attacks(lines):
    result = []
    for line in lines:
        m = ATTACK.match(line)
        if not m or m.group(1).strip() in ANIMAL_LABELS:
            continue
        rest = re.sub(r"(\S)(?=(?:AT|FK|LZ|TP|RW)\s)", r"\1 ", m.group(2))  # „1W3–1RW kurz“
        a = {"name": m.group(1).strip().capitalize() if m.group(1).isupper() else m.group(1).strip()}
        for key, label in [("attack", "AT"), ("ranged", "FK"), ("loadTime", "LZ"), ("damage", "TP"), ("reach", "RW")]:
            if mm := re.search(rf"\b{label}\s+(.+?)(?=\s+(?:AT|FK|LZ|TP|RW)\b|$)", rest):
                v = mm.group(1).strip()
                if key in ("attack", "ranged"):
                    v = num(v)  # „14 (Biss)“: führende Zahl; der ganze Text steht in „text“
                    if v is None:
                        continue
                a[key] = v
        a["text"] = rest.strip()
        result.append(a)
    return result


def skills(text, unparsed):
    result = []
    # „Sinnesschärfe 10 Verbergen 4“ (Komma fehlt), „Körperbeherrschung4“, „Verbergen 12*“, „Einschüchtern 11/12 (w/m)“
    text = re.sub(r"[\x00-\x1f]", "", text or "")  # Steuerzeichen im Wiki („Körperbeherr\x02schung“)
    text = re.sub(r"(\d)\s+(?=[A-ZÄÖÜ])", r"\1, ", text)
    text = re.sub(r"([a-zäöüß])(\d)", r"\1 \2", text)
    for part in split_top(text):
        part = re.sub(r"(\d)\*+", r"\1", part)
        m = re.fullmatch(r"(.+?)\s+(-?\d+)(?:/\d+)?(?:\s*\((.*)\))?", part)
        if not m:
            m2 = re.fullmatch(r"(.+?)\s*\((.*)\)", part)
            if m2 and (sid := skillnames.skill_id(m2.group(1))):
                result.append({"skill": sid, "note": m2.group(2)})  # „Klettern (keine Probe erlaubt …)“
            elif value(part):
                unparsed.append(part)
            continue
        sid = skillnames.skill_id(m.group(1))
        if sid is None:
            unparsed.append(part)
            continue
        e = {"skill": sid, "value": int(m.group(2))}
        note = m.group(3)
        if re.search(r"\s-?\d+/\d", part):
            note = part[len(m.group(1)):].strip()  # „11/12 (w/m)“: erster Wert, Rest als Hinweis
        if note:
            e["note"] = note
        result.append(e)
    return result


def training_table():
    """Tabelle „Tierarten und Modifikatoren“: Tiername → Mods, Ausbildungsaufsätze, Komplexität."""
    result = {}
    for t in re.findall(r"<table.*?</table>", wiki.fetch(TRAINING_PAGE), flags=re.S):
        rows = [import_equipment.cells(r) for r in re.findall(r"<tr.*?</tr>", t, flags=re.S)]
        for row in rows:
            if len(row) != 5 or row[1].startswith("Ausbildungs") or not row[0]:
                continue
            result[row[0]] = {"trainingModifier": row[1], "trickModifier": row[2], "trainingsText": row[3],
                              "complexity": row[4]}
    return result


def modifier(text):
    t = text.replace("+/–0", "0").replace("+/-0", "0").replace("±0", "0").replace("–", "-").strip()
    return int(t) if re.fullmatch(r"[+-]?\d+", t) else None


TRAINING_NAMES = {"Hüte": "Hütetier", "Jagd": "Jagdtier", "Kampf": "Kampftier", "Reit": "Reittier", "Renn": "Renntier",
                  "Such": "Suchtier", "Trage": "Trage- oder Zugtier", "Trag": "Trage- oder Zugtier",
                  "Zug": "Trage- oder Zugtier", "Wach": "Wachtier", "Wache": "Wachtier", "Zirkus": "Zirkustier"}


# Zeilen der Tabelle, die für eine ganze Gruppe bzw. eine Doppelseite gelten
TRAINING_GROUPS = {"Bären": "Bär", "Wölfe": "Wolf", "Krokodil": "Krokodil"}
TRAINING_ALIASES = {"Maulesel und Maultier": "Maulesel"}


def match_training(name, table):
    """Tabellenname zum Tier: „Scheunenkatz“ ↔ „Scheunenkatze“, „Kröte (Koschkröte)“ ↔ „Koschkröte“."""
    keys = [TRAINING_ALIASES.get(name, name), re.sub(r"\s*\(.*\)$", "", name)]
    if m := re.search(r"\((.+)\)$", name):
        keys.append(m.group(1))
    for k in keys:
        for cand in (k, k.rstrip("e"), k + "e"):
            if cand in table:
                return cand
    return None


def first_block(lines):
    """Seiten mit zwei Wertekästen (Schakal: Bestiarium und Götterwirken): nur der erste bis zu seinen Quellen."""
    pub = next((i for i, l in enumerate(lines) if l.startswith("Publikation")), None)
    if pub is None:
        return lines
    end = next((i for i in range(pub + 1, len(lines)) if not re.search(r"Seiten?\b|,\s*\d|Vorsatz", lines[i])), len(lines))
    return lines[:end]


def publications(lines):
    text = pagetext.publication_text(lines)
    text = re.sub(r"(Seiten? \d+)(?:\s*,\s*\d+)+", r"\1", text)  # „Seite 206, 375, 377“
    text = re.sub(r"(Seiten? \d+) und .*", r"\1", text)  # „Seite 35 und [reduzierter Wertekasten] Seite 112 …“
    return books.publications(text)


def animal(info, table, used, unparsed):
    lines = split_lines(first_block(crawl_animals.lines_of(info["page"])), ANIMAL_LABELS)
    # „Tierkunde (domestizierte Tiere):“ u. Ä. als Feld, damit es nicht am vorigen Feld hängt
    lore = next((l for l in lines if re.match(r"(Tierkunde|Magiekunde|Sphärenkunde|Pflanzenkunde|Fischen & Angeln|Götter & Kulte)\b", l)), "")
    lines = [re.sub(r"^(Tierkunde|Magiekunde|Sphärenkunde|Pflanzenkunde|Fischen & Angeln|Götter & Kulte)\b.*\)\s*:?$", "Tierkunde:", l)
             for l in lines]
    f = pagetext.labeled(lines, ANIMAL_LABELS)
    # Einzeilige Felder: Folgezeilen gehören zum Wertekasten (Gewicht → „MU 12 …“) oder sind Tierkunde-Stufen
    # (Talente, Sonderfertigkeiten, Vor-/Nachteile gehen dagegen teils über mehrere Zeilen.)
    for label in ("Größe", "Gewicht", "RS/BE", "Aktionen", "Anzahl", "Größenkategorie", "Typus", "Beute", "Flucht",
                  "Schmerz +1 bei", "Jagd"):
        if f.get(label):
            f[label] = f[label].split("\n")[0]
    name = lines[0]
    s = stats(lines)
    e = {"id": "animal." + wiki.slug(name), "name": name}
    if info.get("group"):
        e["group"] = info["group"]
    for key, label in [("sizeText", "Größe"), ("weightText", "Gewicht")]:
        if v := value(f.get(label)):
            e[key] = v.replace("\n", " ")
    e["attributes"] = {a: num(s[a]) for a in ATTRS if a in s and num(s[a]) is not None}
    notes = {a: m.group(1) for a in ATTRS if a in s and (m := re.search(r"\(([tk])\)", s[a]))}
    if notes:
        e["attributeNotes"] = notes
    for key, label in [("lifePoints", "LeP"), ("astralEnergy", "AsP"), ("karmaEnergy", "KaP"), ("defense", "VW"),
                       ("dodge", "AW"), ("parry", "PA"), ("spirit", "SK"), ("toughness", "ZK"), ("speed", "GS")]:
        if label in s and (n := num(s[label])) is not None:
            e[key] = n
            if label == "GS" and not re.fullmatch(r"-?\d+", s[label]):
                e["speedText"] = s[label]
    if "INI" in s:
        e["initiative"] = s["INI"]
    e["attacks"] = attacks(lines)
    if m := re.match(r"(\d+)\s*/\s*(\d+)", f.get("RS/BE", "")):
        e["protection"], e["encumbrance"] = int(m.group(1)), int(m.group(2))
    if f.get("RS/BE") and not re.fullmatch(r"\d+\s*/\s*\d+", f["RS/BE"].strip()):
        e["protectionText"] = f["RS/BE"].replace("\n", " ")
    if v := value(f.get("Aktionen")):
        e["actions"] = v
    traits = f.get("Vorteile/Nachteile")
    adv_text = f.get("Vorteile") or f.get("Vorteil")
    dis_text = f.get("Nachteile") or f.get("Nachteil")
    if traits:
        adv_text, _, dis_text = traits.replace("\n", " ").partition(" / ")
    for key, text in [("advantages", adv_text), ("disadvantages", dis_text)]:
        fixed, rec = names(text)
        if fixed:
            e[key] = fixed
        if rec:
            e["recommended" + key[0].upper() + key[1:]] = rec
    if v := value(f.get("Sonderfertigkeiten")):
        e["specialAbilities"] = split_top(v.replace("\n", " "))
    if v := value(f.get("Talente")):
        miss = []
        e["skills"] = skills(v.replace("\n", " "), miss)
        unparsed += [f"{name}: {m}" for m in miss]
    for key, label in [("spellsText", "Zauber"), ("liturgiesText", "Liturgien"), ("count", "Anzahl"),
                       ("sizeCategory", "Größenkategorie"), ("type", "Typus"), ("prey", "Beute"),
                       ("combatBehaviour", "Kampfverhalten"), ("flight", "Flucht"), ("painText", "Schmerz +1 bei"),
                       ("hunting", "Jagd"), ("specialRules", "Sonderregeln"), ("note", "Anmerkung"),
                       ("errata", "Errata")]:
        if v := value(f.get(label)):
            e[key] = v
    key = match_training(name, table) or match_training(info["label"], table) \
        or TRAINING_GROUPS.get((info.get("group") or "").split(" › ")[-1])
    if key:
        used.add(key)
        t = table[key]
        e["training"] = {"trainingModifier": modifier(t["trainingModifier"]),
                         "trickModifier": modifier(t["trickModifier"]),
                         "trainings": sorted({"animal-training." + wiki.slug(TRAINING_NAMES[x])
                                              for x in (re.sub(r"tier$", "", p.strip()) for p in t["trainingsText"].split(","))
                                              if x in TRAINING_NAMES}),
                         "complexity": t["complexity"]}
    # „Tierkunde (domestizierte Tiere)“ bzw. „(Wildtiere)“: Startwert der Loyalität 4 bzw. 0
    if "domestiziert" in lore:
        e["domesticated"] = True
    elif re.search(r"Wild|Ungeheuer", lore):
        e["domesticated"] = False
    if v := value(f.get("Vertrautentricks")):
        e["familiarTricksText"] = v
    e["publications"] = publications(lines)
    return e


BONUS = re.compile(r"([+-]\d+)\s*(%?)\s*(MU|KL|IN|CH|FF|GE|KO|KK|AT|VW|PA|AW|TP|LeP|GS|INI|RS|SK|ZK)")


def bonuses(text, unparsed, owner):
    """„+2 MU“, „+25 % LeP“, „Einschüchtern-FW +6“ → strukturierte Boni; Sonstiges bleibt im Text."""
    result = []
    for line in text.split("\n"):
        line = line.strip().replace("–", "-")
        if m := BONUS.fullmatch(line):
            b = {"target": m.group(3), "value": int(m.group(1))}
            if m.group(2):
                b["percent"] = True
            result.append(b)
            continue
        m = re.fullmatch(r"(.+?)-FW\s*([+-]\d+)(?:\s*\(.*\))?", line)
        if m:
            options = [skillnames.skill_id(re.sub(r"\*$", "", n.strip())) for n in re.split(r"\s+oder\s+|-\s+oder\s+", m.group(1))]
            options = [o for o in options if o]
            if options:
                b = {"target": options[0], "value": int(m.group(2))}
                if len(options) > 1:
                    b["options"] = options
                result.append(b)
                continue
        if line:
            unparsed.append(f"{owner}: Bonus „{line}“")
    return result


def ability(info, trick_ids, unparsed):
    lines = pagetext.article_lines(info["page"])
    f = pagetext.labeled(lines, ABILITY_LABELS)
    raw = info["label"] if re.match(r"(Vorteil|Nachteil) ", lines[0]) else lines[0]
    name, levels = sfi.adv.split_name(raw)
    kind = info["kind"]
    e = {"id": f"{PREFIX[kind]}.{wiki.slug(name)}", "name": name, "kind": kind}
    if levels:
        e["maxLevel"] = levels
    # Beschreibung ohne Feldnamen (Tricks, Ausbildungsaufsätze) zwischen Titel und erstem Feld
    first = next((i for i, l in enumerate(lines[1:], 1) if any(l == x or l.startswith(x + ":") for x in ABILITY_LABELS)), len(lines))
    intro = [l for l in lines[1:first] if not re.fullmatch(r"\((?:Basis|Spezial)manöver\)", l)]
    if m := next((re.fullmatch(r"\((Basis|Spezial)manöver\)", l) for l in lines[1:first] if re.fullmatch(r"\((?:Basis|Spezial)manöver\)", l)), None):
        e["maneuver"] = m.group(1).lower()
    rule = f.get("Regel") or f.get("Regeln") or f.get("Wirkung") or "\n".join(intro)
    e["ruleText"] = rule
    for key, label in [("animalsText", "Tierarten"), ("astralCostText", "AsP-Kosten"), ("durationText", "Wirkungsdauer"),
                       ("property", "Merkmal"), ("penalty", "Erschwernis"), ("note", "Anmerkung"), ("errata", "Errata")]:
        if v := value(f.get(label)):
            e[key] = v
    pre = value(f.get("Voraussetzung") or f.get("Voraussetzungen"))
    if pre:
        e["prerequisitesText"] = pre
        if m := re.search(r"Loyalität\s+(\d+)", pre):
            e["loyalty"] = int(m.group(1))
        tricks = [trick_ids.get(t.strip()) for t in re.findall(r"Trick\s+([^,;]+)", pre)]
        if tricks and all(tricks):
            e["requiredTricks"] = tricks
    if v := value(f.get("Boni")):
        e["bonusesText"] = v
        e["bonuses"] = bonuses(v, unparsed, name)
    if v := value(f.get("Tricks")):
        found = [trick_ids.get(re.sub(r"\s+", " ", t).strip()) for t in re.split(r",|\bund\b", v)]
        if all(found):
            e["tricks"] = found
        else:
            unparsed.append(f"{name}: Tricks „{v}“")
    # Tiervorteile: „Es startet mit einem um 2 höheren KO-Wert.“, „Es hat eine um 25 % höhere GS.“
    if kind == "advantage":
        found = [{"target": m.group(2), "value": int(m.group(1))}
                 for m in re.finditer(r"um (\d+) höheren (MU|KL|IN|CH|FF|GE|KO|KK)-Wert", rule)]
        found += [{"target": m.group(2), "value": int(m.group(1)), "percent": True}
                  for m in re.finditer(r"um (\d+) % höhere (GS|LeP)\b", rule)]
        if found:
            e["bonuses"] = found
    e["apValueText"] = f.get("AP-Wert", "")
    if "von Anfang an" in e["apValueText"]:
        e["cost"] = {"type": "fixed", "ap": 0}  # Zwiegespräch, Hexensinn: hat jeder Vertraute
    else:
        e["cost"] = sfi.sf_cost(e["apValueText"], levels) if e["apValueText"] else {"type": "manual"}
    if kind == "disadvantage" and e["cost"].get("ap"):
        e["cost"]["ap"] = -abs(e["cost"]["ap"])
    if e["cost"]["type"] == "perLevel" and not levels:
        e["cost"] = {"type": "fixed", "ap": e["cost"]["ap"]}
    if e["cost"]["type"] == "levels" and not levels:
        e["maxLevel"] = len(e["cost"]["levels"])
    e["publications"] = publications(lines)
    return e


def main():
    pages = json.load(open(PAGES, encoding="utf-8"))
    table = training_table()
    used, unparsed = set(), []
    animals = [animal(info, table, used, unparsed) for info in pages if info["kind"] == "animal"]
    seen = {}
    for a in animals:
        if a["id"] in seen:
            a["id"] += "-" + wiki.slug(a.get("group", "x"))
        seen[a["id"]] = a

    trick_pages = [i for i in pages if i["kind"] == "trick"]
    trick_ids = {}
    for info in trick_pages:
        n = pagetext.article_lines(info["page"])[0]
        trick_ids[n] = "animal-trick." + wiki.slug(n)
    abilities = [ability(info, trick_ids, unparsed) for info in pages if info["kind"] != "animal"]

    crawl_lists.write_by_book("animals", animals)
    crawl_lists.write_by_book("animal-abilities", abilities)
    missing = sorted(set(table) - used)
    print("  Tiere ohne Ausbildungstabelle:", sum(1 for a in animals if "training" not in a),
          "| Tabellenzeilen ohne Tier:", missing)
    print("  ohne Eigenschaften:", [a["name"] for a in animals if len(a["attributes"]) < 8])
    for u in unparsed:
        print("  nicht erkannt:", u)
    for a in abilities:
        if a["cost"]["type"] == "manual":
            print("  AP manuell:", a["id"], "|", a["apValueText"][:80])


if __name__ == "__main__":
    main()
