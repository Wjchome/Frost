from pathlib import Path
import sys

from fontTools import subset
from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont

source = Path(sys.argv[1])
output = Path(sys.argv[2])
project = Path(__file__).resolve().parents[1]
assets = project / "Assets"
characters = set(range(0x20, 0x7f))
for extension in ("*.cs", "*.uxml"):
    for path in assets.rglob(extension):
        characters.update(map(ord, path.read_text(encoding="utf-8-sig")))

font = instantiateVariableFont(TTFont(source), {"wght": 450}, inplace=True)
for name_id, value in {
    1: "Frostline Sans",
    2: "Regular",
    3: "Frostline Sans Regular 1.0",
    4: "Frostline Sans Regular",
    6: "FrostlineSans-Regular",
    16: "Frostline Sans",
    17: "Regular",
}.items():
    for record in tuple(font["name"].names):
        if record.nameID == name_id:
            font["name"].setName(value, name_id, record.platformID, record.platEncID, record.langID)

options = subset.Options()
options.name_IDs = ["*"]
subsetter = subset.Subsetter(options=options)
subsetter.populate(unicodes=characters)
subsetter.subset(font)
font.save(output)
