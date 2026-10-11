"""Zuordnung Optolith-ID → App-ID für Import und Export von Optolith-Helden.

Optolith liefert seine Daten im Programm mit (YAML unter app/Database/Data). Übernommen werden nur IDs und die Namen
zum Abgleich, keine Regeltexte (das Optolith-README erlaubt die ID-Zuordnung zum Lesen von Heldendateien ausdrücklich).

Daten auspacken (einmalig):
    ./Optolith_1.5.1.AppImage --appimage-extract
    npx @electron/asar extract squashfs-root/resources/app.asar optolith-app
Dann:
    python3 tools/optolith/build_mapping.py optolith-app/app/Database/Data

Schreibt src/DSACharacterCreator.Content/data/optolith/mapping.json und docs/OPTOLITH.md (was nicht zugeordnet ist).
"""
import difflib
import json
import os
import re
import sys
import unicodedata

import yaml

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
DATA = os.path.join(ROOT, "src", "DSACharacterCreator.Content", "data")
OUT = os.path.join(DATA, "optolith", "mapping.json")
REPORT = os.path.join(ROOT, "docs", "OPTOLITH.md")

# Optolith-Name → Name in der App, wo die Schreibweise abweicht (normalisiert verglichen, siehe norm()).
ALIASES = {
    "Menschen": "Mensch",
    "Zwerge": "Zwerg",
    "Persönlichkeitsschwächen": "Persönlichkeitsschwäche",
    "Kein Bann des Eisen": "Kein Bann des Eisens",
    "Schlechte Regeneration (Karmalenergie)": "Schlechte Regeneration (Karmaenergie)",
    "Fertigkeitsspezialisierung": "Fertigkeitsspezialisierung (Talente)",
    "Berufsgeheimnis": "Berufsgeheimnisse & Geheimwissen",
    "Schnellladen": "Schnellladen (Kampftechnik)",
    "Nachladespezialist": "Nachladespezialist (Kampftechnik)",
    "Prophezeihung": "Prophezeiung",
    "Höhere Prophezeihung": "Höhere Prophezeiung",
    "Tradition (Zauberbarde)": "Tradition (Zauberbarden)",
    "Tradition (Intuitiver Zauberer)": "Tradition (Intuitive Zauberer)",
    "Mohisch": "Tahaya",
    "(Alt-)Imperiale Zeichen": "Imperiale Zeichen",
    "Drakhard-Zinken": "Drakhard-Zinke",
}

# Optolith-Einträge, deren Name mehrdeutig ist (gleicher Name, Unterscheidung nur über die ID)
ID_ALIASES = {
    "ADV_28": "Immunität gegen (Gift)",
    "ADV_29": "Immunität gegen (Krankheit)",
    # Andere Schreibweise oder Zusatz in Klammern; geprüft über Regeltext bzw. Buch und Seite
    "ADV_83": "Feenblut (Großer Biestinger)",
    "SPELL_237": "Attributo (Gewandtheit)",
    "LITURGY_138": "Eisbärengestalt",
    "SA_186": "Hruruzat-Stil",
    "SA_231": "Adaption (Zauber)",
    "SA_339": "Volumenerweiterung des Zauberstabes",
    "SA_348": "Klinge wider (Wesenheit)",
    "SA_365": "Machtvoller Instrumentzauber",
    "SA_395": "Brennglas (Zauberkugel)",  # Aventurische Magie I S. 36 (Scharlatane)
    "SA_702": "Brennglas (Magierkugel)",  # Aventurische Magie II S. 17 (Gildenmagier)
    "SA_400": "Machtvolle Kugelzauber (Zauberkugel)",  # Aventurische Magie I S. 37
    "SA_706": "Machtvolle Kugelzauber (Magierkugel)",  # Aventurische Magie II S. 17
    "SA_404": "Volumenerweiterung der Zauberkugel",  # Aventurische Magie I S. 37
    "SA_469": "Fokus der Verwirrung (Boron)",  # Aventurisches Götterwirken S. 26
    "SA_518": "Zeichen der Vorsehung",
    "SA_581": "Zeremonialgegenstände herstellen",
    "SA_639": "Gebieter/in des (Aspekts)",
    "SA_813": "Rote Jungfern (Schwarze Witwe)",
    "SA_891": "Ungeheuer-Taktik (einzelne Spezies)",
    "SA_990": "Flusslotse (Region)",
    "SA_991": "Seelotse (Region)",
    "SA_995": "Anatomie (Spezies)",
    "SA_1008": "Giftverstärkung (Giftart)",
    "SA_1033": "Wegabkürzungen kennen",
    "SA_1117": "Meisterin der Tarnung",
    "SA_1138": "Seenritter",
    "SA_1142": "Distelritter",
    "SA_1241": "Volumenerweiterung des Dolchs",
    "SA_1293": "Tradition (Zibilja)",
    "SA_1333": "Bewahrer des Uralten (Hüter der Kraft)",
    "SA_1334": "Brecher des Willens (Mehrer der Macht)",
    "SA_1335": "Hüterin des Hains (Haindruiden)",
    "SA_1336": "Konzilsmeister (Konzilsdruiden)",
    "SA_1337": "Meister der elementaren Gewalten (Konzilsdruiden)",
    "SA_1338": "Meister des Kreises (Hüter der Kraft)",
    "SA_1339": "Meister vom Wald (Sumudiener)",
    "SA_1340": "Unscheinbare (Mehrer der Macht)",
    "SA_1341": "Wächter der Göttin (Sumudiener)",
    "SA_1342": "Weiße Leute (Haindruiden)",
    "SA_1350": "Gegnerische Zauberpraxis (Element)",
    "SA_1440": "Scholar des Agrimeton",
}


def norm(name):
    s = unicodedata.normalize("NFC", name or "").lower()
    s = s.replace("ß", "ss").replace("–", "-").replace("—", "-").replace("’", "'").replace("„", "").replace("“", "")
    s = re.sub(r"[^\w]+", " ", s)
    return " ".join(s.split())


# SF, deren Auswahl in der App eine Zauber- bzw. Liturgieerweiterung am Zauber ist
ENHANCEMENT_ABILITIES = {"SA_414", "SA_663"}

# Professionen, die sich nicht über den Namen finden lassen: Optolith-ID → (App-ID, Variante)
PROFESSION_OVERRIDES = {
    "P_19": ("profession.katzenhexe", None), "P_137": ("profession.katzenhexe", None),
    "P_20": ("profession.kroetenhexe", None), "P_138": ("profession.kroetenhexe", None),
    "P_21": ("profession.rabenhexe", None), "P_139": ("profession.rabenhexe", None),
    "P_91": ("profession.eulenhexe", None), "P_136": ("profession.eulenhexe", None),
    "P_92": ("profession.schlangenhexe", None), "P_140": ("profession.schlangenhexe", None),
    "P_45": ("profession.kuensterlin", None),
    "P_71": ("profession.intuitiver-zauberer-magischer-taugenichts", None),
    "P_75": ("profession.reisende-sangara", None),
    "P_93": ("profession.mehrer-der-macht", None), "P_200": ("profession.mehrer-der-macht", None),
    "P_94": ("profession.sumudiener", None), "P_201": ("profession.sumudiener", None),
    "P_124": ("profession.belhankaner-magierin", None),
    "P_169": ("profession.efferdgeweihter-der-siebenwindkueste", None),
    "P_221": ("profession.nipakau-kocan-tahaya-geisterkrieger", None),
    "P_229": ("profession.herr-der-erde-luftherr", None),
    "P_87": ("profession.ritter-der-streitenden-koenigreiche", "seenritter-in"),
    "P_198": ("profession.ritter-der-streitenden-koenigreiche", "seenritter-in"),
    "P_88": ("profession.ritter-der-streitenden-koenigreiche", "waldritter-in"),
    "P_199": ("profession.ritter-der-streitenden-koenigreiche", "waldritter-in"),
}

STOPWORDS = {"der", "des", "die", "zu", "von", "nach", "und", "schwesternschaft"}


def stem(word):
    for suffix in ("in", "en", "er", "es", "e", "s"):
        if len(word) > 4 and word.endswith(suffix):
            return word[:-len(suffix)]
    return word


def tokens(name):
    return {stem(w) for w in norm(name).replace("schülerin", "schüler").split() if w not in STOPWORDS}


def similar(names, ours, threshold=0.6):
    """Ähnlichster Name nach gemeinsamen Wortstämmen (Magierakademien: „Graumagierin der Akademie …“ ↔ „Graumagier
    (Akademie …)“); nur eindeutige Treffer."""
    scores = {}
    for n in names:
        a = tokens(n)
        for e in ours:
            b = tokens(e["name"])
            scores[e["id"]] = max(scores.get(e["id"], 0), len(a & b) / len(a | b) if a | b else 0)
    ranked = sorted(scores.items(), key=lambda x: -x[1])
    if ranked and ranked[0][1] >= threshold and (len(ranked) == 1 or ranked[0][1] > ranked[1][1]):
        return ranked[0][0]
    return None


def variant_names(name):
    """„Fahrende/r Händler/in und Haimamud“ → Fahrender Händler, Fahrende Händlerin …, Haimamud; „Koch/Köchin“ → Koch,
    Köchin; „Bergmann/-frau“ → Bergmann, Bergfrau."""
    result = []
    for part in re.split(r" und | oder |, ", name):
        forms = [""]
        for word in part.split():
            if "/" not in word:
                options = [word]
            else:
                a, b = word.split("/", 1)
                if b.startswith("-"):  # Bergmann/-frau
                    options = [a, re.sub(r"(mann|mutter|vater)$", "", a) + b[1:]]
                elif b[:1].isupper() or len(b) > 3:  # Koch/Köchin
                    options = [a, b]
                else:  # Händler/in, Fahrende/r
                    options = [a, a + b, a[:-1] + b]
            forms = [f"{f} {o}".strip() for f in forms for o in options]
        result += forms
    return result


# Varianten, die sich nicht über den Namen finden lassen: Optolith-ID → Variante in der App
VARIANT_OVERRIDES = {
    "PV_111": "wildniskundige-fuer-andere-gelaendetypen",
    "PV_112": "wildniskundige-fuer-andere-gelaendetypen",
    "PV_113": "wildniskundige-fuer-andere-gelaendetypen",
}


def enhancement_key(name):
    """„Gute Illusion I“ = „Gute Illusion 1“, „Weiter und höher“ = „Weiter und höher 1“."""
    roman = {"I": "1", "II": "2", "III": "3"}
    words = [roman.get(w, w) for w in re.sub(r"[^\w ]", " ", norm_name(name)).split()]
    if words and words[-1] == "1":
        words = words[:-1]
    return " ".join(words).lower()


def norm_name(name):
    return re.sub(r"(\D)(\d)$", r"\1 \2", name.strip())


def load_ours(folder, prefixes=None):
    items = []
    path = os.path.join(DATA, folder)
    for f in sorted(os.listdir(path)):
        if f.endswith(".json"):
            items += json.load(open(os.path.join(path, f), encoding="utf-8"))
    if prefixes:
        items = [e for e in items if e["id"].split(".")[0] in prefixes]
    return items


def load_opt(data, name, lang="de-DE"):
    path = os.path.join(data, lang, name + ".yaml")
    return yaml.safe_load(open(path, encoding="utf-8")) or [] if os.path.exists(path) else []


def load_univ(data, name):
    path = os.path.join(data, "univ", name + ".yaml")
    return {e["id"]: e for e in (yaml.safe_load(open(path, encoding="utf-8")) or [])} if os.path.exists(path) else {}


def opt_name(entry):
    n = entry.get("name")
    if isinstance(n, dict):  # Professionen: {m: …, f: …}
        return n.get("m") or n.get("f") or ""
    return n or ""


def opt_names(entry):
    if entry.get("id") in ID_ALIASES:
        return [ID_ALIASES[entry["id"]]]
    n = entry.get("name")
    names = [v for v in (n.get("m"), n.get("f")) if v] if isinstance(n, dict) else [n] if n else []
    sub = entry.get("subname")
    if sub:  # Professionen: „Graumagier“ + „Akademie der Verformungen zu Lowangen“
        subs = [v for v in (sub.get("m"), sub.get("f")) if v] if isinstance(sub, dict) else [sub]
        names = [f"{a} {b}" for a in names for b in subs] + [f"{a} ({b})" for a in names for b in subs]
    return names


class Matcher:
    def __init__(self, ours, extra_names=None):
        self.index = {}
        for e in ours:
            for n in [e["name"]] + (extra_names(e) if extra_names else []):
                self.index.setdefault(norm(n), e["id"])

    def find(self, *names):
        for n in names:
            k = norm(ALIASES.get(n, n))
            if k in self.index:
                return self.index[k]
        return None


def match_simple(opt_entries, matcher, missing, label, names=opt_names):
    result = {}
    for e in opt_entries:
        found = matcher.find(*names(e))
        if found:
            result[e["id"]] = found
        else:
            missing.append((label, e["id"], opt_name(e)))
    return result


def main(data):
    missing = []
    m = {"source": "Optolith 1.5.1"}

    m["attributes"] = {f"ATTR_{i + 1}": a for i, a in enumerate(["MU", "KL", "IN", "CH", "FF", "GE", "KO", "KK"])}

    levels = json.load(open(os.path.join(DATA, "rules", "experience-levels.json"), encoding="utf-8"))
    m["experienceLevels"] = match_simple(load_opt(data, "ExperienceLevels"), Matcher(levels), missing, "Erfahrungsgrad")

    m["species"] = match_simple(load_opt(data, "Races"), Matcher(load_ours("species")), missing, "Spezies")
    # Spezies-Varianten sind in der App nur die Würfeltabelle fürs Aussehen
    m["speciesVariants"] = {e["id"]: opt_name(e) for e in load_opt(data, "RaceVariants")}

    m["cultures"] = match_simple(load_opt(data, "Cultures"), Matcher(load_ours("cultures")), missing, "Kultur")

    professions = load_ours("professions")
    by_id = {p["id"]: p for p in professions}
    pm = Matcher(professions)
    m["professions"] = {}
    for e in load_opt(data, "Professions"):
        if e["id"] in PROFESSION_OVERRIDES:
            found, variant = PROFESSION_OVERRIDES[e["id"]]
        else:
            found, variant = pm.find(*opt_names(e)) or similar(opt_names(e), professions), None
        if not found:
            missing.append(("Profession", e["id"], " ".join(opt_names(e)[:1])))
            continue
        m["professions"][e["id"]] = {"id": found, **({"variant": variant} if variant else {})}

    variants = {}
    pv_of = {}
    for pid, p in load_univ(data, "Professions").items():
        for v in p.get("variants", []) or []:
            pv_of[v] = pid
    for e in load_opt(data, "ProfessionVariants"):
        prof = m["professions"].get(pv_of.get(e["id"], ""), {}).get("id")
        if not prof:
            missing.append(("Professionsvariante", e["id"], opt_name(e)))
            continue
        vm = Matcher([{"id": v["id"], "name": v["name"]} for v in by_id[prof].get("variants", [])],
                     lambda v: variant_names(v["name"]))
        found = VARIANT_OVERRIDES.get(e["id"]) or vm.find(*opt_names(e))
        if found:
            variants[e["id"]] = {"profession": prof, "variant": found}
        else:
            missing.append(("Professionsvariante", e["id"], f"{opt_name(e)} ({by_id[prof]['name']})"))
    m["professionVariants"] = variants

    m["skills"] = match_simple(load_opt(data, "Skills"), Matcher(load_ours("skills")), missing, "Talent")
    # Anwendungsgebiete (Fertigkeitsspezialisierung: sid2 = Nummer des Anwendungsgebiets)
    m["skillApplications"] = {e["id"]: {str(a["id"]): a["name"] for a in e.get("applications", []) or []}
                              for e in load_opt(data, "Skills") if e.get("applications")}
    m["combatTechniques"] = match_simple(load_opt(data, "CombatTechniques"), Matcher(load_ours("combat-techniques")),
                                         missing, "Kampftechnik")

    spells = {}
    spells.update(match_simple(load_opt(data, "Spells"), Matcher(load_ours("spells", {"spell", "ritual"})), missing, "Zauber"))
    spells.update(match_simple(load_opt(data, "LiturgicalChants"), Matcher(load_ours("spells", {"liturgy", "ceremony"})),
                               missing, "Liturgie"))
    spells.update(match_simple(load_opt(data, "Cantrips"), Matcher(load_ours("spells", {"cantrip"})), missing, "Zaubertrick"))
    spells.update(match_simple(load_opt(data, "Blessings"), Matcher(load_ours("spells", {"blessing"})), missing, "Segnung"))
    m["spells"] = spells

    languages = load_ours("languages")
    scripts = load_ours("scripts")
    # „Leonal (Khorrzu)“ auch als „Leonal“
    base = lambda e: [re.sub(r" \(.*\)$", "", e["name"])]
    lm, sm = Matcher(languages, base), Matcher(scripts, base)
    traits = {}
    for file, ours, label in [("Advantages", load_ours("advantages"), "Vorteil"),
                              ("Disadvantages", load_ours("disadvantages"), "Nachteil"),
                              ("SpecialAbilities", load_ours("special-abilities"), "Sonderfertigkeit")]:
        matcher = Matcher(ours)
        defs = {e["id"]: e for e in ours}
        univ = load_univ(data, file)
        for e in load_opt(data, file):
            if e["id"] in ENHANCEMENT_ABILITIES:
                continue  # eigene Zuordnung (siehe „enhancements“)
            found = matcher.find(*opt_names(e))
            if not found:
                missing.append((label, e["id"], opt_name(e)))
                continue
            entry = {"id": found}
            d = defs[found]
            target = d.get("optionTarget") or (d["cost"].get("target") if d["cost"]["type"] == "bySkillFactor" else None)
            manual = d["cost"]["type"] == "manual"
            cost_options = [o["name"] for o in d["cost"].get("options", []) or []]
            om = Matcher([{"id": n, "name": n} for n in cost_options])
            univ_options = {o["id"]: o for o in univ.get(e["id"], {}).get("selectOptions", []) or []}
            options = {}
            for o in e.get("selectOptions", []) or []:
                if isinstance(o["id"], str):
                    continue  # TAL_…, SPELL_… über die allgemeinen Zuordnungen
                name = o.get("name", "")
                if target == "language":
                    opt = lm.find(name)
                elif target == "script":
                    opt = sm.find(name)
                elif target in ("property", "aspect"):
                    opt = name
                elif cost_options:
                    opt = om.find(name)
                else:
                    opt = None
                if opt:
                    options[str(o["id"])] = opt
                    continue
                # ohne Ausprägung in der App: als Präzisierung (freier Text), bei eigenem AP-Wert mit den Kosten
                text = {"text": name}
                cost = univ_options.get(o["id"], {}).get("cost")
                if manual and isinstance(cost, int):
                    text["ap"] = cost
                options[str(o["id"])] = text
            if options:
                entry["options"] = options
            if e.get("input"):
                entry["input"] = True  # freie Eingabe (sid als Text), z. B. „Angst vor …“
            if manual and isinstance(univ.get(e["id"], {}).get("cost"), int):
                entry["ap"] = univ[e["id"]]["cost"]
            traits[e["id"]] = entry
    m["activatables"] = traits

    # Zauber- und Liturgieerweiterungen: Optolith führt sie als Auswahl der SF „Zaubererweiterung“ (SA_414) bzw.
    # „Liturgieerweiterung“ (SA_663) mit eigener Nummer je Erweiterung; Zuordnung über den Zielzauber und den Namen.
    ours_spells = {e["id"]: e for e in load_ours("spells")}
    m["enhancements"] = {}
    for key, file in [("SA_414", "SpellEnhancements"), ("SA_663", "LiturgicalChantEnhancements")]:
        result = {}
        for e in load_opt(data, file):
            spell = m["spells"].get(e["target"])
            for level in ("level1", "level2", "level3"):
                if level not in e:
                    continue
                opt = e[level]
                ours = ours_spells.get(spell, {}).get("enhancements", [])
                found = next((x["id"] for x in ours if enhancement_key(x["name"]) == enhancement_key(opt["name"])), None)
                if not found:  # kleine Schreibunterschiede („Großer Radius“ / „Größerer Radius“) im selben Zauber
                    keys = {enhancement_key(x["name"]): x["id"] for x in ours}
                    close = difflib.get_close_matches(enhancement_key(opt["name"]), list(keys), n=1, cutoff=0.85)
                    found = keys[close[0]] if close else None
                if found:
                    result[str(opt["id"])] = {"spell": spell, "id": found}
                else:
                    missing.append(("Erweiterung", f"{key}:{opt['id']}", f"{opt['name']} ({e['target']})"))
        m["enhancements"][key] = result

    # Ausrüstung mit Optolith-Gruppe (gr), die eine Heldendatei je Gegenstand mitführt
    equipment = {}
    ours_eq = Matcher(load_ours("weapons") + load_ours("armor") + load_ours("items"))
    univ_eq = load_univ(data, "Equipment")
    for e in load_opt(data, "Equipment"):
        found = ours_eq.find(*opt_names(e))
        if found:
            equipment[e["id"]] = {"id": found, "gr": univ_eq.get(e["id"], {}).get("gr", 0)}
    m["items"] = equipment
    m["itemGroups"] = {str(e["id"]): opt_name(e) for e in load_opt(data, "EquipmentGroups")}

    m["hairColors"] = {str(e["id"]): opt_name(e) for e in load_opt(data, "HairColors")}
    m["eyeColors"] = {str(e["id"]): opt_name(e) for e in load_opt(data, "EyeColors")}
    m["socialStatus"] = {str(e["id"]): opt_name(e) for e in load_opt(data, "SocialStatuses")}

    # Namen der nicht zugeordneten Einträge für den Importbericht („Zaubererweiterung“ statt „SA_414“)
    m["unmapped"] = {oid: name for _, oid, name in missing}

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(m, f, ensure_ascii=False, indent=1, sort_keys=False)
        f.write("\n")

    write_report(m, missing)
    print(f"{OUT}: " + ", ".join(f"{k} {len(v)}" for k, v in m.items() if isinstance(v, dict)))
    print(f"{len(missing)} ohne Zuordnung, siehe {REPORT}")


def write_report(m, missing):
    lines = ["# Optolith-Import und -Export", "",
             "Import (Hauptmenü › Importieren, erkennt Optolith-Dateien selbst) und Export (Kartenmenü › Für Optolith "
             "exportieren) stehen in `Core/Optolith`. Die Zuordnung `data/optolith/mapping.json` erzeugt "
             "`tools/optolith/build_mapping.py` aus den Daten von Optolith 1.5.1 (nur IDs und Namen, keine Regeltexte).", "",
             "## Regeln beim Import", "",
             "- Kultur und Profession werden als Vorlage gesetzt; Talente und Kampftechniken als Abweichung davon, sodass die "
             "Werte dem Optolith-Stand entsprechen. Einträge der Profession, die der Held in Optolith nicht hat, entfallen; "
             "magische Handlungen der Profession (Flüche …) bleiben, weil Optolith sie nicht speichert.",
             "- Abgeschlossene Helden (Phase 3): Endstand als Erschaffung, AP über den Erfahrungsgrad hinaus als Gutschrift "
             "„Optolith-Import“ im Protokoll. Das Steigerungsprotokoll selbst kennt Optolith nicht.",
             "- Automatische Vor-/Nachteile der Spezies setzt die App selbst; Muttersprache = Sprache auf Stufe 4.",
             "- Lieblingszauber/-liturgie: Ausprägung nach Steigerungsfaktor („B-Zauber“), Zauber als Präzisierung.",
             "- Permanente AsP: Optolith kennt keine Bindungskosten; Verluste zählen zuerst als Bindung, nicht erfasste "
             "Bindungen als zurückgekauft (AsP bleiben gleich).",
             "- Ausrüstung ist nicht bezahlt; eine Geldbuchung gleicht an den Geldbeutel an. Angelegt wird die Rüstung mit dem "
             "höchsten RS, Waffen gelten als nicht geführt. Sozialstatus steht in den Notizen.",
             "- Zauber-/Liturgieerweiterungen (SA_414/SA_663) kommen an den Zauber (`enhancements`, über Zielzauber "
             "und Namen); in neueren Büchern umbenannte fehlen (Liste unten).",
             "- Nicht übernommen: Tiere (`pets`), Pakt, permanent verlorene LeP.", "",
             "## Export", "",
             "Nur der Endstand (Werte, AP-Summe), ohne Protokoll und Tiergefährten. Gegenstände mit Optolith-Vorlage werden an "
             "die Vorlage gebunden (Optolith ergänzt die Werte), andere als eigene Gegenstände ohne Kampfwerte.", "",
             "## Ohne Gegenstück in der App", "",
             "Optolith-Einträge ohne Gegenstück werden beim Import übersprungen und im Importbericht genannt. Professionen "
             "ohne Varianten in unseren Wiki-Daten (viele Magierakademien) verlieren die Variante.", ""]
    by_label = {}
    for label, oid, name in missing:
        by_label.setdefault(label, []).append(f"`{oid}` {name}")
    for label, items in by_label.items():
        lines += [f"### {label} ({len(items)})", "", ", ".join(items), ""]
    with open(REPORT, "w", encoding="utf-8") as f:
        f.write("\n".join(lines))


if __name__ == "__main__":
    main(sys.argv[1])
