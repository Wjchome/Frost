"""Draw the original Frostline card illustrations. Requires Pillow."""

from math import cos, pi, sin
from pathlib import Path
from random import Random

from PIL import Image, ImageDraw, ImageFilter


OUTPUT = Path(__file__).resolve().parents[1] / "Assets/Resources/CardArt/Expedition"
SIZE = (480, 336)
SCALE = 3
INK = "#1a2830"
PAPER = "#f0e8d5"
GOLD = "#e6ad67"
ICE = "#bcdedc"


class Plate:
    def __init__(self, seed, dark, light, backdrop):
        self.random = Random(seed)
        self.image = Image.new("RGB", (240 * SCALE, 168 * SCALE), dark)
        self.draw = ImageDraw.Draw(self.image)
        self.paint_backdrop(dark, light, backdrop)
        for _ in range(125):
            x = self.random.randrange(4, 236)
            y = self.random.randrange(10, 157)
            self.line("#ddd5c1", .55, (x, y), (x + self.random.randrange(1, 4), y - 1))

    def paint_backdrop(self, dark, light, backdrop):
        if backdrop == "ridge":
            self.oval((100, 14, 170, 84), PAPER)
            self.poly(light, (0, 107), (42, 59), (82, 92), (146, 49), (183, 98),
                      (211, 72), (240, 98), (240, 147), (0, 154))
            self.poly(ICE, (9, 98), (42, 59), (72, 84), (53, 75), (42, 86), (35, 75))
            self.poly(ICE, (116, 71), (146, 49), (179, 93), (152, 78), (143, 72), (136, 75))
            self.poly(dark, (0, 130), (46, 104), (68, 118), (184, 103), (240, 131), (240, 168), (0, 168))
            self.line(light, 2, (7, 143), (47, 126), (78, 123))
            self.line(ICE, 1.4, (168, 141), (198, 122), (229, 133))
            self.flake(35, 41, 7, ICE, 1.4)
            self.flake(201, 51, 8, PAPER, 1.2)
        elif backdrop == "works":
            self.poly(light, (13, 72), (49, 45), (240, 12), (240, 113), (18, 149))
            self.poly("#637477", (0, 128), (43, 103), (83, 118), (190, 100), (240, 131), (240, 168), (0, 168))
            for x in (29, 58, 181, 210):
                self.line(dark, 2, (x, 124), (x, 54), (x + 16, 45))
                self.oval((x - 2, 65, x + 3, 70), GOLD)
            self.line(PAPER, 1.5, (14, 118), (47, 101), (68, 99))
            self.line(PAPER, 1.5, (179, 121), (219, 108), (238, 108))
            self.spark(51, 73, 7, PAPER)
            self.spark(191, 66, 9, GOLD)
        elif backdrop == "beasts":
            self.oval((79, 24, 169, 114), light)
            self.arc((72, 17, 176, 121), 193, 348, light, 1.4)
            self.line(light, 2.4, (0, 99), (38, 77), (68, 80), (93, 68))
            self.line(PAPER, 1.3, (155, 68), (189, 60), (218, 68), (240, 54))
            self.poly("#715864", (0, 133), (56, 112), (104, 125), (197, 107), (240, 115), (240, 168), (0, 168))
            self.flake(48, 46, 9, light, 1.4)
            self.flake(202, 91, 9, PAPER, 1.2)
        else:
            self.poly(light, (4, 64), (65, 45), (150, 30), (201, 48), (239, 42),
                      (224, 103), (143, 126), (73, 127), (15, 119))
            self.arc((67, 21, 174, 133), 21, 332, PAPER, 1.6)
            self.arc((61, 16, 179, 137), 154, 296, PAPER, 1.1)
            self.line(light, 1.4, (14, 123), (75, 142), (198, 126), (225, 102))
            if backdrop == "storm":
                self.flake(48, 49, 9, PAPER, 1.2)
                self.flake(191, 89, 8, ICE, 1.3)
            else:
                self.spark(43, 76, 7, PAPER)
                self.spark(187, 74, 7, PAPER)

    @staticmethod
    def xy(point):
        return tuple(round(value * SCALE) for value in point)

    def poly(self, fill, *points, edge=None, width=2):
        coords = [self.xy(point) for point in points]
        self.draw.polygon(coords, fill=fill)
        if edge:
            self.draw.line(coords + coords[:1], fill=edge, width=round(width * SCALE), joint="curve")

    def oval(self, box, fill, edge=None, width=2):
        self.draw.ellipse(self.xy(box), fill=fill, outline=edge, width=round(width * SCALE))

    def roundel(self, box, fill, edge=INK, radius=6):
        self.draw.rounded_rectangle(self.xy(box), radius=round(radius * SCALE),
                                    fill=fill, outline=edge, width=2 * SCALE)

    def line(self, fill, width, *points):
        self.draw.line([self.xy(point) for point in points], fill=fill,
                       width=round(width * SCALE), joint="curve")

    def arc(self, box, start, end, fill, width=2):
        self.draw.arc(self.xy(box), start, end, fill=fill, width=round(width * SCALE))

    def spark(self, x, y, size, color):
        self.poly(color, (x, y-size), (x+size/4, y-size/4), (x+size, y),
                  (x+size/4, y+size/4), (x, y+size), (x-size/4, y+size/4),
                  (x-size, y), (x-size/4, y-size/4))

    def flake(self, x, y, size, color, width):
        for arm in range(6):
            angle = arm * pi / 3
            dx, dy = cos(angle), sin(angle)
            knee = (x + dx * size * .64, y + dy * size * .64)
            self.line(color, width, (x, y), (x + dx * size, y + dy * size))
            for side in (-1, 1):
                branch = angle + side * pi / 3
                self.line(color, width, knee,
                          (knee[0]+cos(branch)*size*.30, knee[1]+sin(branch)*size*.30))

    def hatch(self, color, x, y, count, step=5):
        for index in range(count):
            start = x + index * step
            self.line(color, 1.2, (start, y), (start + 4, y - 7))

    def finish(self, name):
        image = self.image.resize(SIZE, Image.Resampling.LANCZOS)
        # A restrained ink-on-paper grain softens the flat fills at native card size.
        grain = Image.frombytes("L", SIZE, bytes(
            self.random.randrange(94, 161) for _ in range(SIZE[0] * SIZE[1])))
        grain = grain.filter(ImageFilter.GaussianBlur(0.45)).convert("RGB")
        image = Image.blend(image, grain, 0.075)
        image.save(OUTPUT / f"{name}.png", optimize=True)


def snowball(plate, variant):
    p = plate
    p.line(ICE, 2, (27, 72), (64, 60), (84, 54))
    p.line(PAPER, 3, (25, 96), (55, 87), (78, 76))
    p.line(ICE, 2, (41, 111), (71, 101))
    if variant == 0:
        p.poly("#659da8", (83, 98), (74, 80), (83, 60), (105, 52), (126, 58),
               (143, 77), (139, 99), (122, 115), (98, 116), edge=INK)
        p.poly(PAPER, (78, 79), (90, 61), (107, 55), (128, 64), (138, 77),
               (127, 91), (103, 98), (84, 91))
        p.flake(111, 78, 14, "#437180", 2)
        p.hatch(ICE, 97, 107, 5)
        p.spark(154, 57, 8, PAPER)
    elif variant == 1:
        p.poly("#62929e", (72, 105), (66, 81), (79, 56), (103, 41), (142, 43),
               (163, 65), (170, 89), (161, 109), (138, 128), (102, 129), edge=INK)
        p.poly(PAPER, (69, 82), (82, 59), (102, 47), (142, 47), (163, 69),
               (151, 94), (118, 102), (83, 95))
        p.poly("#b4d2cf", (118, 102), (151, 94), (164, 70), (166, 90), (158, 109),
               (136, 121), (113, 119))
        p.flake(116, 74, 21, "#426a7d", 2.7)
        p.spark(181, 62, 10, ICE)
    else:
        p.poly("#7397ad", (55, 109), (53, 73), (70, 44), (107, 27), (152, 31),
               (180, 50), (187, 82), (177, 114), (145, 139), (93, 140), edge=INK)
        p.poly(PAPER, (58, 73), (74, 46), (109, 31), (153, 36), (179, 55),
               (181, 74), (159, 91), (134, 85), (107, 106), (78, 94))
        p.line("#b5d2d7", 4, (181, 76), (155, 109), (129, 126), (88, 126))
        p.line("#416578", 2.5, (176, 102), (157, 108), (145, 129))
        p.flake(116, 66, 25, "#456b7d", 2.7)
        p.spark(193, 49, 13, PAPER)
        p.poly(ICE, (26, 113), (47, 102), (43, 118))
        p.poly(PAPER, (186, 118), (202, 101), (198, 122))


def wand(plate, material, gem, kind):
    p = plate
    p.poly("#5a4138", (54, 127), (49, 118), (143, 45), (156, 58), (66, 132), edge=INK)
    p.line(material, 4, (59, 119), (142, 53))
    for offset in range(4):
        p.line(GOLD, 2, (62+offset*6, 114-offset*5), (68+offset*6, 119-offset*5))
    if kind == "long":
        p.arc((132, 27, 185, 74), 5, 352, material, 8)
        p.arc((124, 22, 197, 81), 15, 330, PAPER, 1.2)
        p.poly(gem, (158, 29), (176, 44), (162, 66), (146, 51), edge=INK)
        p.line(PAPER, 2, (158, 32), (153, 46), (162, 60))
    elif kind == "arc":
        p.poly(material, (139, 52), (137, 31), (151, 22), (160, 41),
               (175, 25), (189, 34), (176, 59), (151, 63), edge=INK)
        p.poly(gem, (145, 48), (160, 46), (173, 33), (168, 51), (152, 58))
        p.line(PAPER, 2, (171, 29), (179, 18), (190, 21), (200, 12))
    else:
        p.poly(gem, (141, 49), (147, 28), (168, 25), (181, 43),
               (171, 62), (151, 63), edge=INK)
        p.poly(material, (146, 46), (151, 33), (163, 30), (172, 43), (168, 54), (153, 56))
        p.line(PAPER, 2, (150, 37), (157, 33), (163, 33))
    p.spark(199, 55, 10, PAPER)
    p.spark(107, 83, 8, PAPER)


def weapon(plate, kind):
    p = plate
    if kind == "blade":
        p.poly("#7799a0", (72, 111), (141, 40), (194, 30), (177, 71), (88, 128), edge=INK)
        p.poly(PAPER, (79, 110), (143, 46), (185, 35), (159, 61))
        p.line("#365d66", 2, (84, 115), (180, 40))
        p.line("#714e3f", 13, (47, 136), (83, 115))
        p.line(GOLD, 6, (74, 105), (89, 128))
        p.hatch(PAPER, 53, 136, 4, 4)
    elif kind == "cross":
        for blade in ([(56, 125), (149, 49), (181, 37), (170, 69), (66, 137)],
                      [(173, 130), (78, 68), (59, 37), (99, 44), (181, 116)]):
            p.poly("#ba8c59", *blade, edge=INK)
        p.line(PAPER, 4, (84, 107), (171, 47))
        p.line(ICE, 4, (69, 45), (112, 76), (153, 106))
        p.line("#614237", 11, (48, 132), (83, 110))
        p.line("#644237", 11, (158, 111), (187, 133))
        p.spark(117, 84, 9, PAPER)
    else:
        p.line(INK, 15, (61, 135), (148, 51))
        p.line("#b78d65", 10, (61, 135), (148, 51))
        p.poly("#62868a", (123, 54), (108, 35), (130, 22), (149, 41),
               (157, 41), (173, 22), (193, 39), (180, 66), (153, 74), edge=INK)
        p.line(PAPER, 3, (114, 35), (129, 27), (145, 43))
        p.oval((150, 48, 171, 67), GOLD, INK)
        p.hatch(PAPER, 75, 122, 6)


def bottle(plate, liquid, glass, neck, mark):
    p = plate
    p.poly(glass, (103, 40), (136, 40), (140, 61), (163, 76), (168, 111),
           (150, 131), (90, 131), (73, 112), (77, 80), (100, 61), edge=INK, width=3)
    p.poly(liquid, (79, 84), (102, 78), (126, 86), (158, 82), (164, 109),
           (146, 126), (94, 126), (78, 111))
    p.line(PAPER, 3, (84, 105), (86, 86), (101, 74))
    p.line(PAPER, 2, (139, 119), (152, 111))
    p.roundel((98, 28, 142, 44), neck, radius=4)
    p.line(PAPER, 2, (105, 34), (133, 34))
    if mark == "drop":
        p.poly("#edd8ef", (122, 89), (132, 104), (129, 115), (119, 120), (108, 112),
               (108, 104), edge=INK, width=1.5)
    else:
        p.oval((108, 93, 130, 117), "#f2c3ad", INK)
        p.line(PAPER, 2, (119, 97), (119, 111))
    p.spark(175, 54, 9, PAPER)
    p.spark(56, 87, 7, PAPER)


def curios(p, kind):
    if kind == "fruit":
        p.poly("#a1455a", (72, 95), (80, 70), (103, 56), (137, 55), (164, 74),
               (173, 101), (151, 127), (115, 135), (87, 121), edge=INK, width=3)
        p.poly("#ed8f96", (105, 59), (101, 38), (115, 47), (129, 34), (131, 53),
               (148, 42), (140, 58), edge=INK)
        p.oval((95, 68, 162, 120), "#efae9d", INK)
        for x, y in ((106, 86), (120, 78), (135, 80), (147, 86), (114, 99),
                     (130, 91), (139, 104), (124, 110)):
            p.oval((x, y, x+8, y+8), "#ac4061")
    elif kind == "knife":
        p.poly("#cf8fa3", (103, 99), (144, 57), (184, 47), (166, 77),
               (117, 113), edge=INK, width=3)
        p.line(PAPER, 3, (110, 99), (151, 62), (176, 52))
        p.line(INK, 13, (65, 136), (111, 105))
        p.line("#75545c", 8, (66, 134), (110, 104))
        p.hatch(GOLD, 75, 132, 5, 4)
        p.oval((80, 79, 97, 98), "#b24764", INK)
        p.oval((65, 87, 83, 106), "#9e526c", INK)
    elif kind == "copy":
        p.poly("#a6b8b3", (92, 47), (156, 31), (178, 113), (115, 131), edge=INK, width=3)
        p.poly(PAPER, (74, 46), (140, 49), (137, 136), (70, 130), edge=INK, width=3)
        p.poly("#ddd4b8", (80, 54), (131, 56), (130, 128), (77, 124), edge="#616e70", width=1)
        p.arc((88, 70, 122, 109), 40, 325, "#567577", 2)
        p.spark(105, 89, 9, GOLD)
    else:
        p.poly("#7f9693", (75, 57), (117, 38), (159, 50), (173, 70), (161, 111),
               (124, 137), (91, 118), (73, 90), edge=INK, width=3)
        p.poly("#ac8871", (83, 63), (116, 48), (156, 58), (164, 74), (152, 106),
               (124, 126), (99, 110), (83, 86), edge=INK)
        p.oval((100, 64, 146, 110), "#403c46", INK)
        p.line(GOLD, 3, (120, 68), (126, 57), (139, 59))
        p.spark(145, 56, 9, GOLD)
        p.arc((108, 70, 141, 102), 141, 238, PAPER, 2)


def salvage(plate, kind):
    p = plate
    if kind in ("junk", "permanent"):
        p.poly("#648486", (59, 114), (69, 68), (100, 59), (121, 85), (117, 125),
               (74, 130), edge=INK)
        p.poly("#bd8052", (110, 110), (119, 74), (151, 60), (175, 80), (176, 112),
               (149, 133), (117, 130), edge=INK)
        p.oval((120, 89, 160, 125), "#694735", INK)
        p.oval((132, 96, 151, 114), GOLD, INK)
        p.line(PAPER, 3, (68, 105), (74, 77), (100, 68))
        p.hatch(ICE, 73, 120, 6)
        p.poly("#d0b591", (91, 124), (99, 106), (114, 106), (123, 127), edge=INK)
        if kind == "permanent":
            p.arc((88, 29, 153, 84), 26, 316, GOLD, 7)
            p.poly(GOLD, (146, 48), (156, 61), (164, 39))
        else:
            p.poly("#d2b468", (105, 38), (119, 43), (121, 62), (106, 71), (94, 58), edge=INK)
    elif kind in ("bomb", "flare"):
        body = "#9a4a41" if kind == "flare" else "#496d75"
        p.oval((69, 58, 159, 135), body, INK, 3)
        p.roundel((95, 52, 130, 72), "#aa8660", radius=3)
        p.line(INK, 7, (115, 55), (127, 35), (145, 29), (165, 37))
        p.line(GOLD, 3, (115, 55), (127, 36), (145, 29), (165, 37))
        p.arc((78, 65, 151, 130), 169, 260, PAPER, 3)
        p.hatch(PAPER, 95, 120, 8)
        p.spark(170, 37, 18, GOLD)
        p.spark(188, 60, 8, PAPER)
        if kind == "flare":
            p.poly("#ffd58b", (102, 115), (102, 92), (111, 83), (117, 99), (123, 87),
                   (131, 113), (118, 120))
        else:
            p.poly(GOLD, (113, 80), (132, 90), (122, 111), (102, 110), (95, 92), edge=INK)
            p.line(INK, 3, (104, 91), (123, 99))
    elif kind == "bucket":
        p.arc((65, 33, 176, 110), 172, 370, INK, 5)
        p.poly("#798c8a", (65, 67), (176, 67), (157, 128), (87, 129), edge=INK, width=3)
        p.poly("#4e696e", (125, 77), (171, 74), (153, 124), (123, 126))
        p.oval((62, 56, 177, 84), "#d8b67e", INK)
        p.oval((73, 61, 166, 77), "#334b51")
        p.line(PAPER, 3, (80, 84), (93, 115), (108, 120))
        p.hatch(ICE, 106, 120, 8)
    elif kind == "horn":
        p.poly("#8d5f4a", (68, 110), (152, 110), (164, 130), (75, 134), (64, 127), edge=INK)
        p.line(PAPER, 3, (76, 117), (149, 117))
        p.line(INK, 13, (117, 111), (113, 84), (128, 68), (154, 61))
        p.line(GOLD, 8, (117, 109), (113, 84), (128, 68), (154, 61))
        p.poly("#c7905d", (123, 65), (162, 36), (186, 57), (166, 83), (133, 80), edge=INK)
        p.oval((156, 31, 189, 73), GOLD, INK)
        p.oval((163, 38, 183, 65), "#704836")
        p.line(PAPER, 2, (177, 35), (185, 46), (185, 57))
        p.spark(195, 40, 9, PAPER)
    else:
        p.poly("#bd8d61", (96, 87), (147, 87), (124, 137), edge=INK)
        p.line("#8a5e42", 2, (105, 106), (131, 116))
        p.line("#8a5e42", 2, (115, 126), (133, 101))
        p.oval((86, 51, 155, 104), "#c4dfd9", INK)
        p.oval((100, 36, 146, 86), PAPER, INK)
        p.arc((107, 43, 142, 73), 190, 301, "#84aeaf", 3)
        p.flake(127, 70, 12, "#61999d", 1.8)


def medic_prop(p, prop, color):
    if prop == "flag":
        p.line(INK, 6, (173, 28), (173, 138))
        p.poly(color, (174, 33), (206, 39), (196, 54), (174, 56), edge=INK)
        p.spark(188, 44, 6, PAPER)
    elif prop == "shield":
        p.poly(color, (165, 76), (194, 66), (206, 84), (196, 114),
               (176, 130), (161, 109), edge=INK, width=3)
        p.poly(PAPER, (173, 83), (191, 75), (198, 86), (191, 107), (179, 119), (171, 104))
        p.spark(184, 96, 11, color)
    elif prop == "satchel":
        p.roundel((160, 90, 199, 130), color)
        p.arc((166, 76, 194, 108), 180, 360, INK, 3)
        p.line(PAPER, 2, (169, 104), (189, 104))
        p.spark(179, 116, 6, PAPER)
    elif prop == "spyglass":
        p.poly(color, (160, 52), (195, 43), (203, 56), (164, 66), edge=INK)
        p.line(PAPER, 3, (165, 55), (195, 48))
        p.oval((190, 41, 205, 58), ICE, INK)
    elif prop == "antenna":
        p.line(INK, 4, (171, 89), (181, 40))
        p.oval((175, 31, 188, 45), color, INK)
        p.spark(191, 26, 7, PAPER)
    elif prop == "gauntlet":
        p.poly(color, (163, 93), (166, 72), (175, 65), (181, 74), (188, 63),
               (198, 68), (202, 86), (193, 111), (177, 118), edge=INK)
        p.hatch(PAPER, 170, 89, 6, 4)
    elif prop == "book":
        p.poly(color, (162, 80), (177, 77), (186, 83), (199, 77), (206, 100),
               (186, 107), (163, 101), edge=INK)
        p.line(PAPER, 2, (170, 87), (184, 89), (188, 101), (199, 92))
    else:
        p.roundel((161, 89, 196, 129), color)
        p.oval((172, 98, 187, 115), PAPER, INK)
        p.line(INK, 3, (179, 102), (179, 110))


def scout(p, cloth, trim, scarf, hat, prop):
    p.poly(scarf, (97, 91), (76, 86), (44, 92), (29, 108), (60, 108), (91, 102), edge=INK)
    p.poly(cloth, (71, 139), (78, 113), (96, 94), (144, 94), (162, 111),
           (171, 140), (149, 146), (91, 147), edge=INK, width=3)
    p.poly(trim, (80, 118), (99, 101), (108, 117), (96, 141), (79, 137))
    p.poly("#20363a", (140, 100), (159, 113), (166, 136), (135, 143), (133, 120))
    p.poly(scarf, (93, 94), (151, 94), (152, 107), (131, 119), (101, 112), (90, 103), edge=INK)
    p.line(PAPER, 2, (106, 110), (127, 114), (140, 110))
    if hat == "hood":
        p.poly(cloth, (86, 80), (90, 53), (112, 33), (138, 32), (157, 51), (162, 83),
               (149, 101), (95, 101), edge=INK, width=3)
        p.poly(trim, (90, 69), (96, 50), (111, 40), (137, 38), (151, 49), (143, 61), (114, 58))
        p.arc((96, 50, 156, 95), 221, 320, PAPER, 2.2)
    elif hat == "cap":
        p.poly(cloth, (84, 79), (92, 48), (116, 35), (139, 40), (157, 58), (156, 87),
               (143, 102), (96, 100), edge=INK, width=3)
        p.poly(trim, (89, 57), (115, 38), (138, 43), (152, 57), (147, 69), (93, 69))
        p.line(PAPER, 3, (93, 62), (115, 57), (148, 62))
        p.oval((109, 25, 123, 41), GOLD, INK)
    else:
        p.poly(cloth, (84, 82), (91, 60), (106, 44), (143, 42), (157, 56),
               (162, 84), (147, 102), (96, 101), edge=INK, width=3)
        p.poly(trim, (87, 64), (101, 36), (124, 29), (148, 39), (158, 56), (149, 67))
        p.line(PAPER, 3, (103, 44), (123, 35), (143, 41))
        p.line(INK, 3, (88, 67), (142, 61), (167, 67))
    p.oval((98, 64, 151, 98), "#d9b799", INK)
    p.poly("#b37b62", (132, 74), (147, 71), (148, 86), (138, 96), (122, 96))
    p.line(INK, 2.5, (108, 76), (112, 73), (116, 75))
    p.oval((130, 73, 135, 78), INK)
    p.line(INK, 2, (121, 87), (133, 86))
    p.line("#a16959", 2, (121, 75), (119, 81), (125, 82))
    p.hatch(PAPER, 82, 135, 4)
    p.spark(120, 128, 7, GOLD)
    p.line(trim, 2, (138, 119), (144, 136), (155, 134))
    medic_prop(p, prop, trim)


def automaton(p, metal, enamel, glow, face, prop):
    p.poly("#405253", (70, 137), (75, 113), (100, 100), (143, 101), (166, 118),
           (172, 141), (77, 147), edge=INK, width=3)
    p.poly(metal, (79, 129), (88, 111), (109, 106), (133, 111), (143, 138), (86, 142), edge=INK)
    p.hatch(PAPER, 89, 131, 8)
    p.roundel((99, 100, 142, 116), enamel)
    if face == "round":
        p.oval((80, 41, 159, 106), metal, INK, 3)
        p.oval((92, 52, 150, 98), enamel, INK)
        p.arc((83, 44, 153, 103), 185, 278, PAPER, 3)
    elif face == "visor":
        p.poly(metal, (80, 58), (102, 38), (143, 40), (163, 57), (162, 96),
               (142, 109), (100, 109), (79, 94), edge=INK, width=3)
        p.roundel((90, 61, 153, 91), enamel)
        p.line(PAPER, 3, (88, 57), (104, 45), (132, 45))
    else:
        p.poly(metal, (87, 49), (104, 38), (141, 38), (159, 54), (159, 91),
               (144, 104), (98, 108), (82, 92), edge=INK, width=3)
        p.poly(enamel, (92, 63), (145, 54), (152, 78), (142, 97), (103, 98), (89, 83), edge=INK)
        p.line(PAPER, 3, (95, 54), (109, 45), (141, 44))
    p.roundel((98, 68, 143, 87), INK, radius=5)
    if face == "round":
        p.oval((113, 70, 130, 85), glow)
        p.spark(121, 77, 5, PAPER)
    else:
        p.line(glow, 3, (104, 78), (114, 75), (120, 81), (129, 75), (137, 76))
    p.oval((86, 85, 94, 93), GOLD, INK)
    p.oval((148, 85, 156, 93), GOLD, INK)
    p.line(INK, 3, (118, 43), (114, 30))
    p.oval((108, 23, 120, 35), glow, INK)
    medic_prop(p, prop, metal)


def snow_creature(p, kind):
    if kind in ("baby", "hedgehog", "nunu", "bobo"):
        if kind == "hedgehog":
            p.poly("#617887", (54, 110), (61, 84), (52, 68), (73, 72), (71, 48), (90, 59),
                   (102, 39), (117, 52), (133, 38), (142, 60), (163, 49), (158, 69),
                   (178, 61), (173, 84), (185, 99), (169, 128), (89, 142), edge=INK, width=3)
        elif kind == "nunu":
            p.poly("#ae938f", (63, 105), (66, 73), (86, 56), (101, 38), (133, 39), (151, 53),
                   (171, 58), (177, 95), (161, 127), (82, 134), edge=INK, width=3)
            p.oval((65, 49, 97, 82), "#c7abaa", INK)
            p.oval((142, 43, 175, 76), "#c7abaa", INK)
        elif kind == "bobo":
            p.poly("#7599a8", (58, 125), (65, 95), (82, 87), (75, 67), (92, 44), (119, 41),
                   (140, 63), (154, 56), (175, 77), (171, 102), (183, 121),
                   (165, 133), (84, 138), edge=INK, width=3)
        else:
            p.poly("#94b1ba", (77, 122), (79, 94), (87, 80), (84, 66), (104, 48), (129, 49),
                   (147, 63), (145, 83), (161, 106), (155, 126), (129, 138),
                   (96, 135), edge=INK, width=3)
            p.poly(GOLD, (104, 53), (118, 29), (133, 52), edge=INK)
        p.oval((85, 67, 158, 119), PAPER, INK)
        p.oval((101, 83, 110, 93), INK)
        p.oval((134, 81, 143, 91), INK)
        p.line("#794f57", 3, (111, 101), (120, 105), (133, 100))
        p.oval((91, 95, 100, 101), "#d19484")
        p.oval((143, 94, 153, 100), "#d19484")
        for x in (94, 115, 137):
            p.line(PAPER, 2, (x, 127), (x+9, 123))
        p.spark(182, 90, 8, PAPER)
        return

    p.poly("#577787", (55, 112), (68, 87), (82, 67), (105, 72), (132, 61), (161, 56),
           (178, 69), (192, 83), (171, 102), (147, 102), (126, 133),
           (110, 132), (116, 115), (84, 121), (71, 136), (58, 133), edge=INK, width=3)
    p.poly(PAPER, (67, 87), (85, 71), (100, 76), (129, 67), (163, 62),
           (177, 75), (160, 91), (130, 89), (109, 107), (88, 102))
    p.poly("#729bab", (135, 66), (143, 43), (153, 34), (160, 62), edge=INK)
    p.poly("#729bab", (158, 61), (174, 41), (179, 50), (175, 74), edge=INK)
    p.line(INK, 3, (153, 76), (161, 73))
    p.oval((183, 79, 194, 88), INK)
    p.hatch(ICE, 79, 112, 6)


def bird(p, kind):
    if kind == "penguins":
        for x, y, size in ((79, 89, 24), (155, 91, 24), (119, 85, 34)):
            p.oval((x-size, y-size, x+size, y+size*1.6), "#465e69", INK)
            p.oval((x-size*.62, y-size*.55, x+size*.62, y+size*1.26), PAPER)
            p.poly(GOLD, (x-9, y+1), (x+15, y+4), (x-4, y+10), edge=INK, width=1)
            p.oval((x-11, y-9, x-6, y-4), INK)
            p.oval((x+6, y-9, x+11, y-4), INK)
        return
    p.poly("#a9adb2", (57, 105), (83, 88), (110, 89), (121, 72), (114, 51),
           (122, 34), (145, 32), (159, 45), (159, 60), (140, 65), (145, 95),
           (131, 123), (91, 134), (64, 122), edge=INK, width=3)
    p.poly(PAPER, (65, 106), (89, 95), (116, 98), (129, 80), (128, 55),
           (131, 39), (144, 37), (153, 45), (146, 59), (136, 66), (138, 93),
           (123, 116), (86, 123))
    p.poly(GOLD, (153, 46), (182, 51), (186, 57), (160, 63), (151, 59), edge=INK)
    p.oval((144, 45, 149, 50), INK)
    p.line("#598389", 3, (122, 74), (139, 74))
    p.hatch("#70858a", 75, 117, 8)
    p.line(GOLD, 4, (95, 128), (90, 140), (106, 142))
    if kind == "shield":
        medic_prop(p, "shield", "#577480")


def ice_beast(p, body, frost, shape):
    if shape in ("worm", "roll", "sticky"):
        if shape == "worm":
            for x, y, r in ((65, 111, 17), (87, 107, 21), (112, 96, 24), (133, 81, 28), (155, 68, 30)):
                p.oval((x-r, y-r, x+r, y+r), body, INK, 2.6)
                p.arc((x-r+5, y-r+4, x+r-5, y+r-6), 184, 273, frost, 3)
            p.oval((157, 61, 165, 70), INK)
            p.oval((173, 65, 184, 81), frost, INK)
        elif shape == "roll":
            p.oval((61, 43, 172, 136), body, INK, 3)
            p.oval((72, 49, 160, 123), frost, INK)
            p.arc((84, 59, 148, 115), 25, 350, body, 5)
            p.arc((99, 71, 133, 103), 35, 341, body, 4)
            p.oval((111, 82, 124, 93), GOLD, INK)
            p.line(GOLD, 3, (162, 117), (181, 108), (191, 119))
        else:
            p.poly(body, (52, 119), (59, 88), (75, 77), (84, 47), (113, 48), (133, 35),
                   (157, 49), (163, 75), (183, 87), (192, 111), (175, 129), (111, 137),
                   (70, 132), edge=INK, width=3)
            p.poly(frost, (63, 113), (76, 88), (90, 78), (96, 54), (117, 57), (133, 43),
                   (152, 55), (156, 75), (133, 88), (97, 90), (84, 110))
            p.oval((107, 76, 116, 89), INK)
            p.oval((138, 73, 148, 87), INK)
            p.line(INK, 3, (116, 105), (125, 110), (135, 104))
            p.hatch(frost, 96, 124, 9, 6)
        return

    head = {"bear": (77, 52, 163, 111), "elder": (83, 54, 159, 109),
            "gulper": (78, 49, 170, 116), "foot": (73, 52, 163, 104),
            "walnut": (82, 51, 160, 112)}[shape]
    p.poly(body, (52, 128), (57, 100), (77, 83), (160, 78), (182, 94), (193, 126),
           (166, 137), (82, 143), edge=INK, width=3)
    if shape == "bear":
        p.oval((78, 36, 108, 66), body, INK)
        p.oval((135, 38, 160, 64), body, INK)
    if shape == "foot":
        p.poly(frost, (75, 78), (67, 51), (88, 56), (98, 36), (118, 46), (143, 37),
               (158, 53), (176, 55), (166, 87), edge=INK, width=3)
        p.roundel((140, 104, 194, 135), body, radius=12)
        p.hatch(frost, 153, 129, 6, 4)
    p.oval(head, frost, INK, 3)
    if shape in ("elder", "walnut"):
        p.poly(body, (78, 70), (100, 43), (141, 44), (163, 70), (139, 75),
               (118, 69), (94, 76), edge=INK)
    p.oval((101, 75, 108, 85), INK)
    p.oval((140, 74, 147, 84), INK)
    if shape == "gulper":
        p.oval((98, 85, 149, 113), "#633f4a", INK)
        p.line(PAPER, 3, (108, 93), (137, 93))
    else:
        p.oval((110, 85, 138, 103), body, INK, 1.4)
        p.line(INK, 2, (119, 96), (128, 96))
    if shape == "elder":
        p.poly(PAPER, (91, 96), (110, 102), (137, 102), (150, 95), (149, 117),
               (128, 133), (105, 125), edge=INK)
        p.line(body, 2, (110, 112), (117, 124))
    p.hatch(frost, 70, 129, 7)


def draw_asset(name, painter, palette, *args):
    plate = Plate(name, *palette)
    painter(plate, *args)
    plate.finish(name)


FROST = ("#345765", "#6e989d", "storm")
SNOWFOLK = ("#345765", "#6e989d", "ridge")
AMBER = ("#58464a", "#ab796c", "workbench")
VIOLET = ("#493c5a", "#9986a6", "storm")
MACHINE = ("#3b5052", "#9b8a67", "works")
ENEMY = ("#503e49", "#ac8c91", "beasts")


def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    for name, size in (("DraftSnowball", 0), ("DraftLargeSnowball", 1), ("DraftHugeSnowball", 2)):
        draw_asset(name, snowball, FROST, size)

    items = {
        "DraftWand": (wand, AMBER, GOLD, "#b5d9d5", "plain"),
        "DraftGreatWand": (wand, FROST, "#d2b57f", "#9bd6d7", "long"),
        "DraftDamageWand": (wand, AMBER, "#e8bb7b", "#e88762", "arc"),
        "DraftBerryWand": (wand, VIOLET, "#e1c475", "#cb81a1", "plain"),
        "DraftGreatWeapon": (weapon, FROST, "blade"),
        "DraftWeapon": (weapon, AMBER, "blade"),
        "DraftWildWeapon": (weapon, VIOLET, "cross"),
        "DraftJunkWrench": (weapon, MACHINE, "wrench"),
        "DraftGearHammer": (weapon, AMBER, "wrench"),
        "DraftBucket": (salvage, MACHINE, "bucket"),
        "DraftIceCream": (salvage, FROST, "cream"),
        "DraftGramophone": (salvage, AMBER, "horn"),
        "DraftFlare": (salvage, AMBER, "flare"),
        "DraftJunkBomb": (salvage, MACHINE, "bomb"),
        "DraftJunk": (salvage, MACHINE, "junk"),
        "DraftPermanentJunk": (salvage, AMBER, "permanent"),
        "Dye": (bottle, VIOLET, "#553d68", "#a797bc", GOLD, "drop"),
        "DraftBerryJuice": (bottle, AMBER, "#a14868", "#e2a1a3", GOLD, "berry"),
        "DraftGreatJuice": (bottle, VIOLET, "#833e68", "#db98ac", "#deb980", "berry"),
        "DraftPomegranate": (curios, AMBER, "fruit"),
        "DraftBerryKnife": (curios, VIOLET, "knife"),
        "DraftBombShield": (curios, MACHINE, "shield"),
        "DraftCopy": (curios, FROST, "copy"),
    }
    for name, (painter, palette, *args) in items.items():
        draw_asset(name, painter, palette, *args)

    snowfolk = {
        "SnowCaptain": ("#38606e", "#99bbbc", "#b45146", "hood", "spyglass"),
        "IcefieldCaptain": ("#446776", "#acc8bc", "#b78344", "cap", "shield"),
        "RimeCaptain": ("#595572", "#a3a9c3", "#925255", "hat", "gauntlet"),
        "WhitewindCaptain": ("#849ca8", "#d3e0cf", "#9c4d4a", "hood", "flag"),
        "SnowHelper": ("#4e7874", "#9bbfa5", "#a64e4c", "cap", "satchel"),
        "SnowDrawer": ("#477081", "#8fbfc2", "#af724d", "hat", "book"),
        "SnowProtector": ("#4a6674", "#abc1bf", "#79516b", "hood", "shield"),
        "SnowReflector": ("#60688c", "#aab2d1", "#975357", "cap", "gauntlet"),
        "SnowInker": ("#655575", "#b3a1c2", "#526d82", "hood", "satchel"),
        "SnowAmplifier": ("#66786b", "#a7bf9a", "#937044", "hat", "flag"),
    }
    for name, args in snowfolk.items():
        draw_asset(name, scout, SNOWFOLK, *args)

    junkfolk = {
        "OverloadCaptain": ("#ad8055", "#42696c", "#ffd78c", "visor", "flag"),
        "SparkCaptain": ("#bd805e", "#607781", "#f8db82", "round", "antenna"),
        "FurnaceCaptain": ("#b67658", "#65464d", "#ffb75d", "mask", "gauntlet"),
        "IronCaptain": ("#80918f", "#4d6870", "#b4e5d6", "visor", "shield"),
        "JunkMaker": ("#ad875d", "#4f6b62", "#f0ce83", "round", "satchel"),
        "JunkReceiver": ("#91a395", "#4c686c", "#d6e8b9", "visor", "book"),
        "JunkLover": ("#b98268", "#8c6276", "#f3c8a8", "round", "gauntlet"),
        "JunkFanatic": ("#c18f5c", "#68587a", "#f0cb7b", "mask", "antenna"),
        "JunkDestroyer": ("#a8806c", "#725661", "#ffb16a", "visor", "gauntlet"),
        "JunkKeeper": ("#9da18b", "#5b786e", "#e3df9e", "round", "shield"),
    }
    for name, args in junkfolk.items():
        draw_asset(name, automaton, MACHINE, *args)

    for name, kind in (("EnemySnowBaby", "baby"), ("EnemySnowBobo", "bobo"),
                       ("EnemySnowWolf", "wolf"), ("EnemySnowHedgehog", "hedgehog"),
                       ("EnemySnowNunu", "nunu")):
        draw_asset(name, snow_creature, ENEMY, kind)
    for name, kind in (("EnemyGoose", "goose"), ("EnemyShieldGoose", "shield"),
                       ("EnemyPenguinGang", "penguins")):
        draw_asset(name, bird, ENEMY, kind)
    for name, args in {
        "EnemyBigFoot": ("#70869b", "#d8ddd4", "foot"),
        "EnemyGreatSnow": ("#627d8d", "#c6d6d2", "bear"),
        "EnemyGreatSticky": ("#667e73", "#adcb9e", "sticky"),
        "EnemyMountainWalnut": ("#987768", "#d5b48b", "walnut"),
        "EnemyObaBear": ("#916c67", "#d7b4a4", "bear"),
        "EnemySnowElder": ("#6c7885", "#ddded2", "elder"),
        "EnemySnowGulper": ("#748c97", "#d3e4d9", "gulper"),
        "EnemySnowRoll": ("#5b8793", "#c5dfd7", "roll"),
        "EnemySnowWorm": ("#72809c", "#bbc8e0", "worm"),
        "EnemySticky": ("#8a727e", "#caa9bd", "sticky"),
    }.items():
        draw_asset(name, ice_beast, ENEMY, *args)
    draw_asset("EnemyQuick", bird, FROST, "goose")
    draw_asset("EnemyLittleMoko", scout, ENEMY, "#7d645f", "#c7a088", "#9f604a", "cap", "satchel")
    draw_asset("EnemyMakoko", scout, ENEMY, "#765256", "#b98b75", "#ae6044", "hood", "gauntlet")
    draw_asset("EnemyPepperWitch", scout, AMBER, "#733f54", "#bb765e", "#a64442", "hat", "flag")


if __name__ == "__main__":
    main()
