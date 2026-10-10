"""Hilfsfunktionen zum Auslesen des Ulisses DSA Regel-Wikis (https://dsa.ulisses-regelwiki.de/).

Nur Texte übernehmen, die im Wiki stehen (siehe CLAUDE.md). Seiten werden in einem lokalen Cache
abgelegt (nicht im Repo) und schonend mit Pause abgerufen.
"""
import html
import os
import re
import time
import unicodedata
import urllib.parse
import urllib.request

BASE = "https://dsa.ulisses-regelwiki.de/"
CACHE = os.environ.get("DSA_WIKI_CACHE", os.path.join(os.path.dirname(__file__), ".cache"))
USER_AGENT = "DSACharacterCreator data import (personal, non-commercial)"


def fetch(page: str, **params: str) -> str:
    """Lädt eine Wiki-Seite (aus dem Cache, sonst mit 0,7 s Pause aus dem Netz)."""
    query = "?" + urllib.parse.urlencode(params) if params else ""
    key = re.sub(r"[^\w.-]+", "_", page + query)
    path = os.path.join(CACHE, key)
    if not os.path.exists(path):
        os.makedirs(CACHE, exist_ok=True)
        url = BASE + urllib.parse.quote(page, safe="/._-") + query
        req = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
        data = urllib.request.urlopen(req, timeout=30).read()
        with open(path, "wb") as f:
            f.write(data)
        time.sleep(0.7)
    with open(path, "rb") as f:
        data = f.read()
    try:
        return data.decode("utf-8")
    except UnicodeDecodeError:  # einzelne Wiki-Seiten enthalten fehlerhafte Bytes
        return data.decode("utf-8", errors="replace")


def _text(fragment: str) -> str:
    fragment = re.sub(r"<br\s*/?>", "\n", fragment, flags=re.I)
    fragment = re.sub(r"<[^>]+>", "", fragment)
    lines = [re.sub(r"\s+", " ", line).strip() for line in html.unescape(fragment).split("\n")]
    return "\n".join(line for line in lines if line)


def fields(page_html: str) -> dict[str, str]:
    """Liest die Feldpaare <div class="spalte1">Label</div><div>Wert</div> einer Eintragsseite."""
    result = {}
    for label, value in re.findall(
            r'<div class="spalte1">(.*?)</div>\s*<div>(.*?)</div>', page_html, flags=re.S):
        result[_text(label).rstrip(":").strip()] = _text(value)
    return result


def publications(text: str, books: dict[str, str]) -> list[dict]:
    """„Regelwerk, Seite 188 ff.“ → [{"book": "grw", "page": 188}]. Unbekannte Bücher → KeyError."""
    result = []
    for line in text.split("\n"):
        m = re.match(r"(.+?),\s*Seiten?\s*(\d+)", line)
        name, page = (m.group(1).strip(), int(m.group(2))) if m else (line.strip(), None)
        if name not in books:
            raise KeyError(f"Unbekannte Publikation im Wiki: '{name}' – in data/books.json als wikiNames ergänzen")
        entry = {"book": books[name]}
        if page is not None:
            entry["page"] = page
        result.append(entry)
    return result


def slug(name: str) -> str:
    s = name.lower().replace("ä", "ae").replace("ö", "oe").replace("ü", "ue").replace("ß", "ss")
    s = unicodedata.normalize("NFKD", s).encode("ascii", "ignore").decode()
    return re.sub(r"[^a-z0-9]+", "-", s).strip("-")
