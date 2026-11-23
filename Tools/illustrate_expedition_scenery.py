"""Draw the expedition's original screen-printed backdrops. Requires Pillow."""

from pathlib import Path
from random import Random

from PIL import Image, ImageDraw, ImageFilter


OUTPUT = Path(__file__).resolve().parents[1] / "Assets/Resources/Art/Expedition"
WIDTH, HEIGHT = 1600, 900


def mix(first, second, amount):
    return tuple(round(a + (b - a) * amount) for a, b in zip(first, second))


class Landscape:
    def __init__(self, name, colors, moon):
        self.random = Random(name)
        self.night, self.haze, self.far, self.middle, self.snow, self.near, self.light = colors
        self.image = Image.new("RGB", (WIDTH, HEIGHT), self.night)
        self.draw = ImageDraw.Draw(self.image)
        for y in range(HEIGHT):
            self.draw.line((0, y, WIDTH, y), fill=mix(self.night, self.haze, min(y / 580, 1)))
        x, y, radius = moon
        self.draw.ellipse((x-radius, y-radius, x+radius, y+radius), fill=self.light)
        for _ in range(154):
            px = self.random.randrange(WIDTH)
            py = self.random.randrange(24, 520)
            length = self.random.randrange(1, 6)
            self.draw.line((px, py, px + length, py - 1), fill=mix(self.haze, self.light, .31), width=1)
        self.ridge(407, 122, self.far, .20)
        self.ridge(520, 203, self.middle, .43)
        self.ridge(613, 173, self.near, .32)
        self.draw.polygon([(0, 629), (371, 631), (632, 607), (861, 616),
                           (1072, 650), (1284, 621), (1600, 669), (1600, 900), (0, 900)],
                          fill=mix(self.near, self.snow, .45))
        self.draw.polygon([(0, 760), (304, 753), (564, 700), (1022, 695), (1261, 728),
                           (1600, 744), (1600, 900), (0, 900)],
                          fill=mix(self.near, self.snow, .31))

    def ridge(self, baseline, amplitude, color, cap_strength):
        step = 200
        points = [(x, baseline - self.random.randrange(18, amplitude)) for x in range(-step, WIDTH + step*2, step)]
        profile = [points[0]]
        for left, right in zip(points, points[1:]):
            profile.append(((left[0]+right[0])//2 + self.random.randrange(-32, 32), max(left[1], right[1]) + 40))
            profile.append(right)
        self.draw.polygon(profile + [(WIDTH, HEIGHT), (0, HEIGHT)], fill=color)
        for index in range(2, len(profile) - 1, 2):
            left, apex, right = profile[index-1:index+2]
            toe_l, toe_r = mix(apex, left, .66), mix(apex, right, .66)
            ink = mix(color, self.snow, cap_strength)
            self.draw.polygon([apex, toe_r, (apex[0]+16, apex[1]+29),
                               (apex[0]-4, apex[1]+48), (apex[0]-11, apex[1]+24), toe_l], fill=ink)
            self.draw.polygon([apex, toe_r, (right[0] + 36, right[1] + 104),
                               (apex[0]+24, apex[1]+129), (apex[0]+41, apex[1]+43)],
                              fill=mix(color, self.night, .23))
            if apex[0] < 460 or apex[0] > 1140:
                for offset in range(18, 118, 12):
                    start = (apex[0] + offset // 3, apex[1] + offset)
                    end = (start[0] + 24, start[1] + 11)
                    self.draw.line([start, end], fill=mix(color, self.snow, .23), width=1)

    def polygon(self, points, fill, outline=None, width=3):
        self.draw.polygon(points, fill=fill)
        if outline:
            self.draw.line(points + points[:1], fill=outline, width=width, joint="curve")

    def tower(self, x, y, scale, lit=True, flag=False):
        p = lambda ox, oy: (round(x + ox*scale), round(y + oy*scale))
        poly = lambda *points: [p(a, b) for a, b in points]
        shadow = mix(self.near, self.night, .60)
        wood = mix(self.near, self.haze, .45)
        snow = mix(self.snow, self.haze, .22)
        self.polygon(poly((-81, 0), (-63, -229), (49, -239), (72, 9)), wood, shadow)
        self.polygon(poly((15, -236), (49, -239), (72, 9), (17, 3)), shadow)
        self.polygon(poly((-96, -218), (-9, -309), (80, -235), (57, -210), (-9, -256), (-70, -203)),
                     shadow)
        self.polygon(poly((-96, -220), (-9, -307), (80, -235), (61, -230), (-10, -283), (-84, -217)), snow)
        for row in range(-195, 1, 27):
            self.draw.line(poly((-55, row), (14, row-3)), fill=mix(wood, self.night, .31), width=2)
        self.polygon(poly((-36,-163), (8,-164), (11,-104), (-39,-101)), shadow)
        if lit:
            self.polygon(poly((-31,-156), (3,-157), (5,-109), (-33,-107)), self.light)
            self.draw.line(poly((-13,-156), (-12,-109)), fill=wood, width=3)
        self.draw.line(poly((-50, 1), (-29,-29), (-10,1)), fill=shadow, width=4)
        if flag:
            self.draw.line(poly((-8,-302), (-8,-368)), fill=shadow, width=3)
            self.polygon(poly((-8,-367), (60,-354), (30,-339), (-8,-340)), (176, 103, 78))

    def track(self, left, vanishing):
        # The rail geometry converges towards the selected edge of the composition.
        ox, oy = vanishing
        for rail in (0, 1):
            bottom_x = left + rail * 380
            self.draw.line([(ox + rail*12, oy), (bottom_x, HEIGHT)], fill=self.near, width=9)
            self.draw.line([(ox + rail*12 - 1, oy), (bottom_x - 3, HEIGHT)], fill=self.light, width=2)
        for k in range(1, 17):
            t = (k/16) ** 2.1
            start = mix((ox-1, oy), (left-50, HEIGHT), t)
            end = mix((ox+14, oy), (left+430, HEIGHT), t)
            self.draw.line([start, end], fill=self.near, width=max(1, round(t*10)))

    def finish(self, name):
        for _ in range(180):
            x, y = self.random.randrange(WIDTH), self.random.randrange(613, HEIGHT)
            length = self.random.randrange(3, 36)
            if 490 < x < 1120:
                continue
            self.draw.line((x, y, x + length, y - length//7), fill=mix(self.near, self.snow, .52), width=1)
        noise = Image.frombytes("L", (WIDTH, HEIGHT), bytes(
            self.random.randrange(81, 158) for _ in range(WIDTH * HEIGHT)))
        noise = noise.filter(ImageFilter.GaussianBlur(.48)).convert("RGB")
        image = Image.blend(self.image, noise, .058)
        image.save(OUTPUT / f"{name}.png", optimize=True)


BLUE = ((22,38,52), (71,111,123), (67,96,110), (83,117,131), (184,208,203), (36,63,75), (242,209,152))
LAVENDER = ((34,39,59), (88,99,124), (73,89,113), (93,111,132), (200,211,214), (48,60,82), (236,211,172))
RUST = ((43,34,43), (129,89,81), (97,79,90), (123,91,93), (194,167,156), (67,56,66), (246,182,117))
TEAL = ((17,37,49), (61,109,116), (61,93,102), (76,107,112), (173,203,191), (36,65,71), (242,208,145))


def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    for name, palette, moon in (
        ("Cover", BLUE, (1197,245,92)), ("Valley", BLUE, (1230,205,72)),
        ("Outpost", LAVENDER, (374,211,61)), ("Ridge", TEAL, (1460,172,71)),
        ("Bridge", LAVENDER, (1232,208,56)), ("Watchtower", TEAL, (1141,208,64)),
        ("CableYard", RUST, (570,203,65)), ("Fortress", LAVENDER, (1331,158,76)),
        ("Mine", RUST, (1150,166,71)),
    ):
        scene = Landscape(name, palette, moon)
        if name == "Cover":
            scene.track(841, (1230, 551))
            scene.tower(1248, 654, 1.21, flag=True)
            scene.tower(1411, 592, .44)
        elif name in ("Outpost", "Watchtower", "Fortress"):
            scene.tower(1344, 628, 1.16 if name == "Watchtower" else .82,
                        lit=name != "Fortress", flag=name != "Outpost")
            if name in ("Outpost", "Fortress"):
                scene.tower(1483, 668, .63, lit=name == "Outpost")
            if name == "Fortress":
                scene.tower(224, 654, .88, lit=False, flag=True)
                scene.tower(347, 598, .48, lit=False)
        elif name in ("Bridge", "CableYard", "Mine"):
            edge = 1271 if name in ("CableYard", "Mine") else 244
            scene.track(940 if edge > 500 else 43, (edge, 591))
            scene.tower(edge, 637, .60 if name == "Bridge" else .85,
                        lit=name != "CableYard")
            scene.tower(edge + (164 if edge < 500 else 178), 617, .54, lit=name == "Mine")
        else:
            scene.tower(1457 if name == "Valley" else 260, 642, .50, flag=True)
        scene.finish(name)


if __name__ == "__main__":
    main()
