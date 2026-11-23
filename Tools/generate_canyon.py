"""生成风蚀雪谷的原创地形与局内牌面图。"""
from pathlib import Path
from random import Random
from PIL import Image, ImageDraw

art = Path(__file__).resolve().parents[1] / "Assets" / "Resources" / "Art"
rng = Random(5941)


def tint(a, b, k):
    return tuple(int(i * (1 - k) + j * k) for i, j in zip(a, b))


def canyon():
    w, h = 800, 450
    im = Image.new("RGB", (w, h))
    d = ImageDraw.Draw(im)
    for y in range(h):
        color = tint((39, 96, 119), (161, 207, 208), min(1, y / 200))
        d.line((0, y, w, y), fill=color)

    for i in range(13, 38, 6):
        d.ellipse((112-i, 74-i, 112+i, 74+i), fill=tint((106, 162, 176),(234, 220, 165), (38-i)/27))
    d.ellipse((100, 62, 124, 86), fill="#f3e8c3")
    d.rectangle((100,73,124,74),fill="#e4d7b6")

    ridges = [
        ([(0,131),(81,113),(153,128),(266,66),(313,82),(356,121),(449,101),(520,128),(610,71),(712,135),(800,93)], "#608f9e","#bed4cb"),
        ([(0,136),(112,142),(206,112),(280,132),(349,98),(405,130),(496,149),(555,123),(679,146),(753,116),(800,131)], "#4f8294","#c0dad8"),
        ([(0,168),(112,156),(199,166),(294,146),(349,155),(468,151),(599,161),(680,150),(800,163)], "#629aab","#bedfe0")
    ]
    for peaks, shade, top in ridges:
        d.polygon([*peaks,(800,227),(0,227)], fill=shade)
        for i, (x,y) in enumerate(peaks):
            if i == 0 or i == len(peaks)-1 or y >= min(peaks[i-1][1],peaks[i+1][1]):
                continue
            lift = min(peaks[i-1][1], peaks[i+1][1])-y
            d.polygon([(x,y),(x-max(10,lift),y+lift),(x-7,y+lift-4),(x+4,y+lift+6),
                       (x+8,y+lift-1),(x+lift//2,y+lift+2),(x+lift,y+lift)],fill=top)
            d.polygon([(x+5,y+7),(x+lift,y+lift),(x+lift+22,227),(x+8,y+lift-1)],fill="#78a4aa")

    ground = [(0,221),(65,198),(136,199),(313,171),(473,171),
              (652,193),(725,189),(800,215),(800,450),(0,450)]
    d.polygon(ground, fill="#9dc4c9")
    d.polygon([(0,255),(78,221),(153,221),(308,197),(510,198),(647,222),
               (740,214),(800,236),(800,450),(0,450)],fill="#bad8d7")
    d.polygon([(0,318),(173,269),(311,270),(420,258),(544,263),(661,247),
               (800,274),(800,450),(0,450)],fill="#c4e0dd")
    for i in range(240):
        x,y=rng.randrange(w),rng.randrange(179,450)
        dx=rng.randrange(3,43)
        d.line((x,y,x+dx,y-rng.randrange(0,3)),fill=rng.choice(("#accdce","#cfebe5","#b8d5d6","#b9d9d5")))
    for pts in [
        [(18,264),(135,249),(155,256),(267,227)],
        [(534,220),(609,229),(632,221),(750,243)],
        [(73,340),(140,322),(187,320),(234,311)],
        [(480,267),(518,258),(561,264),(613,247)],
        [(583,363),(620,336),(650,333),(683,318)],
        [(212,374),(325,343),(349,346),(408,329)],
        [(369,191),(405,183),(448,184)],
    ]:
        d.line(pts,fill="#a0c6cb",width=2)
        d.line([(x,y+2) for x,y in pts],fill="#def0e9")

    for right in (False, True):
        xflip = lambda x: w-x if right else x
        def rock(points, color):
            d.polygon([(xflip(x),y) for x,y in points],fill=color)
        rock([(0,113),(20,124),(48,111),(64,149),(80,157),(77,234),(0,277)],"#426f85")
        rock([(0,130),(24,130),(43,122),(56,154),(43,204),(0,218)],"#6796a3")
        rock([(0,113),(20,121),(48,108),(54,126),(42,133),(30,132),(0,144)],"#d8e9e5")
        rock([(0,229),(37,204),(51,175),(64,218),(89,217),(111,261),(0,286)],"#5c899c")
        rock([(37,204),(51,174),(53,198),(69,211),(59,218),(42,217)],"#dbede9")
        rock([(0,280),(25,261),(53,265),(79,239),(101,259),(88,278),(41,293),(0,324)],"#6b9aa7")
        rock([(27,260),(54,262),(80,233),(102,252),(90,257),(73,254),(57,274)],"#cde6e3")
        rock([(0,347),(37,303),(74,309),(100,297),(120,322),(104,343),(0,393)],"#80aab4")

    def crystal(x,y,wd,ht):
        d.ellipse((x-wd,y-4,x+wd,y+6),fill="#8ab3be")
        d.polygon([(x-wd,y),(x-wd+3,y-ht*2//3),(x+1,y-ht),(x+wd,y-ht//2),(x+wd-1,y)],fill="#629fae")
        d.polygon([(x,y+1),(x+1,y-ht),(x+wd,y-ht//2),(x+wd-1,y)],fill="#53889c")
        d.line((x-wd+3,y-ht*2//3,x+1,y-ht),fill="#d3eee6",width=2)

    for params in [(85,239,8,20),(108,205,7,17),(684,225,8,18),(711,239,11,28),
                   (725,241,5,15),(631,205,5,15),(48,309,10,22),(758,303,13,32)]:
        crystal(*params)
    for _ in range(178):
        x,y=rng.randrange(w),rng.randrange(72,385)
        fill=rng.choice(("#a5ced2","#e1ece3","#c0dcd8","#d8e9e3"))
        radius=rng.choice((0,0,0,1))
        d.ellipse((x-radius,y-radius,x+radius,y+radius),fill=fill)
    im.resize((1600,900), Image.Resampling.NEAREST).save(art / "canyon.png")


def item(art_id, polys):
    im = Image.new("RGBA", (96, 66))
    d = ImageDraw.Draw(im)
    d.ellipse((22, 53, 73, 61), fill=(7,19,29,115))
    for color, points in polys:
        d.polygon(points, fill=color)
    im.resize((192,132), Image.Resampling.NEAREST).save(art / (art_id + ".png"))


canyon()
item("spear", [
    ("#192638",[(20,54),(22,46),(59,24),(59,14),(78,9),(76,27),(69,33),(64,31),(30,58)]),
    ("#aa8769",[(26,49),(60,27),(64,30),(29,55),(24,54)]),
    ("#dbbd89",[(33,46),(58,30),(60,32),(35,49)]),
    ("#a5dcde",[(62,18),(75,12),(72,24),(67,31),(61,26)]),
    ("#e3f6ed",[(64,18),(74,13),(69,22),(62,26),(61,22)])
])
item("chill", [
    ("#182a3c",[(34,18),(38,11),(56,11),(62,16),(63,22),(67,32),(66,52),(61,57),(36,57),(30,52),(29,34)]),
    ("#6cb7c9",[(37,21),(59,21),(61,29),(63,35),(62,50),(58,54),(38,54),(34,49),(34,34)]),
    ("#b6e4de",[(37,24),(43,23),(41,31),(41,46),(36,47),(36,35)]),
    ("#34475f",[(37,16),(40,14),(55,14),(59,18),(57,22),(39,22)]),
    ("#d4efe4",[(46,30),(49,30),(49,36),(54,33),(56,35),(51,39),(56,43),(54,45),(49,42),(49,48),(46,48),(46,42),(41,45),(39,43),(44,39),(39,36),(41,34),(46,36)])
])
item("whistle", [
    ("#182a39",[(20,35),(29,26),(43,26),(50,32),(64,25),(77,27),(81,35),(74,43),(65,43),(53,49),(31,56),(21,50)]),
    ("#d6a865",[(24,36),(30,30),(43,29),(51,35),(64,29),(76,30),(77,35),(73,39),(64,39),(51,46),(32,53),(24,47)]),
    ("#edca84",[(25,35),(32,30),(43,30),(50,36),(48,41),(40,38),(30,43),(24,41)]),
    ("#5e5547",[(57,34),(64,31),(73,31),(73,36),(65,38),(57,39)]),
    ("#c2d8c8",[(24,20),(36,18),(38,20),(27,24),(23,23)]),
    ("#c2d8c8",[(21,13),(29,8),(32,10),(26,16),(22,16)])
])

for asset in ["scout", "porter", "mechanic", "courier", "hound", "drone", "warden", "ram",
              "spear", "chill", "repair", "whistle"]:
    im = Image.open(art / (asset + ".png")).convert("RGBA")
    bbox = im.getchannel("A").point(lambda a: 255 if a > 100 else 0).getbbox()
    if bbox:
        x0,y0,x1,y1 = bbox
        cropped = im.crop((max(0,x0-5),max(0,y0-5),min(im.width,x1+5),min(im.height,y1+5)))
        cropped.save(art / ("battle_" + asset + ".png"))
