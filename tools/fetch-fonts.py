"""Manrope'un statik ağırlıklarını Google Fonts'tan indirip TTF olarak kaydeder.

Kullanım:  python -I tools/fetch-fonts.py
Gereksinim: fontTools (pip install fonttools)
"""
import io
import pathlib
import re
import sys
import urllib.request

from fontTools.ttLib import TTFont

ROOT = pathlib.Path(__file__).resolve().parent.parent
OUT = ROOT / "src" / "CamAda" / "Assets" / "Fonts"
# Eski bir Chrome kimliği: Google Fonts bu tarayıcıya ağırlık başına tek, tam karakter setli WOFF verir.
USER_AGENT = (
    "Mozilla/5.0 (Windows NT 6.1) AppleWebKit/534.30 (KHTML, like Gecko) "
    "Chrome/12.0.742.122 Safari/534.30"
)
WEIGHTS = {400: "Regular", 600: "SemiBold", 700: "Bold", 800: "ExtraBold"}
TURKISH = "şŞğĞıİçÇöÖüÜ"
OFL_URL = "https://raw.githubusercontent.com/google/fonts/main/ofl/manrope/OFL.txt"


def fetch(url: str) -> bytes:
    request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
    with urllib.request.urlopen(request, timeout=30) as response:
        return response.read()


def main() -> int:
    OUT.mkdir(parents=True, exist_ok=True)
    for weight, style in WEIGHTS.items():
        css = fetch(f"https://fonts.googleapis.com/css2?family=Manrope:wght@{weight}").decode("utf-8")
        match = re.search(r"url\((https://[^)]+)\)", css)
        if not match:
            print(f"HATA: {weight} için yazı tipi adresi bulunamadı", file=sys.stderr)
            return 1
        font = TTFont(io.BytesIO(fetch(match.group(1))))
        font.flavor = None  # WOFF -> TTF
        cmap = font.getBestCmap()
        missing = [ch for ch in TURKISH if ord(ch) not in cmap]
        if missing:
            print(f"HATA: Manrope {style} içinde eksik karakterler: {''.join(missing)}", file=sys.stderr)
            return 1
        target = OUT / f"Manrope-{style}.ttf"
        font.save(target)
        print(f"{target.name} kaydedildi (ağırlık {font['OS/2'].usWeightClass})")

    (OUT / "OFL.txt").write_bytes(fetch(OFL_URL))
    print("OFL.txt kaydedildi")
    print("Türkçe karakterler tamam")
    return 0


if __name__ == "__main__":
    sys.exit(main())
