"""Liest die Regeldaten aus einer Dark-Aid-Bibliothek (libtde5rulesystems.so bzw. tde5rulesystems.dll).

Dark Aid bettet seine Daten als Qt-Ressourcen ein: Version 15 (Qt 5) zlib-, Version 16 (Qt 6) zstd-komprimiert, kleine
Dateien unkomprimiert (4 Byte Länge vor dem Inhalt). Gesucht werden alle diese Blöcke; JSON-Dateien mit „type“ und
„data“ sind Regeldaten.
"""
import json
import re
import subprocess
import zlib


def blocks(path):
    raw = open(path, "rb").read()
    # unkomprimiert: Länge (Big Endian) direkt vor „{“
    for m in re.finditer(rb"\{", raw):
        i = m.start()
        length = int.from_bytes(raw[i - 4:i], "big") if i >= 4 else 0
        if 2 <= length < 20_000_000 and raw[i + length - 1:i + length] in (b"}", b"\n"):
            yield raw[i:i + length]
    zstd = [m.start() for m in re.finditer(rb"\x28\xb5\x2f\xfd", raw)]
    for k, start in enumerate(zstd):
        end = zstd[k + 1] if k + 1 < len(zstd) else len(raw)
        out = subprocess.run(["zstd", "-d", "-c", "-q"], input=raw[start:end], capture_output=True).stdout
        if out:
            yield out
    if zstd:
        return
    for m in re.finditer(rb"\x78[\x01\x5e\x9c\xda]", raw):
        try:
            out = zlib.decompressobj().decompress(raw[m.start():m.start() + 20_000_000])
        except zlib.error:
            continue
        if len(out) > 100:
            yield out


def load(path):
    """Regeldaten nach Typ: {"skills": [{…, "_book": "regelwerk"}, …], …}; außerdem "_rulesystem" (Bücherliste)."""
    result = {}
    for data in blocks(path):
        try:
            d = json.loads(data)
        except (ValueError, UnicodeDecodeError):
            continue
        if isinstance(d, dict) and "rulebooks" in d and d.get("id", "").startswith("dsa5"):
            result.setdefault("_rulesystem", []).append(d)
        if not (isinstance(d, dict) and isinstance(d.get("type"), str) and isinstance(d.get("data"), list)):
            continue
        for e in d["data"]:
            if isinstance(e, dict) and isinstance(e.get("id"), str):
                e["_book"] = d.get("rulebook")
                result.setdefault(d["type"], []).append(e)
    return result
