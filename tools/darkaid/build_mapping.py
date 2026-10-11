"""Zuordnung Dark-Aid-ID → App-ID für Import und Export von Dark-Aid-Helden (*.tdc).

The Dark Aid (dsachargen.de) bettet seine Regeldaten in die Bibliothek libtde5rulesystems ein (siehe extract.py).
Übernommen werden nur IDs und Namen zum Abgleich, keine Regeltexte. Dark-Aid-IDs sind die Namen in Kleinbuchstaben ohne
Leer- und Sonderzeichen („praeziserschusswurf“); verglichen wird trotzdem über die Namen in den Dark-Aid-Daten.

Aufruf mit einer oder mehreren Bibliotheken (ältere zuerst, damit alte IDs erhalten bleiben):
    ./thedarkaid_15.AppImage --appimage-extract    # → squashfs-root/usr/lib/libtde5rulesystems.so.1
    tar xzf thedarkaid_16_linux_qt6.4.tar.gz       # → thedarkaid16/lib/libtde5rulesystems.so.1.0.0
    python3 tools/darkaid/build_mapping.py squashfs-root/usr/lib/libtde5rulesystems.so.1 \\
        thedarkaid16/lib/libtde5rulesystems.so.1.0.0

Braucht das Programm zstd (Dark Aid 16). Schreibt src/DSACharacterCreator.Content/data/darkaid/mapping.json und
docs/DARKAID.md (Regeln und was nicht zugeordnet ist).
"""
import importlib.util
import json
import os
import re
import sys

sys.path.insert(0, os.path.dirname(__file__))
import extract  # noqa: E402

# Namensvergleich wie bei Optolith (norm, similar, variant_names …)
_spec = importlib.util.spec_from_file_location(
    "optolith_mapping", os.path.join(os.path.dirname(__file__), "..", "optolith", "build_mapping.py"))
_opt = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(_opt)
DATA, ROOT, norm, similar, variant_names, enhancement_key, load_ours = (
    _opt.DATA, _opt.ROOT, _opt.norm, _opt.similar, _opt.variant_names, _opt.enhancement_key, _opt.load_ours)

OUT = os.path.join(DATA, "darkaid", "mapping.json")
REPORT = os.path.join(ROOT, "docs", "DARKAID.md")

# Dark-Aid-Name → Name in der App, wo die Schreibweise abweicht (normalisiert verglichen)
ALIASES = {
    "Menschen": "Mensch",
    "Zwerge": "Zwerg",
    "Fertigkeitsspezialisierung": "Fertigkeitsspezialisierung (Talente)",
    "Berufsgeheimnis": "Berufsgeheimnisse & Geheimwissen",
    "Immunität gegen Gift": "Immunität gegen (Gift)",
    "Immunität gegen Krankheit": "Immunität gegen (Krankheit)",
}

# Traditionen: Dark Aid führt sie als Auswahl der SF „Tradition“ (magisch) bzw. „Tradition“ (karmal)
TRADITION_ABILITIES = {"magischetradition", "geweihtentradition"}
TRADITION_ALIASES = {
    "Intuitiver Zauberer": "Intuitive Zauberer",
    "Hexe": "Hexen",
    "Elf": "Elfen",
    "Zauberbarde": "Zauberbarden",
    "Zaubertänzer (Rahkisa)": "Zaubertänzer",
    "Scharlatan": "Scharlatane",
    "Schelm": "Schelme",
    "Druide": "Druiden",
    "Animist": "Animisten",
    "Zauberalchimist": "Zauberalchimisten",
}


def compact(name):
    """Schreibweise der Dark-Aid-IDs: „Präziser Schuss/Wurf“ → „praeziserschusswurf“."""
    s = norm(name).replace("ä", "ae").replace("ö", "oe").replace("ü", "ue")
    return re.sub(r"[^a-z0-9]", "", s)


def base_name(name):
    """„Eulenhexe (Schwesternschaft: …)“ → „Eulenhexe“."""
    return re.sub(r" \(.*\)$", "", name)


class Matcher:
    def __init__(self, ours, extra_names=None):
        self.index = {}
        self.ids = {}
        for e in ours:
            for n in [e["name"]] + (extra_names(e) if extra_names else []):
                self.index.setdefault(norm(n), e["id"])
                self.ids.setdefault(compact(n), e["id"])

    def find(self, *names, darkaid_id=None):
        """Über die Namen, sonst über die Dark-Aid-ID (Einträge ohne Namen in den Daten)."""
        for n in names:
            if not n:
                continue
            k = norm(ALIASES.get(n, n))
            if k in self.index:
                return self.index[k]
        if darkaid_id and darkaid_id in self.ids:
            return self.ids[darkaid_id]
        return None


def merged(entries):
    """Einträge gleicher ID aus mehreren Büchern (Ergänzungen, z. B. neue Persönlichkeitsschwächen) zusammenführen; der
    erste mit Namen bestimmt Name und Buch."""
    result = {}
    for e in entries:
        if e["id"] not in result:
            result[e["id"]] = dict(e)
        else:
            for k, v in e.items():
                result[e["id"]].setdefault(k, v)
            if "name" in e and "name" not in result[e["id"]]:
                result[e["id"]]["name"] = e["name"]
    return list(result.values())


def names_of(e):
    extra = e.get("additionalnames") or []
    names = [e.get("name"), e.get("namemale"), e.get("namefemale")] + (extra if isinstance(extra, list) else [extra])
    return [n if isinstance(n, str) else n.get("name") for n in names if n]


def with_choice(names, ours, combat):
    """„Schnellladen (Bögen)“ → Schnellladen (Kampftechnik) mit Auswahl Bögen; „Klinge wider Geister“ → Klinge wider
    (Wesenheit) mit Präzisierung Geister; „Tierwandlung Biber (Nagerpfad, mittel)“ → Tierwandlung, Präzisierung."""
    for name in names:
        m = re.match(r"^(.*?) \((.*)\)$", name)
        candidates = [(m.group(1), m.group(2))] if m else []
        words = name.split()
        candidates += [(" ".join(words[:i]), " ".join(words[i:])) for i in range(len(words) - 1, 0, -1)]
        for base, choice in candidates:
            for e in ours:
                if norm(base_name(e["name"])) != norm(base):
                    continue
                cost = e.get("cost", {})
                options = [o["name"] for o in cost.get("options", []) or []]
                hit = next((o for o in options if compact(o) == compact(choice)), None)
                if hit:
                    return {"id": e["id"], "option": hit}
                target = e.get("optionTarget") or cost.get("target")
                if target == "combatTechnique" and compact(choice) in combat:
                    return {"id": e["id"], "option": combat[compact(choice)]}
                # Platzhalter wie „Klinge wider (Wesenheit)“: nur, wenn es den Namen bei uns nur einmal gibt
                same = [o for o in ours if norm(base_name(o["name"])) == norm(base)]
                if not options and not target and "(" in e["name"] and len(same) == 1:
                    return {"id": e["id"], "text": choice}
    return None


def load(paths):
    data = {}
    for path in paths:  # spätere Versionen ersetzen Einträge gleicher ID
        for t, entries in extract.load(path).items():
            if t == "_rulesystem":
                data[t] = entries
                continue
            known = {e["id"]: e for e in data.get(t, [])}
            for e in merged(entries):
                known[e["id"]] = e
            data[t] = list(known.values())
    return data


def match(entries, matcher, missing, label, names=names_of):
    result = {}
    for e in entries:
        found = matcher.find(*names(e), darkaid_id=e["id"])
        if found:
            result[e["id"]] = found
        else:
            missing.append((label, e["id"], (names(e) or [e["id"]])[0]))
    return result


def main(paths):
    d = load(paths)
    missing = []
    m = {"source": "The Dark Aid " + " / ".join(os.path.basename(p) for p in paths)}

    m["attributes"] = dict(zip(
        ["mut", "klugheit", "intuition", "charisma", "fingerfertigkeit", "gewandtheit", "konstitution", "koerperkraft"],
        ["MU", "KL", "IN", "CH", "FF", "GE", "KO", "KK"]))
    levels = json.load(open(os.path.join(DATA, "rules", "experience-levels.json"), encoding="utf-8"))
    m["experienceLevels"] = match(d["experiencelevel"], Matcher(levels), missing, "Erfahrungsgrad")

    # Rassen (Mittelländer, Tulamiden …) → Spezies und Würfeltabelle fürs Aussehen
    species = match(d["species"], Matcher(load_ours("species")), missing, "Spezies")
    tables = {}
    for f in os.listdir(os.path.join(DATA, "appearance")):
        for s in json.load(open(os.path.join(DATA, "appearance", f), encoding="utf-8")):
            tables.setdefault(s["species"], []).extend(v["name"] for v in s.get("variants", []))
    m["races"] = {}
    for e in d["speciesraces"]:
        sp = species.get(e.get("species"))
        if not sp:
            missing.append(("Spezies", e["id"], e.get("name", e["id"])))
            continue
        entry = {"species": sp}
        table = similar(names_of(e), [{"id": t, "name": t} for t in tables.get(sp, [])], threshold=0.5)
        if table:
            entry["table"] = table
        m["races"][e["id"]] = entry
    for e in d["species"]:  # Spezies ohne Rassen (Achaz …)
        if e["id"] in species and e["id"] not in m["races"]:
            m["races"][e["id"]] = {"species": species[e["id"]]}

    m["cultures"] = match(d["cultures"], Matcher(load_ours("cultures")), missing, "Kultur")

    # Professionen; eine gewählte Variante speichert Dark Aid als eigene Professions-ID
    professions = load_ours("professions")
    by_id = {p["id"]: p for p in professions}
    pm = Matcher(professions)
    m["professions"] = {}
    for e in d["professions"]:
        names = names_of(e)
        found = (pm.find(*names, darkaid_id=e["id"]) or similar(names, professions)
                 or pm.find(*[base_name(n) for n in names]))
        if not found:
            missing.append(("Profession", e["id"], names[0] if names else e["id"]))
            continue
        m["professions"][e["id"]] = {"id": found}
        ours_variants = by_id[found].get("variants", [])
        vm = Matcher([{"id": v["id"], "name": v["name"]} for v in ours_variants], lambda v: variant_names(v["name"]))
        for v in e.get("variants", []) or []:
            vnames = names_of(v) + [n.replace(" oder ", "/") for n in names_of(v)]
            variant = vm.find(*vnames, darkaid_id=v["id"]) or similar(vnames, ours_variants)
            if variant:
                m["professions"].setdefault(v["id"], {"id": found, "variant": variant})
            else:
                missing.append(("Professionsvariante", v["id"], f"{names_of(v)[0]} ({by_id[found]['name']})"))

    m["skills"] = match(d["skills"], Matcher(load_ours("skills")), missing, "Talent")
    m["combatTechniques"] = match(d["combattechniques"], Matcher(load_ours("combat-techniques")), missing, "Kampftechnik")
    # Zauber samt magischer Handlungen (Flüche, Animistenkräfte …), Liturgien samt Segnungen
    spells = match(d["spells"], Matcher(load_ours("spells", {"spell", "ritual", "cantrip", "action"})), missing, "Zauber")
    spells.update(match(d["chants"], Matcher(load_ours("spells", {"liturgy", "ceremony", "blessing"})), missing, "Liturgie"))
    m["spells"] = spells

    base = lambda e: [base_name(e["name"])]  # „Leonal (Khorrzu)“ auch als „Leonal“
    m["languages"] = match(d["languages"], Matcher(load_ours("languages"), base), missing, "Sprache")
    m["scripts"] = match(d["scripts"], Matcher(load_ours("scripts"), base), missing, "Schrift")

    abilities = load_ours("special-abilities")
    tm = Matcher(abilities)
    # „Tradition (Gildenmagier)“ auch über „gildenmagier“, „hexen“ → „hexe“, „phexkirche“ → „phex“
    inner = {}
    for a in abilities:
        if a["name"].startswith("Tradition ("):
            key = compact(a["name"][len("Tradition ("):-1])
            for k in (key, re.sub(r"(kirche|kult|en|n|e)$", "", key)):
                inner.setdefault(k, a["id"])
    m["traditions"] = {}
    for t in d["magicaltraditions"] + d["blessedtraditions"]:
        names = names_of(t)
        candidates = [f"Tradition ({TRADITION_ALIASES.get(n, n)})" for n in names] + \
                     [f"Tradition ({n}kirche)" for n in names] + [f"Tradition ({n}-Kult)" for n in names]
        found = (tm.find(*candidates) or inner.get(t["id"]) or inner.get(re.sub(r"(en|n|e)$", "", t["id"]))
                 or similar([f"Tradition {n}" for n in names],
                            [a for a in abilities if a["name"].startswith("Tradition (")], 0.5))
        if found:
            m["traditions"][t["id"]] = found
        else:
            missing.append(("Tradition", t["id"], names[0] if names else t["id"]))

    # Vor-/Nachteile (bei Dark Aid beide unter „disadvantages“), Sonderfertigkeiten und Stab-/Objektzauber.
    # „Schnellladen (Bögen)“ ist in der App eine Auswahl: {"id", "option"} bzw. {"id", "text"} (Präzisierung).
    ours_traits = load_ours("advantages") + load_ours("disadvantages")
    combat = {compact(c["name"]): c["id"] for c in load_ours("combat-techniques")}
    m["activatables"] = {}
    for label, entries, ours in [("Vor-/Nachteil", d["disadvantages"], ours_traits),
                                 ("Sonderfertigkeit", d["specialabilities"], abilities),
                                 ("Objektzauber", d.get("enchantments", []), abilities)]:
        matcher = Matcher(ours)
        for e in entries:
            if e["id"] in TRADITION_ABILITIES:
                continue
            names = names_of(e)
            found = matcher.find(*names, darkaid_id=e["id"])
            if not found:
                found = with_choice(names, ours, combat)
            if not found:
                # „Ungeheuer-Taktik“ = „Ungeheuer-Taktik (einzelne Spezies)“, „Hruruzat“ = „Hruruzat-Stil“ (eindeutig)
                hits = {o["id"] for o in ours for n in names
                        if norm(base_name(o["name"])) == norm(n) or norm(o["name"]) == norm(n + "-Stil")}
                found = hits.pop() if len(hits) == 1 else None
            if found:
                m["activatables"].setdefault(e["id"], found)
            else:
                missing.append((label, e["id"], (names or [e["id"]])[0]))

    # Art der Auswahl je Eintrag (für den Export): „string“, „stringwithcost“, „terrain“, „skillspecialization“,
    # „specializedknowledge“, „tradesecret“ oder „ruleelement“ (dann mit den möglichen Typen: „ruleelement:language“)
    m["variants"] = {}
    for e in d["disadvantages"] + d["specialabilities"] + d.get("enchantments", []):
        v = e.get("variants")
        if isinstance(v, dict) and v.get("type"):
            types = v.get("ruleelementtypes")
            types = types if isinstance(types, list) else [types] if types else []
            m["variants"][e["id"]] = ":".join([v["type"]] + types)
    m["enchantments"] = sorted(e["id"] for e in d.get("enchantments", []))
    m["blessedTraditions"] = sorted(e["id"] for e in d["blessedtraditions"])

    # Namen der übrigen Auswahlen (Gottheit, Gift, Merkmal, Element, Berufsgeheimnis …) als Präzisierung
    m["names"] = {}
    for t, typ in [("gods", "god"), ("poisons", "poison"), ("spellproperties", "spellproperty"),
                   ("tradesecrets", "tradesecret"), ("chantaspects", "chantaspect"), ("diseases", "disease"),
                   ("animistpatrons", "animistpatron")]:
        m["names"][typ] = {e["id"]: names_of(e)[0] for e in d.get(t, []) if names_of(e)}
    # Elemente stehen nicht in eigenen Daten
    m["names"]["alchemicalelement"] = {k: k.capitalize() for k in ["feuer", "wasser", "luft", "erz", "humus", "eis"]}

    # Zauber- und Liturgieerweiterungen je Zauber: Dark-Aid-Zauber → Dark-Aid-Erweiterung → Erweiterung der App
    ours_spells = {e["id"]: e for e in load_ours("spells")}
    m["enhancements"] = {}
    for e in d["spells"] + d["chants"]:
        spell = m["spells"].get(e["id"])
        if not spell or not e.get("enhancements"):
            continue
        ours = {enhancement_key(x["name"]): x["id"] for x in ours_spells[spell].get("enhancements", [])}
        result = {}
        for x in e["enhancements"]:
            found = ours.get(enhancement_key(x.get("name", "")))
            if found:
                result[x["id"]] = found
            else:
                missing.append(("Erweiterung", f"{e['id']}/{x['id']}", f"{x.get('name', x['id'])} ({names_of(e)[0]})"))
        if result:
            m["enhancements"][e["id"]] = result

    # Ausrüstung: „meleeweapon/langschwert“ → Gegenstand der App
    weapons, armor, items = load_ours("weapons"), load_ours("armor"), load_ours("items")
    m["items"] = {}
    for t, typ, ours in [("meleeweapons", "meleeweapon", weapons + items), ("rangedweapons", "rangedweapon", weapons + items),
                         ("shields", "shield", weapons + armor + items), ("armor", "armor", armor + items),
                         ("equipment", "equipment", items + weapons + armor)]:
        im = Matcher(ours)
        for e in d.get(t, []):
            found = im.find(*names_of(e))
            if found:
                m["items"][f"{typ}/{e['id']}"] = found

    # Bücher (Dark-Aid-ID → Buch der App), für die Quellenauswahl
    books = json.load(open(os.path.join(DATA, "books.json"), encoding="utf-8"))
    bm = Matcher([{"id": b["id"], "name": b["name"]} for b in books])
    roman = {" 2": " II", " 3": " III", " 1": ""}
    m["rulebooks"] = {}
    for system in d.get("_rulesystem", []):
        for b in system["rulebooks"]:
            name = b["name"].replace("DSA5 ", "")
            alt = re.sub(r" (\d)$", lambda x: roman.get(x.group(0), x.group(0)), name)
            found = bm.find(name, alt, name.replace("Streitenen", "Streitenden"))
            if found:
                m["rulebooks"][b["id"]] = found

    m["unmapped"] = {oid: name for _, oid, name in missing}

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8") as f:
        json.dump(m, f, ensure_ascii=False, indent=1)
        f.write("\n")
    write_report(missing)
    print(f"{OUT}: " + ", ".join(f"{k} {len(v)}" for k, v in m.items() if isinstance(v, dict)))
    print(f"{len(missing)} ohne Zuordnung, siehe {REPORT}")


def write_report(missing):
    lines = ["# Dark-Aid-Import und -Export", "",
             "Import (Hauptmenü › Importieren, erkennt `.tdc`-Dateien selbst) und Export (Kartenmenü › Für Dark Aid "
             "exportieren) stehen in `Core/DarkAid`. Die Zuordnung `data/darkaid/mapping.json` erzeugt "
             "`tools/darkaid/build_mapping.py` aus den in Dark Aid 15/16 eingebetteten Daten (nur IDs und Namen, keine "
             "Regeltexte).", "",
             "## Regeln beim Import", "",
             "- Kultur und Profession werden als Vorlage gesetzt (eine gewählte Professionsvariante speichert Dark Aid als "
             "eigene Professions-ID); Talente und Kampftechniken als Abweichung davon, sodass die Werte dem Dark-Aid-Stand "
             "entsprechen. Einträge der Profession, die der Held nicht hat, entfallen.",
             "- Muttersprachen (`mothertongues`) sind die kostenlosen Sprachen der Kultur; Dialekte werden zur Präzisierung.",
             "- Erschaffene Helden (`iscreated`): Endstand als Erschaffung, die AP-Boni (`xp.bonuses`) als Gutschriften im "
             "Protokoll. Ein Steigerungsprotokoll kennt Dark Aid nicht.",
             "- `basevalues`: `improved` = zugekaufte LeP/AsP/KaP, `bought` = zurückgekaufte permanente Verluste. Die "
             "Verluste selbst (`losses`, z. B. Bindung des Stabes) ergeben sich in der App aus den Sonderfertigkeiten; "
             "andere Verlustquellen (Langer Tanz) nennt der Importbericht.",
             "- Vor- und Nachteile stehen bei Dark Aid beide unter `disadvantages`; Auswahlen, die Dark Aid als eigenen "
             "Eintrag führt („Schnellladen (Bögen)“, „Klinge wider Geister“), werden zu Auswahl bzw. Präzisierung.",
             "- Ausrüstung ist nicht bezahlt; eine Geldbuchung gleicht an den Geldbeutel (`purse`, Kreuzer) an. Größe in "
             "1/10000 Schritt, Gewicht in 1/400000 Stein.",
             "- Selbst angelegte Einträge (eigene Waffen, eigene Sonderfertigkeiten mit GUID) kommen als eigene "
             "Gegenstände bzw. werden übersprungen; Zaubererweiterungen der alten Form „Zauber (Erweiterung)“ werden "
             "über den Namen erkannt.", "",
             "## Export", "",
             "Endstand mit Erfahrungsgrad und AP-Gutschriften als `xp.bonuses`, ohne Steigerungsprotokoll und "
             "Tiergefährten. Eigene Gegenstände ohne Gegenstück in Dark Aid fehlen. Zaubererweiterungen, die Dark Aid nicht "
             "kennt, werden wie in älteren Dark-Aid-Fassungen eigene Sonderfertigkeiten „Zauber (Erweiterung)“.", "",
             "## Abgleich mit echten Helden", "",
             "`DSA_DARKAID=<ordner>` für `DarkAidRealHeroTests`: Werte (Eigenschaften, Talente, Kampftechniken, Zauber) "
             "und Rundweg Export → Import. Stand 2026-10-11 mit 46 Helden: alle Werte gleich. Die AP-Summe der App weicht bei "
             "einigen Helden ab: Klingensturm kostet in Dark Aid 25 AP, im Wiki 30 AP (das Wiki gilt); Immunität gegen "
             "(Gift) hat in der App einen selbst einzutragenden AP-Wert; bei Hexen (+10), einigen Graumagiern und Geoden "
             "ist die Ursache offen (Dark Aid speichert keine AP-Summe zum Vergleich).", "",
             "## Ohne Gegenstück in der App", ""]
    by_label = {}
    for label, oid, name in missing:
        by_label.setdefault(label, []).append(f"`{oid}` {name}")
    for label, items in by_label.items():
        lines += [f"### {label} ({len(items)})", "", ", ".join(items), ""]
    with open(REPORT, "w", encoding="utf-8") as f:
        f.write("\n".join(lines))


if __name__ == "__main__":
    main(sys.argv[1:])
