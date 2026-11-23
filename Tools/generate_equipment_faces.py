"""Render the expedition's equipment portraits into the existing CardArt folder."""
from pathlib import Path

from PIL import Image, ImageDraw


OUTPUT = Path(__file__).resolve().parents[1] / "Assets" / "Resources" / "CardArt"

PALETTES = {
    "ink": ("#322740", "#6e4979", "#dbaddc"),
    "oil": ("#482c35", "#8e4c46", "#efaa75"),
    "flux": ("#452e33", "#825937", "#ffcf80"),
    "calibration": ("#233b42", "#477373", "#bde8c9"),
    "brace": ("#263c4c", "#497384", "#a9dee6"),
    "powder": ("#30334c", "#58688f", "#c0cef0"),
    "heater": ("#512c32", "#975437", "#ffd48b"),
    "screen": ("#213c45", "#3c747c", "#bce4df"),
    "frostknife": ("#283b56", "#536f94", "#c9edf5"),
    "arccut": ("#372d43", "#76506b", "#ffd196"),
    "barbs": ("#38323c", "#695b48", "#e0c18b"),
    "embers": ("#4d3035", "#924e43", "#ffd2a1"),
}


def bottle(draw, liquid, glass, shine):
    outer = [(38,20),(37,35),(29,43),(26,52),(28,64),(36,69),(68,69),
             (77,62),(79,50),(74,41),(65,35),(64,20)]
    draw.polygon(outer, fill=glass, outline="#171830", width=2)
    draw.polygon([(31,48),(40,45),(52,48),(70,45),(74,50),(73,61),
                  (67,65),(37,65),(32,61)], fill=liquid)
    draw.line([(35,51),(34,57),(37,61)], fill=shine, width=2)
    draw.line([(34,43),(39,39),(42,31),(42,26)], fill=shine, width=2)
    draw.rounded_rectangle((36,17,66,25), radius=2, fill="#cf9a64", outline="#302339", width=2)
    draw.line((40,19,61,19), fill="#f8d395", width=2)


def portrait(name):
    dark, middle, light = PALETTES[name]
    image = Image.new("RGB", (105, 79), dark)
    draw = ImageDraw.Draw(image)
    draw.ellipse((12,-15,94,82), fill=middle)
    draw.ellipse((14,-12,92,79), outline=light)
    draw.line([(1,63),(23,57),(82,57),(104,64)], fill=middle)
    draw.ellipse((25,62,83,74), fill=dark)
    draw.line([(3,9),(14,9),(14,4)], fill=light)
    draw.line([(101,9),(91,9),(91,4)], fill=light)
    draw.line([(3,69),(14,69),(14,74)], fill=light)
    draw.line([(101,69),(91,69),(91,74)], fill=light)
    for x, y in ((21,22), (84,31), (16,48), (88,47)):
        draw.line((x-2,y,x+2,y), fill=light)
        draw.line((x,y-2,x,y+2), fill=light)

    if name == "ink":
        bottle(draw, "#57306d", "#8f729e", "#e7c1f0")
        draw.polygon([(47,9),(59,13),(60,19),(47,19)], fill="#e5b376", outline="#302339")
        draw.polygon([(50,50),(45,57),(47,62),(52,64),(57,62),(59,57),(54,50)],
                     fill="#d9a6eb", outline="#251b3b")
        draw.line([(61,49),(68,40),(74,34)], fill="#f1d2fb", width=2)
    elif name == "oil":
        bottle(draw, "#b95037", "#e28a5b", "#ffdf9f")
        draw.polygon([(41,59),(49,57),(54,49),(61,47),(66,50),(62,56),(54,61),(44,63)],
                     fill="#ffc273", outline="#7b322c")
        draw.line([(63,48),(66,43),(70,44)], fill="#b2d893", width=2)
    elif name == "powder":
        draw.polygon([(39,22),(56,22),(65,28),(66,36),(76,51),(77,60),(69,67),
                      (34,67),(26,60),(29,48),(40,33)], fill="#b6b0a6", outline="#23293d", width=2)
        draw.line([(38,24),(48,32),(63,29)], fill="#eee0bf", width=4)
        draw.line([(38,35),(50,38),(66,34),(72,39)], fill="#4b4a64", width=3)
        draw.polygon([(40,53),(48,59),(64,58),(56,49)], fill="#687fb7")
        draw.line([(37,49),(47,53),(61,45)], fill="#e5eafa", width=2)
    elif name in ("flux", "heater"):
        hot = name == "heater"
        draw.rounded_rectangle((28,24,77,67), radius=7, fill="#5a3a37", outline="#251b2c", width=2)
        draw.rounded_rectangle((34,29,71,60), radius=4, fill="#ffae69" if hot else "#ddad6d",
                               outline="#6f4541", width=2)
        draw.rectangle((25,22,80,29), fill="#c18c68", outline="#332333", width=2)
        draw.rectangle((27,61,78,68), fill="#a47055", outline="#332333", width=2)
        for x in (37, 51, 65):
            draw.line((x,57,x,36), fill="#7e433c", width=3)
            draw.line((x+1,57,x+1,40), fill="#ffdd85", width=2)
        if hot:
            draw.arc((35,3,70,33), 190, 350, fill="#ffda91", width=3)
            draw.line([(57,17),(61,9),(58,4)], fill="#ffe2a9", width=3)
        else:
            draw.line([(36,22),(36,13),(43,13),(43,22)], fill="#f6d18d", width=3)
            draw.line([(62,22),(62,13),(69,13),(69,22)], fill="#f6d18d", width=3)
            draw.line([(43,13),(52,7),(52,16),(63,10)], fill="#fff0b2", width=3)
    elif name in ("brace", "screen"):
        shield = [(52,10),(77,23),(76,45),(68,59),(52,70),(36,61),(29,48),(27,23)]
        draw.polygon(shield, fill="#b4d7dd", outline="#192d41", width=2)
        draw.polygon([(52,17),(71,27),(70,44),(64,56),(52,63),(39,56),(35,44),(34,27)],
                     fill="#48868b" if name == "screen" else "#4e6079")
        draw.line([(35,27),(52,17),(71,27)], fill="#e2f0e9", width=2)
        if name == "screen":
            for x in (41,51,61):
                draw.line((x,31,x,53), fill="#8ad8c5", width=2)
            draw.line([(40,39),(49,34),(57,43),(65,39)], fill="#e3ffff", width=2)
        else:
            draw.line([(39,39),(49,49),(65,33)], fill="#c8f2d7", width=5)
    elif name == "calibration":
        draw.polygon([(21,52),(73,18),(83,27),(31,62)], fill="#9acdba", outline="#192b3a", width=2)
        draw.line([(29,53),(72,25)], fill="#edf9d4", width=2)
        for x, y in ((37,47),(44,43),(52,38),(61,32),(67,28)):
            draw.line((x,y,x+3,y+5), fill="#426665", width=2)
        draw.polygon([(48,34),(54,31),(60,38),(56,50),(47,51)], fill="#cdb783", outline="#233b43")
        draw.line((50,45,57,41), fill="#f8e8b4", width=2)
    elif name == "frostknife":
        draw.line([(25,65),(48,48)], fill="#243149", width=9)
        draw.line([(27,63),(47,48)], fill="#b8a184", width=4)
        draw.polygon([(44,46),(58,27),(83,14),(78,30),(57,52)], fill="#aedbea", outline="#1c304c", width=2)
        draw.line([(51,47),(60,33),(78,19)], fill="#f1fdff", width=3)
        draw.line([(44,42),(53,52)], fill="#d1b7a2", width=4)
    elif name == "arccut":
        draw.line([(29,64),(49,42),(74,30)], fill="#232130", width=9)
        draw.line([(75,62),(55,43),(38,19)], fill="#232130", width=9)
        draw.line([(28,63),(44,47)], fill="#cb815d", width=4)
        draw.line([(75,61),(61,47)], fill="#cb815d", width=4)
        draw.line([(45,37),(38,20),(47,13),(50,23)], fill="#e9bfab", width=5)
        draw.line([(57,40),(74,30),(72,17),(64,19)], fill="#e9bfab", width=5)
        draw.ellipse((46,36,59,49), fill="#e9ab69", outline="#262135", width=2)
        draw.line([(55,13),(51,21),(56,20),(56,26),(64,14)], fill="#ffe58d", width=2)
    elif name == "barbs":
        draw.rounded_rectangle((26,28,80,63), radius=10, fill="#654341", outline="#251d2a", width=2)
        draw.line((31,58,74,58), fill="#8d5c4b", width=4)
        draw.rounded_rectangle((34,33,72,53), radius=5, fill="#a46e55", outline="#dbc290", width=2)
        for x, y in ((36,26),(53,20),(68,25)):
            draw.polygon([(x-5,y+15),(x,y),(x+5,y+15)], fill="#f0d5a5", outline="#473543")
        draw.rectangle((48,43,56,54), fill="#eac189", outline="#392b32")
    elif name == "embers":
        bottle(draw, "#b65042", "#d88962", "#ffd9a1")
        draw.polygon([(52,39),(62,51),(61,59),(54,64),(46,62),(41,55),(44,47),(48,52)],
                     fill="#ffad67", outline="#833442")
        draw.polygon([(53,49),(57,56),(53,60),(48,57),(49,53)], fill="#ffe399")

    image.resize((210, 158), Image.Resampling.NEAREST).save(OUTPUT / f"item_{name}.png")


if __name__ == "__main__":
    for kind in PALETTES:
        portrait(kind)
