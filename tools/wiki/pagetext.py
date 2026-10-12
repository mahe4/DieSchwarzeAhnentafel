"""Fließtext einer handgeschriebenen Wiki-Artikelseite (Spezies, Kulturen, Professionen)."""
import html
import re

import wiki


def article_lines(page: str) -> list[str]:
    h = wiki.fetch(page)
    t = re.sub(r"<script.*?</script>|<style.*?</style>", "", h, flags=re.S)
    t = re.sub(r"<br\s*/?>", "\n", t, flags=re.I)
    t = re.sub(r"</(p|div|li|tr|h\d)>", "\n", t, flags=re.I)
    t = re.sub(r"<[^>]+>", "", t)
    t = html.unescape(t)
    lines = [re.sub(r"\s+", " ", l).strip() for l in t.split("\n")]
    lines = [l for l in lines if l]
    start = next(i for i, l in enumerate(lines) if l == "DSA Regel-Wiki")
    start = next(i for i in range(start, len(lines)) if lines[i] == "Navigation überspringen") + 1
    end = max(i for i, l in enumerate(lines) if l == "Navigation überspringen")
    return lines[start:end]


def article_links(page: str) -> list[tuple[str, str]]:
    """(Ziel, Linktext) aller Inhaltslinks einer Artikelseite, ohne Menü- und Fußzeilenlinks."""
    from urllib.parse import unquote
    h = wiki.fetch(page)
    found = re.findall(r"<a[^>]*href=[\"']?([^\"'>\s]+)[^>]*>(.*?)</a>", h, flags=re.S)
    menu = {"regeln.html", "spezies.html", "kulturen.html", "professionen.html", "sonderfertigkeiten.html",
            "vor-und-nachteile.html", "magie.html", "goetterwirken.html", "ruestkammer.html", "bestiarium.html",
            "herbarium.html", "GifteundKrankheiten.html", "WdV18.html"}
    result = []
    for url, label in found:
        url = unquote(html.unescape(url))
        label = re.sub(r"\s+", " ", html.unescape(re.sub("<[^>]+>", "", label))).strip()
        if url.startswith(("#", "/", "http", "assets", "mailto", "&")) or "#" in url or url in menu or url == page:
            continue
        result.append((url, label))
    return result


def labeled(lines: list[str], labels: list[str], once: tuple[str, ...] = ()) -> dict[str, str]:
    """Ordnet Zeilen den Feldern zu: „Label: Wert“ oder „Label:“ gefolgt von Wertzeilen bis zum nächsten Label.

    Mehrzeilige Werte werden mit Zeilenumbruch verbunden; Zeilen mit „#“ (Unterpunkte im Wiki) bleiben erhalten.
    Labels in `once` gelten nur beim ersten Auftreten; danach ist die Zeile Text des laufenden Felds (z. B. „Wirkung:“
    eines Gifts in einer Tabelle unter dem AP-Wert).
    """
    result: dict[str, list[str]] = {}
    current = None
    for line in lines:
        hit = next((l for l in labels if line == l or line.startswith(l + ":") or line.startswith(l + " ")
                    and line[len(l):].lstrip().startswith(":")), None)
        if hit in once and hit in result and current is not None:
            hit = None
        if hit is not None:
            current = hit
            rest = line[len(hit):].lstrip().removeprefix(":").strip()
            result[current] = [rest] if rest else []
        elif current is not None:
            result[current].append(line)
    return {k: "\n".join(v).strip() for k, v in result.items()}


def publication_text(lines: list[str]) -> str:
    """Alles ab „Publikation…“ bis zum Ende (oder „Errata“); leer, wenn die Seite keine Quelle nennt."""
    start = next((i for i, l in enumerate(lines) if l.startswith("Publikation")), None)
    if start is None:
        return ""
    first = lines[start].split(":", 1)[1].strip() if ":" in lines[start] else ""
    rest = []
    for l in lines[start + 1:]:
        if l.startswith(("Errata", "Anmerkung", "Hinweis")):
            break
        rest.append(l)
    return "\n".join(([first] if first else []) + rest)


def parse_publications(text: str, books: dict[str, str]) -> list[dict]:
    """„Regelwerk Seite 88“, „Orks - Hauer & Schwarzer Pelz, Seite 19“, mehrere Zeilen oder kommagetrennt."""
    result = []
    for name, page in split_publications(text):
        if name not in books:
            raise KeyError(f"Unbekannte Publikation im Wiki: '{name}' – in data/books.json als wikiNames ergänzen")
        entry = {"book": books[name]}
        if page is not None:
            entry["page"] = page
        if entry not in result:
            result.append(entry)
    return result


def split_publications(text: str) -> list[tuple[str, int | None]]:
    result = []
    for line in text.replace(";", "\n").split("\n"):
        line = line.strip().strip("|").strip().replace("Seie ", "Seite ").replace("Seite, ", "Seite ")
        line = re.sub(r"Seiten,?\s*(\d+)(?:,\s*\d+)+", r"Seite \1", line)  # „Seiten 213, 271“: erste Seite
        if not line:
            continue
        # „Kodex des Schwertes, 287“ / „Helden der Echsensümpfe, 82“: Seitenzahl ohne „Seite“
        line = re.sub(r",\s*(\d+)(?=\s*$|\s*,)", r", Seite \1", line)
        found = list(re.finditer(r"\s*(.+?),?\s+Seiten?\s*(\d+)(?:\s*(?:ff?\.?|–\s*\d+|-\s*\d+))?(?:\s*[(\[][^)\]]*[)\]])?\s*(?:,|$)", line))
        if found:
            result += [(m.group(1).strip().rstrip(","), int(m.group(2))) for m in found]
        else:
            result.append((line, None))
    return result
