"""Gewichte und Preise aus Wiki-Text in Zahlen umrechnen.

Wiki „Währungen“: 1 Dukat = 10 Silbertaler, 1 Heller = 0,1 S, 1 Kreuzer = 0,01 S.
Wiki „Maßeinheiten“: 1 Stein = 1 kg, 1 Unze = 25 g, 1 Skrupel = 1 g, 1 Quader = 1 t.
"""
import re

COINS = [(r"D(?:ukaten?)?", 10), (r"S(?:ilbertaler)?", 1), (r"H(?:eller)?", 0.1), (r"K(?:reuzer)?", 0.01)]
WEIGHTS = [(r"Stn|Stein", 1), (r"Unzen?|Unz\.?", 0.025), (r"Skrupel|Skr\.?", 0.001), (r"Quader", 1000)]
NUM = r"(\d+(?:\.\d{3})*(?:,\d+)?|\d+(?:\.\d+)?)"


def _number(text):
    if re.fullmatch(r"\d+\.\d{3}(?:\.\d{3})*", text):  # Tausenderpunkt „1.500“
        return float(text.replace(".", ""))
    return float(text.replace(".", "").replace(",", ".")) if "," in text else float(text)


def _amount(text, units):
    """Summe aller „Zahl Einheit“-Paare („1 D 5 S“); None bei Spannen („5–10 S“) oder ohne Treffer."""
    t = text.replace(" ", " ").strip()
    if re.search(r"\d\s*[-–]\s*\d", t):
        return None
    t = re.split(r"\s*[/(]|\s+pro\s|\s+je\s", t)[0]  # „12 S / 1 I“, „34 S (gesamt)“: erster Wert
    total, found = 0.0, False
    for m in re.finditer(NUM + r"\s*([A-Za-zäöü.]+)", t):
        unit = m.group(2)
        factor = next((f for pattern, f in units if re.fullmatch(pattern, unit)), None)
        if factor is None:
            return None
        total += _number(m.group(1)) * factor
        found = True
    rest = re.sub(NUM + r"\s*[A-Za-zäöü.]+", "", t).strip(" ,+")
    if not found or rest:
        return None
    return round(total, 4)


def tidy(value):
    """10.0 → 10 (schlichtere JSON-Ausgabe)."""
    return int(value) if value is not None and float(value).is_integer() else value


def numbers(entry):
    """Ergänzt weightStone/priceSilver aus den Textfeldern weight/price."""
    if (w := weight(entry.get("weight"))) is not None:
        entry["weightStone"] = tidy(w)
    if (p := price(entry.get("price"))) is not None:
        entry["priceSilver"] = tidy(p)
    return entry


def price(text):
    if text is None:
        return None
    if text.strip().lower() in ("gratis", "kostenlos", "0"):
        return 0
    return _amount(text, COINS)


def weight(text):
    if text is None:
        return None
    return _amount(text, WEIGHTS)


if __name__ == "__main__":
    for t in ["10 S", "0,5 S", "575,5 Silbertaler", "2 D", "1 D 5 S", "3 H", "12 S / 1 I", "34 S (gesamt)", "5-10 S",
              "unterschiedlich", "1.500 S", "0,25 Stn", "2 Stein", "50 Skrupel", "1 Unze"]:
        print(t, "→", price(t), weight(t))
