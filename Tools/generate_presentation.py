"""Generate the title landscape, encounter backgrounds and short game cues."""
from math import exp, pi, sin
from pathlib import Path
from random import Random
import struct
import wave

from PIL import Image, ImageColor, ImageDraw


ROOT = Path(__file__).resolve().parents[1] / "Assets" / "Resources"
WIDTH, HEIGHT = 800, 450


def mix(first, second, amount):
    if isinstance(first, str):
        first = ImageColor.getrgb(first)
    if isinstance(second, str):
        second = ImageColor.getrgb(second)
    return tuple(round(a + (b - a) * amount) for a, b in zip(first, second))


def sky(top, bottom, moon, glow, stars, rng):
    image = Image.new("RGB", (WIDTH, HEIGHT), top)
    draw = ImageDraw.Draw(image)
    for y in range(HEIGHT):
        draw.line((0, y, WIDTH, y), fill=mix(top, bottom, min(1.0, y / 280)))
    x, y, radius = moon
    for r in range(radius * 3, radius, -3):
        amount = (radius * 3 - r) / (radius * 2)
        draw.ellipse((x-r, y-r, x+r, y+r), fill=mix(bottom, glow, amount * 0.23))
    draw.ellipse((x-radius, y-radius, x+radius, y+radius), fill=glow)
    for _ in range(stars):
        sx, sy = rng.randrange(18, WIDTH-18), rng.randrange(8, 195)
        if (sx-x)**2 + (sy-y)**2 < (radius+8)**2:
            continue
        tone = mix(bottom, glow, rng.uniform(0.18, 0.66))
        draw.point((sx, sy), fill=tone)
        if rng.random() < 0.06:
            draw.line((sx-2, sy, sx+2, sy), fill=tone)
            draw.line((sx, sy-2, sx, sy+2), fill=tone)
    return image


def peaks(draw, points, shadow, face, snow):
    draw.polygon([*points, (WIDTH, HEIGHT), (0, HEIGHT)], fill=shadow)
    for index in range(1, len(points)-1):
        left, peak, right = points[index-1:index+2]
        x, y = peak
        if y >= min(left[1], right[1]):
            continue
        lift = min(32, left[1]-y, right[1]-y)
        draw.polygon([left, peak, (x+lift//2, y+lift), (x+23, right[1]+24)], fill=face)
        lx = x + (left[0]-x) * lift // (left[1]-y)
        rx = x + (right[0]-x) * lift // (right[1]-y)
        draw.polygon([peak, (rx, y+lift), (x+7, y+lift-6), (x+1, y+lift-2),
                      (x-6, y+lift-8), (lx, y+lift)], fill=snow)


def terrain(draw, ridge, dark, mid, snow, rng):
    draw.polygon([*ridge, (WIDTH, HEIGHT), (0, HEIGHT)], fill=dark)
    for step in range(3):
        offset = 15 + step * 41
        points = [(x, y+offset+(index%3)*5) for index, (x, y) in enumerate(ridge)]
        draw.polygon([*points, (WIDTH, HEIGHT), (0, HEIGHT)], fill=mix(mid, snow, step * 0.27))
    minimum = max(y for _, y in ridge)+25
    for _ in range(220):
        x, y = rng.randrange(WIDTH), rng.randrange(minimum, HEIGHT)
        length = rng.choice((2, 3, 6, 10, 19))
        draw.line((x, y, x+length, y-1), fill=mix(mid, snow, rng.uniform(0.2, 0.85)))


def shelter(draw, x, y, width):
    draw.rectangle((x+6, y-41, x+width-5, y), fill="#234650")
    draw.polygon([(x-6,y-39),(x+width//2,y-65),(x+width+8,y-36)], fill="#466370")
    draw.line((x-6,y-41,x+width//2,y-65,x+width+8,y-37), fill="#98b9bb", width=4)
    draw.rectangle((x+17,y-27,x+29,y-15), fill="#efb369")
    draw.line((x+23,y-27,x+23,y-15), fill="#715653", width=2)
    draw.rectangle((x+width-25,y-26,x+width-14,y), fill="#182d3b")
    draw.line((x+4,y-6,x+width-5,y-5), fill="#466d73", width=2)


def cover():
    rng = Random(470)
    image = sky("#0b1728","#253c4d",(615,111,42),"#e6c78d",210,rng)
    draw = ImageDraw.Draw(image)
    peaks(draw, [(0,222),(88,168),(152,199),(218,142),(295,189),(352,162),(415,214),
                 (480,145),(531,176),(567,124),(610,167),(669,132),(724,194),(800,164)],
          "#304d60", "#425f71", "#6f8b91")
    peaks(draw, [(0,257),(112,208),(160,231),(238,181),(302,229),(377,209),
                 (455,260),(508,201),(551,220),(588,171),(644,235),(728,176),(800,213)],
          "#213e53", "#38546a", "#87a1a8")
    terrain(draw, [(0,299),(174,267),(294,281),(413,306),(552,255),(658,266),(800,244)],
            "#31566c","#436a7c","#76949d",rng)
    draw.polygon([(381,450),(470,398),(539,365),(599,318),(624,285),
                  (639,286),(619,325),(583,370),(534,402),(470,450)], fill="#86a2a4")
    draw.line([(390,449),(470,404),(534,376),(600,326),(632,286)], fill="#b0bdba", width=2)
    draw.polygon([(602,309),(610,278),(631,263),(677,271),(701,301),(767,330),
                  (800,376),(800,450),(702,450),(694,369)], fill="#243f51")
    draw.line([(611,278),(631,263),(677,271),(701,301),(767,330)], fill="#95adb0", width=5)
    draw.polygon([(625,279),(631,191),(656,180),(678,193),(684,279)], fill="#152734")
    draw.polygon([(633,269),(636,197),(654,187),(654,273)], fill="#2f424b")
    draw.polygon([(623,195),(657,170),(689,192),(685,197),(657,190)], fill="#233541")
    draw.line([(625,193),(657,169),(686,191)], fill="#bcc1ad", width=3)
    draw.rectangle((646,217,666,244), fill="#a66041")
    draw.rectangle((649,219,663,239), fill="#fac879")
    draw.line((657,218,657,242), fill="#88574a", width=2)
    for y in (204,252,262):
        draw.line((636,y,650,y+2), fill="#475b5b", width=2)
    draw.line((656,169,656,140), fill="#719194", width=2)
    draw.polygon([(656,142),(679,146),(670,151),(656,154)], fill="#c4835c")
    draw.polygon([(0,368),(59,336),(111,369),(178,354),(275,416),
                  (368,450),(0,450)], fill="#162d40")
    draw.line([(0,368),(59,336),(111,369),(178,354),(275,416)],
              fill="#65838b", width=3)
    # Keep the left side readable underneath the title.
    shade = Image.new("RGBA", image.size)
    darken = ImageDraw.Draw(shade)
    for x in range(490):
        alpha = round(181 * (1.0 - x / 490)**1.25)
        darken.line((x, 0, x, HEIGHT), fill=(5, 14, 27, alpha))
    return Image.alpha_composite(image.convert("RGBA"), shade).convert("RGB")


def battlefield(kind, seed):
    rng = Random(seed)
    palettes = {
        "valley": ("#2e5b70","#83a3ab","#f2d9ad","#426778","#65858e","#9ab5b7","#385f72","#638899","#9cbbc0"),
        "outpost": ("#173e48","#608a87","#e9d5a8","#345f66","#52777a","#97ada5","#39585e","#627b79","#a0b5ad"),
        "bridge": ("#1c3457","#657d99","#d1e3df","#374d74","#5c7298","#9cabc0","#314a71","#536d90","#9fb2c9"),
        "ridge": ("#382d48","#956a76","#ecd2ad","#544762","#786278","#c1999e","#513f55","#7b656f","#bfa5a0"),
    }
    top, bottom, light, rock, face, cap, ground, mid, snow = palettes[kind]
    image = sky(top, bottom, (608,93,27), light, 76, rng)
    draw = ImageDraw.Draw(image)
    peaks(draw, [(0,194),(63,135),(120,163),(171,107),(236,152),(279,124),(351,194),
                 (418,110),(474,159),(534,128),(589,176),(644,110),(700,159),(800,142)],
          rock,face,cap)
    terrain(draw, [(0,224),(108,200),(205,216),(331,201),(446,217),(573,194),(671,206),(800,188)],
            ground,mid,snow,rng)
    if kind == "outpost":
        shelter(draw, 23, 238, 87)
        shelter(draw, 681, 242, 80)
        draw.line((719,185,719,148), fill="#819f95", width=3)
        draw.polygon([(719,149),(743,155),(719,162)], fill="#cc9b65")
    elif kind == "bridge":
        draw.polygon([(0,376),(158,350),(317,345),(495,352),(661,342),(800,364),
                      (800,403),(658,385),(497,397),(314,389),(156,395),(0,420)],
                     fill="#394656")
        draw.line([(0,377),(158,350),(317,345),(495,352),(661,342),(800,364)],
                  fill="#c2c6c5", width=4)
        for x, y in ((38,369),(141,353),(665,342),(765,359)):
            draw.line((x,y,x,y-46),fill="#263e52",width=8)
            draw.rectangle((x-3,y-50,x+4,y-44), fill="#e4c18e")
        draw.line([(38,327),(90,343),(141,311)], fill="#8fa8b9", width=2)
        draw.line([(665,300),(710,332),(765,317)], fill="#8fa8b9", width=2)
    elif kind == "ridge":
        for x, offset in ((34,2),(761,-10),(709,13)):
            draw.polygon([(x-22,275),(x+offset,169),(x+25,238),(x+33,267)],
                         fill="#5c506d")
            draw.polygon([(x+offset,169),(x+25,238),(x+4,225)], fill="#b48b97")
        for x, y in ((39,300),(84,322),(679,280),(714,310)):
            draw.line((x,y,x+59,y-9), fill="#cbb5af", width=2)
    else:
        for x, y, direction in ((4,287,1),(796,254,-1)):
            draw.polygon([(x,y),(x+28*direction,y-76),(x+70*direction,y-37),
                          (x+87*direction,y+21),(x,y+44)],fill="#406c81")
            draw.line([(x,y),(x+28*direction,y-76),(x+70*direction,y-37)],
                      fill="#bad1cf",width=3)
    return image


def save_image(image, name):
    image.resize((1600, 900), Image.Resampling.NEAREST).save(ROOT / "Art" / f"{name}.png", optimize=True)


def sound(name, seconds, partials, noise=0.0):
    rate = 24000
    rng = Random(390)
    frames = bytearray()
    for index in range(round(rate * seconds)):
        t = index / rate
        value = sum(volume * sin(2*pi*frequency*t) * exp(-t*decay)
                    for frequency, volume, decay in partials)
        value += noise * (rng.random() * 2 - 1) * exp(-t*28)
        value *= min(1, t*240) * min(1, (seconds-t)*45)
        frames.extend(struct.pack("<h", round(max(-1, min(1, value))*32760)))
    with wave.open(str(ROOT / "Audio" / (name + ".wav")), "wb") as track:
        track.setparams((1, 2, rate, 0, "NONE", "not compressed"))
        track.writeframes(frames)


if __name__ == "__main__":
    save_image(cover(), "cover")
    for index, kind in enumerate(("valley", "outpost", "bridge", "ridge")):
        save_image(battlefield(kind, 700 + index), f"field_{kind}")
    (ROOT / "Audio").mkdir(exist_ok=True)
    sound("paper", 0.14, [(182, 0.16, 24), (389, 0.10, 36)], noise=0.2)
    sound("impact", 0.2, [(76, 0.55, 20), (147, 0.21, 23)], noise=0.19)
    sound("effect", 0.22, [(560, 0.2, 16), (839, 0.14, 24), (1120, 0.06, 20)])
    sound("bell", 0.75, [(523, 0.29, 4), (1047, 0.14, 9), (1549, 0.05, 14)])
