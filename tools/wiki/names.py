"""Typische Namen einer Kultur (Wiki-Feld „Typische Namen“) → Namenslisten zum Auswürfeln.

Nur Listen mit einzelnen Namen werden übernommen (keine Rufnamen mit Übersetzung, keine Stämme oder Adelsfamilien).
Die Namensbildung (Sohn des …, ibn …, -son …) steht im Wiki als Fließtext und ist hier je Kultur festgelegt.
"""
import re

# Wiki-Bezeichnung → Liste
KINDS = {
    "männlich": "male",
    "weiblich": "female",
    "geschlechtsunabhängig": "any",
    "achaznamen": "any",
    "familiennamen": "family",
    "nachnamen": "family",
    "sippennamen": "family",
    "sippenname": "family",
    "sippen": "family",
}

# Namensbildung laut Wiki-Text der Kultur: (männlich, weiblich); {first} Vorname, {family} Familienname,
# {father}/{mother} Vorname eines Elternteils, {parent} Vater oder Mutter.
FORMATS = {
    # „Sohn des …“ / „Tochter der …“
    "ambosszwerge": (["{first} Sohn des {father}"], ["{first} Tochter der {mother}"]),
    "brillantzwerge": (["{first} Sohn des {father}"], ["{first} Tochter der {mother}"]),
    "erzzwerge": (["{first} Sohn des {father}"], ["{first} Tochter der {mother}"]),
    "wildzwerge": (["{first} Sohn des {father}"], ["{first} Tochter der {mother}"]),
    # „ibn bei Männern und saba oder -sunni bei Frauen mit dem Namen des Vaters oder der Mutter“
    "aranier": (["{first} ibn {parent}"], ["{first} saba {parent}", "{first} {parent}sunni"]),
    "mhanadistani": (["{first} ibn {parent}"], ["{first} saba {parent}", "{first} {parent}sunni"]),
    "novadis": (["{first} ben {parent}"], ["{first} saba {parent}", "{first} {parent}sunni"]),
    "ferkinas": (["{first} iban {parent}"], ["{first} sabu {parent}"]),
    # „Männer erhalten als Nachnamen den Vornamen des Vaters, Frauen den der Mutter … bren … brai“
    "gjalsker": (["{first} bren {father}"], ["{first} brai {mother}"]),
    # „aus dem elterlichen Vornamen und, je nach Geschlecht, -son beziehungsweise -dottir“ (Eirk Halmarson, Ragna Frenjasdottir)
    "thorwaler": (["{first} {parent}son"], ["{first} {parent}sdottir"]),
}

# Kulturen ohne eigene Listen, die laut Wiki Namen anderer Kulturen verwenden
BORROWED = {
    "nordaventurier": ["culture.mittelreicher", "culture.nivesen", "culture.norbarden", "culture.thorwaler"],
    "svellttaler": ["culture.mittelreicher"],
    "koboldweltler": ["culture.mittelreicher"],
}


def _is_name(item):
    return bool(item) and len(item) <= 30 and not re.search(r"[().:;]| oder | je nach |\bz\. ?B\b", item) \
        and len(item.split()) <= 3


def parse(slug, text):
    """Gibt das „names“-Objekt einer Kultur zurück oder None."""
    lists = {}
    if text:
        # Fortsetzungszeilen („Marnax,\nPogolosch“) anhängen
        lines = []
        for line in text.split("\n"):
            if lines and lines[-1].rstrip().endswith(","):
                lines[-1] += " " + line.strip()
            else:
                lines.append(line)
        for line in lines:
            m = re.match(r"\s*#?\s*([^:#]+?)\s*:\s*(.+)$", line)
            if not m:
                continue
            kind = KINDS.get(m.group(1).strip().lower())
            if not kind:
                continue
            items = [i.strip() for i in m.group(2).split(",")]
            if not all(_is_name(i) for i in items if i):
                continue
            lists.setdefault(kind, []).extend(i for i in items if i)
    names = {k: v for k, v in lists.items()}
    if slug in FORMATS:
        names["maleFormats"], names["femaleFormats"] = FORMATS[slug]
    if slug in BORROWED and not lists:
        names["from"] = BORROWED[slug]
    if not (lists or "from" in names):
        return None
    return names
