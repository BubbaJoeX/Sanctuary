"""Paints the art of Sanctuary's own region badges for build_client_mods.py (REGION_BADGES).

A region badge is the round emblem notifications.swf drops in with "Now entering ..." when you walk into a
region. Each is built on a stock one (its movie, timing and sparkles) with these layers repainted:
  - disc: the round backdrop the emblem stands on,
  - emblem: the region's picture, outlined by a thick rim,
  - composite: the disc with the emblem on it (the stock ones are exactly that),
  - silhouette: the emblem's rim and the disc's ring, drawn first while the badge flies in,
  - glow: the halo behind it, the stock one in the region's colour.
Every layer is painted SUPERSAMPLE times larger than the 256 pixel stock layout these coordinates use and
build_client_mods.py fits it to the size the movie draws it at. No game art is stored here; the stock layers
used (the disc, its ring and the glow) come from the client at build time.
"""

import math

from PIL import Image, ImageChops, ImageDraw, ImageFilter

SUPERSAMPLE = 4
SIZE = 256 * SUPERSAMPLE

# Merry Vale's badge, which Lavender Coast's is built on.
MERRY_VALE_DISC = "img17231899097319843168.dds"
MERRY_VALE_EMBLEM = "img5554964571294068336.dds"
MERRY_VALE_SILHOUETTE = "img2107471258912337437.dds"
MERRY_VALE_GLOW = "img11959885301908811988.dds"
MERRY_VALE_COMPOSITE = "img10357392131954088619.dds"

# The stock emblems stop here, where the disc's lip begins.
EMBLEM_BOTTOM = 245
RIM_WIDTH = 9
# The silhouette draws the rim this much lighter than the emblem (Merry Vale's: 90,77,16 -> 140,117,24).
SILHOUETTE_LIGHTEN = 1.55
# Merry Vale's disc: its sky is the light blue inside the ring (green above this), centred here.
SKY_GREEN = 150
DISC_CENTER = (129.5, 134)
HORIZON = 150

LAVENDER_RIM = (74, 42, 106)
LAVENDER_SHADOW = (112, 72, 184)
LAVENDER = (160, 122, 234)
LAVENDER_LIGHT = (218, 198, 255)
STEM = (62, 146, 58)
STEM_LIGHT = (118, 192, 84)
LEAF = (112, 164, 98)
LEAF_LIGHT = (160, 204, 138)
RIBBON_SHADOW = (212, 140, 30)
RIBBON = (248, 200, 62)
RIBBON_LIGHT = (255, 238, 156)
LAVENDER_GLOW = (206, 168, 255)
SEA_FAR = (104, 178, 236)
SEA_NEAR = (46, 116, 204)
FOAM = (236, 248, 255)

# Lavender Coast's bunch: where the stalks meet, and each stalk's lean (degrees from upright) and length.
BUNCH_BASE = (128, 254)
STALKS = [(-31, 168), (-15, 206), (0, 232), (15, 206), (31, 168)]
# The share of a stalk at its top that is flower spike.
SPIKE_SHARE = 0.56
RIBBON_HEIGHT = 44
RIBBON_SCALE = 1.3


def scaled(*values):
    return [v * SUPERSAMPLE for v in values]


def ellipse(cx, cy, rx, ry, angle=0.0, points=48):
    """An ellipse as a polygon (256 pixel coordinates in, canvas coordinates out), turned by angle radians."""
    cos, sin = math.cos(angle), math.sin(angle)
    result = []
    for i in range(points):
        t = 2 * math.pi * i / points
        x, y = rx * math.cos(t), ry * math.sin(t)
        result.append(((cx + x * cos - y * sin) * SUPERSAMPLE, (cy + x * sin + y * cos) * SUPERSAMPLE))
    return result


def layer():
    return Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))


def grow(mask, radius):
    """mask, widened by radius (256 pixel units), with round corners."""
    blur = mask.filter(ImageFilter.GaussianBlur(radius * SUPERSAMPLE / 2))
    return blur.point(lambda v: 255 if v > 10 else 0)


def shrink(mask, radius):
    blur = mask.filter(ImageFilter.GaussianBlur(radius * SUPERSAMPLE / 2))
    return blur.point(lambda v: 255 if v > 245 else 0)


def flat(color, mask):
    image = Image.new("RGBA", mask.size, color + (255,))
    image.putalpha(mask)
    return image


def cut_bottom(image):
    """Drops what lies below EMBLEM_BOTTOM, where the stock emblems end."""
    mask = Image.new("L", image.size, 255)
    ImageDraw.Draw(mask).rectangle((0, EMBLEM_BOTTOM * SUPERSAMPLE, SIZE, SIZE), fill=0)
    image.putalpha(ImageChops.multiply(image.getchannel("A"), mask))
    return image


def upscale(image):
    return image.convert("RGBA").resize((SIZE, SIZE), Image.LANCZOS)


def stalk_point(angle, distance):
    x, y = BUNCH_BASE
    return x + math.sin(angle) * distance, y - math.cos(angle) * distance


def paint_floret(draw, cx, cy, r, angle):
    """One lavender floret: a shaded bud, lit from the upper left."""
    draw.polygon(ellipse(cx, cy, r * 0.62, r * 0.8, angle), fill=LAVENDER_SHADOW)
    draw.polygon(ellipse(cx - r * 0.1, cy - r * 0.12, r * 0.48, r * 0.64, angle), fill=LAVENDER)
    draw.polygon(ellipse(cx - r * 0.2, cy - r * 0.3, r * 0.18, r * 0.26, angle - 0.4), fill=LAVENDER_LIGHT)


def paint_spike(draw, angle, length):
    """A lavender spike: whorls of florets, packed at the tip and spaced out further down."""
    spike = length * SPIKE_SHARE
    top = length - 3
    distance, whorls = top, []
    while distance > length - spike:
        t = (top - distance) / spike
        r = 8.5 + 4.5 * t
        whorls.append((distance, r))
        distance -= r * (0.95 + 0.6 * t)
    # Lower whorls overlap the ones above them.
    for i, (distance, r) in enumerate(whorls):
        x, y = stalk_point(angle, distance)
        side_x, side_y = math.cos(angle) * r * 0.55, math.sin(angle) * r * 0.55
        if i == 0:
            paint_floret(draw, x, y, r, angle)
            continue
        paint_floret(draw, x - side_x, y - side_y, r, angle - 0.35)
        paint_floret(draw, x + side_x, y + side_y, r, angle + 0.35)
        front_x, front_y = stalk_point(angle, distance - r * 0.3)
        paint_floret(draw, front_x, front_y, r * 0.8, angle)


def paint_stem(draw, angle, length):
    start, end = stalk_point(angle, 0), stalk_point(angle, length * (1 - SPIKE_SHARE) + 8)
    draw.line(scaled(*start, *end), fill=STEM, width=7 * SUPERSAMPLE)
    offset = math.cos(angle) * 1.4, math.sin(angle) * 1.4
    draw.line(scaled(start[0] - offset[0], start[1] - offset[1], end[0] - offset[0], end[1] - offset[1]),
              fill=STEM_LIGHT, width=2 * SUPERSAMPLE)


def paint_leaf(draw, angle, length, width):
    """A narrow lavender leaf from the base of the bunch."""
    x, y = stalk_point(angle, length / 2 + 6)
    draw.polygon(ellipse(x, y, width / 2, length / 2, angle), fill=LEAF)
    tip_x, tip_y = stalk_point(angle, length + 2)
    draw.line(scaled(*stalk_point(angle, 10), tip_x, tip_y), fill=LEAF_LIGHT, width=SUPERSAMPLE + 2)


def paint_ribbon(draw, x, y):
    """A bow tied round the stalks: two loops, two tails and the knot."""
    def scaled_ellipse(cx, cy, rx, ry, angle=0.0):
        return ellipse(x + (cx - x) * RIBBON_SCALE, y + (cy - y) * RIBBON_SCALE, rx * RIBBON_SCALE, ry * RIBBON_SCALE,
                       angle)

    for side in (-1, 1):
        tail = [(3, 2), (18, 30), (11, 27), (8, 34), (-2, 6)]
        draw.polygon([((x + side * px * RIBBON_SCALE) * SUPERSAMPLE, (y + py * RIBBON_SCALE) * SUPERSAMPLE)
                      for px, py in tail], fill=RIBBON_SHADOW)
        loop_x, loop_y = x + side * 17, y - 5
        draw.polygon(scaled_ellipse(loop_x, loop_y, 18, 11, side * -0.35), fill=RIBBON_SHADOW)
        draw.polygon(scaled_ellipse(loop_x - side * 1, loop_y - 2, 15.5, 8.5, side * -0.35), fill=RIBBON)
        draw.polygon(scaled_ellipse(loop_x + side * 3, loop_y + 1, 7, 3.5, side * -0.35), fill=RIBBON_SHADOW)
        draw.polygon(scaled_ellipse(loop_x - side * 4, loop_y - 6, 6, 2.2, side * -0.35), fill=RIBBON_LIGHT)
    draw.polygon(scaled_ellipse(x, y, 8, 8.5), fill=RIBBON_SHADOW)
    draw.polygon(scaled_ellipse(x - 0.8, y - 1, 6.5, 6.8), fill=RIBBON)
    draw.polygon(scaled_ellipse(x - 2.5, y - 3.5, 2.5, 2), fill=RIBBON_LIGHT)


def lavender_bunch():
    bunch = layer()
    draw = ImageDraw.Draw(bunch)
    for angle in (-60, 60):
        paint_leaf(draw, math.radians(angle), 66, 15)
    stalks = [(math.radians(lean), length) for lean, length in STALKS]
    for angle, length in stalks:
        paint_stem(draw, angle, length)
    for angle, length in stalks:
        paint_spike(draw, angle, length)
    paint_ribbon(draw, BUNCH_BASE[0], BUNCH_BASE[1] - RIBBON_HEIGHT)
    return cut_bottom(bunch)


def lavender_coast_emblem():
    """Lavender Coast's emblem: a bunch of lavender tied with a ribbon."""
    emblem = layer()
    bunch = lavender_bunch()
    emblem.alpha_composite(flat(LAVENDER_RIM, grow(bunch.getchannel("A"), RIM_WIDTH)))
    emblem.alpha_composite(bunch)
    return cut_bottom(emblem)


def sea_disc(load):
    """Merry Vale's disc with the sea below the horizon: the coast, seen past the emblem."""
    disc = upscale(load(MERRY_VALE_DISC))
    sky = disc.getchannel("G").point(lambda v: 255 if v > SKY_GREEN else 0)

    sea = layer()
    draw = ImageDraw.Draw(sea)
    top = HORIZON * SUPERSAMPLE
    for y in range(top, SIZE):
        t = (y - top) / (SIZE - top)
        draw.line((0, y, SIZE, y), fill=tuple(round(a + (b - a) * t) for a, b in zip(SEA_FAR, SEA_NEAR)))
    draw.line((0, top, SIZE, top), fill=FOAM, width=SUPERSAMPLE)
    # Rows of little wave crests, wider apart nearer the viewer.
    for row, (y, width) in enumerate([(166, 20), (190, 26), (218, 32)]):
        x = -width + (row % 2) * width / 2
        while x < 256:
            draw.arc(scaled(x, y - width * 0.3, x + width, y + width * 0.3), 200, 340, fill=FOAM,
                     width=2 * SUPERSAMPLE + row)
            x += width * 1.9

    # Darker towards the ring, like the sky.
    shade = disc.convert("L")
    peak = shade.getpixel(tuple(scaled(*DISC_CENTER))) or 255
    shade = shade.point(lambda v: min(255, round(255 * (v / peak) ** 0.6)))
    red, green, blue, alpha = sea.split()
    sea = Image.merge("RGBA", [ImageChops.multiply(channel, shade) for channel in (red, green, blue)]
                      + [ImageChops.multiply(alpha, sky)])
    disc.alpha_composite(sea)
    return disc


def composite(disc, emblem):
    result = disc.copy()
    result.alpha_composite(emblem)
    return result


def silhouette(load, emblem):
    """The emblem's rim (lightened) and the part of the stock disc's ring the emblem doesn't cover."""
    alpha = emblem.getchannel("A")
    body = alpha.point(lambda v: 255 if v > 128 else 0)
    # The emblem carries on below its cut, so the cut gets no rim.
    whole = body.copy()
    ImageDraw.Draw(whole).rectangle((0, EMBLEM_BOTTOM * SUPERSAMPLE - 4, SIZE, SIZE), fill=255)
    rim = ImageChops.multiply(ImageChops.subtract(whole, shrink(whole, RIM_WIDTH)), alpha)
    result = flat(tuple(min(255, round(c * SILHOUETTE_LIGHTEN)) for c in LAVENDER_RIM), rim)

    # The stock silhouette's ring is the purple in it; its rim is olive.
    stock = upscale(load(MERRY_VALE_SILHOUETTE))
    red, green, blue, ring_alpha = stock.split()
    purple = ImageChops.subtract(blue, green).point(lambda v: 255 if v > 40 else v * 6)
    ring = ImageChops.multiply(ring_alpha, purple)
    ring = ImageChops.multiply(ring, ImageChops.invert(grow(body, 1)))
    stock.putalpha(ring)
    stock.alpha_composite(result)
    return stock


def glow(load, color):
    """The stock glow, recoloured: its brightness and alpha in the region's colour."""
    stock = load(MERRY_VALE_GLOW).convert("RGBA")
    brightness = stock.convert("L")
    peak = brightness.getextrema()[1] or 1
    channels = [brightness.point(lambda v, c=c: round(c * v / peak)) for c in color]
    result = Image.merge("RGB", channels).convert("RGBA")
    result.putalpha(stock.getchannel("A"))
    return result


def lavender_coast(load):
    """Lavender Coast's badge on Merry Vale's: {stock texture: its replacement}. load(name) reads a client texture."""
    disc = sea_disc(load)
    emblem = lavender_coast_emblem()
    return {
        MERRY_VALE_DISC: disc,
        MERRY_VALE_EMBLEM: emblem,
        MERRY_VALE_COMPOSITE: composite(disc, emblem),
        MERRY_VALE_SILHOUETTE: silhouette(load, emblem),
        MERRY_VALE_GLOW: glow(load, LAVENDER_GLOW),
    }


PAINTERS = {"lavender_coast": lavender_coast}
