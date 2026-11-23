"""把工程已有插画合成在可重复生成的局内牌面上。"""
from pathlib import Path
from random import Random
import sys

from PIL import Image, ImageDraw, ImageFilter

root = Path(__file__).resolve().parents[1] / "Assets" / "Resources"
source = root / "Art"
output = root / "CardArt"
output.mkdir(exist_ok=True)

palettes = {
    "scout": ((37, 64, 92), (87, 151, 155), (156, 220, 201)),
    "porter": ((34, 70, 84), (82, 157, 141), (173, 226, 191)),
    "mechanic": ((69, 51, 82), (145, 103, 122), (238, 187, 154)),
    "courier": ((42, 62, 92), (100, 149, 169), (210, 228, 206)),
    "hound": ((48, 46, 79), (114, 102, 139), (192, 185, 224)),
    "drone": ((34, 62, 91), (75, 137, 183), (180, 220, 245)),
    "warden": ((62, 46, 72), (129, 91, 117), (221, 169, 186)),
    "ram": ((65, 42, 64), (149, 76, 95), (222, 158, 144)),
    "spear": ((73, 55, 79), (159, 108, 104), (249, 200, 134)),
    "chill": ((31, 64, 106), (75, 155, 189), (180, 228, 242)),
    "repair": ((39, 74, 73), (104, 162, 119), (231, 224, 160)),
    "whistle": ((70, 53, 85), (157, 111, 92), (237, 190, 119)),
}


def blend(a, b, t):
    return tuple(int(x + (y - x) * t) for x, y in zip(a, b))


def face(key, palette):
    dark, mid, light = palette
    w, h = 210, 158
    rng = Random(key)
    image = Image.new("RGBA", (w, h))
    pixels = image.load()
    for y in range(h):
        for x in range(w):
            dist = (((x - 105) / 123) ** 2 + ((y - 67) / 111) ** 2) ** 0.5
            color = blend(mid, dark, min(0.96, dist * 0.79 + y / 1200))
            pixels[x, y] = (*blend(color, light, rng.random() * 0.042), 255)

    detail = Image.new("RGBA", (w, h))
    pen = ImageDraw.Draw(detail)
    pen.ellipse((37, 5, 173, 141), outline=(*light, 107), width=2)
    pen.ellipse((44, 12, 166, 134), outline=(*light, 47), width=1)
    pen.ellipse((52, 20, 158, 126), fill=(*light, 32))
    for size, col, points in (
        (1, (*light, 107), [(0, 98), (26, 72), (44, 81), (60, 54), (89, 88), (125, 68), (155, 84), (184, 58), (210, 80)]),
        (2, (*light, 78), [(0, 120), (32, 102), (56, 110), (82, 95), (133, 101), (164, 94), (188, 106), (210, 101)]),
    ):
        pen.line(points, fill=col, width=size)
    pen.polygon([(0, 130), (31, 119), (61, 121), (120, 107), (164, 124), (207, 117), (210, 158), (0, 158)],
                fill=(*dark, 103))
    for _ in range(27):
        x, y = rng.randrange(9, 201), rng.randrange(12, 139)
        pen.point((x, y), fill=(*light, rng.randrange(60, 155)))
    for x, y in ((24, 33), (183, 44), (29, 107), (173, 110)):
        pen.line((x - 3, y, x + 3, y), fill=(*light, 145))
        pen.line((x, y - 3, x, y + 3), fill=(*light, 145))
    image = Image.alpha_composite(image, detail)

    sprite = Image.open(source / f"battle_{key}.png").convert("RGBA")
    sprite.thumbnail((162, 132), Image.Resampling.NEAREST)
    x, y = (w - sprite.width) // 2, (h - sprite.height) // 2 + 4
    shadow = Image.new("RGBA", image.size)
    mask = Image.new("L", image.size)
    mask.paste(sprite.getchannel("A"), (x, y + 3))
    shadow.paste((*dark, 220), (0, 0, w, h), mask.filter(ImageFilter.MaxFilter(5)).filter(ImageFilter.GaussianBlur(2)))
    image = Image.alpha_composite(image, shadow)
    image.alpha_composite(sprite, (x, y))
    ImageDraw.Draw(image).rounded_rectangle((1, 1, w - 2, h - 2), radius=9, outline=(*light, 125), width=2)
    image.save(output / f"{key}.png")


def icons():
    for name in ("health", "attack", "timer", "snow", "heal", "crown", "bell", "shell"):
        image = Image.new("RGBA", (64, 64))
        d = ImageDraw.Draw(image)
        col = (255, 248, 225, 255)
        if name == "health":
            d.polygon([(9,24),(12,16),(23,12),(32,21),(41,12),(52,16),(55,24),
                       (51,34),(32,53),(13,34)],fill=col)
        elif name == "attack":
            d.polygon([(15,44),(16,38),(22,43),(45,13),(54,10),(52,20),(27,47),(33,51),
                       (28,56),(22,50),(16,57),(10,51),(17,45)],fill=col)
        elif name == "timer":
            d.line((18,12,46,12), fill=col, width=5)
            d.line((18,52,46,52), fill=col, width=5)
            d.line([(20,14),(23,25),(41,40),(44,51)], fill=col, width=4)
            d.line([(44,14),(41,25),(23,40),(20,51)], fill=col, width=4)
            d.polygon([(24,47),(32,38),(40,47)], fill=col)
        elif name == "snow":
            for a,b in (((32,8),(32,56)),((11,20),(53,44)),((11,44),(53,20))):
                d.line([a,b],fill=col,width=5)
            d.line([(23,11),(32,20),(41,11)],fill=col,width=4)
            d.line([(23,53),(32,44),(41,53)],fill=col,width=4)
        elif name == "heal":
            d.rounded_rectangle((25,13,39,51), radius=4, fill=col)
            d.rounded_rectangle((13,25,51,39), radius=4, fill=col)
        elif name == "crown":
            d.polygon([(10,18),(22,28),(32,12),(42,28),(54,18),(49,43),(15,43)],fill=col)
            d.rounded_rectangle((15,46,49,51),radius=2,fill=col)
        elif name == "shell":
            d.polygon([(10,15),(32,7),(54,15),(52,36),(44,48),(32,57),(20,48),(12,36)], fill=col)
            d.line([(17,19),(32,13),(47,19),(46,34),(40,43),(32,49),(24,43),(18,34),(17,19)],fill="#3f8586",width=4)
        elif name == "bell":
            d.arc((15,11,49,47),180,360,fill=col,width=5)
            d.polygon([(15,27),(20,26),(20,39),(15,46),(49,46),(44,39),(44,26),(49,27),(49,38),(54,47),(53,51),(11,51),(10,47),(15,38)],fill=col)
            d.ellipse((27,51,37,60),fill=col)
        image.save(output / f"icon_{name}.png")


def status_icons():
    for name in ("spice", "overload", "ink", "weakness", "block", "bonus", "pendant", "barbs", "burn"):
        image = Image.new("RGBA", (64, 64))
        d = ImageDraw.Draw(image)
        if name == "spice":
            d.polygon([(9,48),(22,46),(31,38),(34,22),(41,15),(52,18),(56,28),(49,42),(37,52),(22,55)], fill="#ffad78")
            d.line([(48,19),(52,9),(59,8)], fill="#b6efad", width=6)
        elif name == "overload":
            d.polygon([(33,4),(12,35),(29,35),(22,61),(53,25),(35,25),(43,4)], fill="#ffe386")
        elif name == "ink":
            d.polygon([(32,3),(14,28),(11,39),(17,54),(32,60),(47,54),(53,39),(50,28)], fill="#ddacff")
            d.line([(21,36),(20,43),(25,48)], fill="#faf0ff", width=5)
        elif name == "weakness":
            for top in (10, 29):
                d.line([(10,top),(32,top+17),(54,top)], fill="#c2d3ff", width=8, joint="curve")
        elif name == "block":
            d.polygon([(32,6),(55,15),(51,40),(41,52),(32,58),(23,52),(13,40),(9,15)], fill="#c3f5ed")
            d.line([(20,31),(29,40),(45,22)], fill="#2a6069", width=6, joint="curve")
        elif name == "bonus":
            d.polygon([(32,4),(59,32),(32,60),(5,32)], fill="#b9f4c8")
            d.line((20,32,44,32), fill="#2f644c", width=6)
            d.line((32,20,32,44), fill="#2f644c", width=6)
        elif name == "pendant":
            d.arc((17,2,47,27), 0, 360, fill="#ffe3a1", width=6)
            d.polygon([(32,22),(51,38),(32,60),(13,38)], fill="#ffe3a1")
            d.polygon([(32,32),(40,39),(32,49),(24,39)], fill="#a36537")
        elif name == "barbs":
            d.polygon([(6,53),(20,19),(29,35),(34,5),(45,30),(51,17),(59,53)],
                      fill="#f3d297")
            d.line((10,55,57,55), fill="#fff0c6", width=5)
        elif name == "burn":
            d.polygon([(30,3),(47,24),(49,17),(58,38),(52,53),(40,61),(24,60),
                       (11,51),(7,38),(14,24),(19,34),(24,21)], fill="#ff9a64")
            d.polygon([(35,29),(43,42),(41,53),(33,57),(23,51),(22,42),(28,45)],
                      fill="#ffe29b")
        image.save(output / f"icon_{name}.png")


if "--status-icons" not in sys.argv:
    for key, colors in palettes.items():
        face(key, colors)
    icons()
status_icons()
