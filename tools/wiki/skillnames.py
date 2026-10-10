"""Zuordnung von Talent- und Kampftechniknamen aus Fließtexten zu IDs (inkl. Schreibvarianten im Wiki)."""
import json
import os
import re

import books

DATA = os.path.join(books.ROOT, "src", "DSACharacterCreator.Content", "data")

# Schreibvarianten und Tippfehler im Wiki → offizieller Name
ALIASES = {
    "Menschenkenntnisse": "Menschenkenntnis",
    "Brett & Glücksspiel": "Brett- & Glücksspiel",
    "Brett- und Glücksspiel": "Brett- & Glücksspiel",
    "Götter und Kulte": "Götter & Kulte",
    "Sagen und Legenden": "Sagen & Legenden",
    "Fischen und Angeln": "Fischen & Angeln",
    "Malen und Zeichnen": "Malen & Zeichnen",
    "Boote und Schiffe": "Boote & Schiffe",
    "Bekehren und Überzeugen": "Bekehren & Überzeugen",
    "Heilkunde: Wunden": "Heilkunde Wunden",
    "Heilkunde: Gift": "Heilkunde Gift",
    "Heilkunde: Krankheiten": "Heilkunde Krankheiten",
    "Heilkunde: Seele": "Heilkunde Seele",
    "Zweihandhiebwaffe": "Zweihandhiebwaffen",
    "Zweihandschwert": "Zweihandschwerter",
    "Armbrust": "Armbrüste",
    "Bogen": "Bögen",
    "Schwert": "Schwerter",
    "Dolch": "Dolche",
    "Rauen": "Raufen",
    "Schleuder": "Schleudern",
    "Zweihänder": "Zweihandschwerter",
    "Lederarbeiten": "Lederbearbeitung",
    "Malen & Zeichen": "Malen & Zeichnen",
    "Heilkunde Krankheit": "Heilkunde Krankheiten",
    "Heilkunde Krankheuten": "Heilkunde Krankheiten",
    "Brett- & Glückspiel": "Brett- & Glücksspiel",
    "Sagen& Legenden": "Sagen & Legenden",
    "Menschenkenntnuis": "Menschenkenntnis",
}


def _load(folder):
    result = {}
    for name in os.listdir(os.path.join(DATA, folder)):
        with open(os.path.join(DATA, folder, name), encoding="utf-8") as f:
            for e in json.load(f):
                result[e["name"]] = e["id"]
    return result


SKILLS = _load("skills")
COMBAT = _load("combat-techniques")


def _norm(name: str) -> str:
    name = re.sub(r"\s+", " ", name.replace("’", "'")).strip().strip("*").strip()
    name = re.sub(r"\s*-\s*&", "- &", name)
    return ALIASES.get(name, name)


def skill_id(name: str) -> str | None:
    return SKILLS.get(_norm(name))


def combat_id(name: str) -> str | None:
    return COMBAT.get(_norm(name))


def value_list(text: str, lookup, unparsed: list[str]) -> list[dict]:
    """„Klettern 7, Körperbeherrschung 2*“ bzw. „Klettern +1, …“ → [{"id": …, "value": …}]."""
    result = []
    # Talentgruppen stehen im Wiki manchmal ohne Zeilenumbruch mitten in der Liste („Willenskraft 5 Natur: …“).
    text = re.sub(r"\b(?:Körper|Gesellschaft|Natur|Wissen|Handwerk)\s*:", ",", text)
    for part in re.split(r",|;(?![^(]*\))", text):
        part = part.strip().rstrip(".")
        if not part:
            continue
        for m in re.finditer(r"([A-ZÄÖÜa-zäöüß&’' :\-]+?)\s*\+?(\d+)\*?(?=\s|$)", part):
            name, value = m.group(1).strip(), int(m.group(2))
            target = lookup(name)
            if target is None:
                unparsed.append(part)
                break
            result.append({"id": target, "value": value})
        else:
            if not re.search(r"\d", part):
                unparsed.append(part)
    return result
