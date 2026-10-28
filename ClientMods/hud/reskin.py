"""Repaints the HUD's stock textures in one modern theme for build_client_mods.py (RESKINS).

Every recipe takes the client's own texture, resized to the size the movie draws it at, and returns a
replacement of the same size, so the layout and hit areas stay exactly as they were:
  - navy, glass, gold: a gradient map of the texture's brightness, keeping its shading and alpha, so purple,
    brown and beige chrome all end up in the same deep blue glass with gold accents,
  - ring: a new minimap ring, drawn from scratch.
No game art is stored here; the textures come from the client at build time.
"""

from PIL import Image, ImageChops, ImageDraw, ImageFilter

# Gradient stops: (brightness 0..1, color).
NAVY = [(0.0, (6, 10, 32)), (0.35, (18, 34, 92)), (0.7, (46, 84, 182)), (1.0, (170, 205, 255))]
GLASS = [(0.0, (14, 28, 80)), (0.45, (52, 98, 200)), (0.8, (150, 192, 255)), (1.0, (240, 248, 255))]
GOLD = [(0.0, (58, 28, 0)), (0.45, (196, 120, 18)), (0.8, (255, 206, 84)), (1.0, (255, 246, 206))]

SUPERSAMPLE = 4


def lerp(a, b, t):
    return tuple(round(x + (y - x) * t) for x, y in zip(a, b))


def gradient_lut(stops):
    lut = []
    for i in range(256):
        t = i / 255
        for (t0, c0), (t1, c1) in zip(stops, stops[1:]):
            if t0 <= t <= t1:
                lut.append(lerp(c0, c1, (t - t0) / (t1 - t0) if t1 > t0 else 0))
                break
    return lut


def gradient_map(image, stops):
    """Recolors image by its brightness, stretched to the full range so dark and light chrome both use all of it."""
    gray = image.convert("L")
    low, high = gray.getextrema()
    if high > low:
        gray = gray.point(lambda v: round((v - low) * 255 / (high - low)))
    lut = gradient_lut(stops)
    channels = [gray.point([lut[i][c] for i in range(256)]) for c in range(3)]
    result = Image.merge("RGB", channels).convert("RGBA")
    result.putalpha(image.getchannel("A"))
    return result


def navy(image):
    return gradient_map(image, NAVY)


def glass(image):
    return gradient_map(image, GLASS)


def gold(image):
    return gradient_map(image, GOLD)


def ring(image):
    """The minimap's frame: a thin gold ring inside a soft blue glow, with a white inner edge."""
    size = image.width * SUPERSAMPLE
    # The stock ring's art spans its texture; keep the same radius.
    box = image.getchannel("A").getbbox() or (0, 0, image.width, image.height)
    outer = (box[2] - box[0]) / 2 * SUPERSAMPLE
    center = size / 2

    def disc(radius, fill):
        layer = Image.new("L", (size, size), 0)
        ImageDraw.Draw(layer).ellipse((center - radius, center - radius, center + radius, center + radius), fill=fill)
        return layer

    width = 7 * SUPERSAMPLE
    band = ImageChops.subtract(disc(outer - 2 * SUPERSAMPLE, 255), disc(outer - 2 * SUPERSAMPLE - width, 255))
    glow = ImageChops.subtract(disc(outer, 255), disc(outer - width - 6 * SUPERSAMPLE, 255)).filter(ImageFilter.GaussianBlur(4 * SUPERSAMPLE))
    inner = ImageChops.subtract(disc(outer - 2 * SUPERSAMPLE - width, 255), disc(outer - 2 * SUPERSAMPLE - width - 2 * SUPERSAMPLE, 255))

    result = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    result.alpha_composite(Image.merge("RGBA", (*Image.new("RGB", (size, size), (90, 150, 255)).split(), glow.point(lambda v: v * 3 // 4))))

    # Gold band, lit from the top.
    shade = Image.new("RGB", (1, size))
    for y in range(size):
        shade.putpixel((0, y), lerp((255, 240, 180), (196, 120, 18), y / size))
    result.alpha_composite(Image.merge("RGBA", (*shade.resize((size, size)).split(), band)))
    result.alpha_composite(Image.merge("RGBA", (*Image.new("RGB", (size, size), (255, 255, 255)).split(), inner.point(lambda v: v * 3 // 5))))
    return result.resize(image.size, Image.LANCZOS)


# The Adventurer's Journal (StoryBook.gfx) is one picture of the open book, drawn at 1024x768: a brown cover with steel
# corners round a stack of green and lavender page edges and the parchment. Pixel boxes at that size (left, top, right,
# bottom): the cover's outer edge, the pages with the stack's edges, and the parchment inside the stack.
BOOK_COVER = (2, 3, 990, 764)
BOOK_PAGES = (21, 12, 975, 755)
BOOK_SHEET = (50, 12, 946, 744)
# The page stack's edges, as ivory paper.
IVORY = [(0.0, (74, 66, 86)), (0.55, (204, 194, 176)), (1.0, (252, 247, 232))]
# The stock photo corners' plum: hue (0..1), and the least saturation and most brightness (0..1) of their pixels, so
# the paper's faint stains stay as they are.
PLUM_HUE = (0.80, 0.99)
PLUM_SATURATION = 0.15
PLUM_VALUE = 0.6


def vertical_gradient(size, top, bottom):
    shade = Image.new("RGB", (1, size[1]))
    for y in range(size[1]):
        shade.putpixel((0, y), lerp(top, bottom, y / max(1, size[1] - 1)))
    return shade.resize(size)


def book(image):
    """The Adventurer's Journal: the stock pages in a new navy cover with a gold rim and gold corner guards.

    The parchment is kept as it is, so its dark ink stays readable; the page stack's coloured edges become ivory and
    the cover and its corners are drawn again in the Main Menu's navy glass and gold."""
    w, h = image.size
    k = SUPERSAMPLE

    def scaled(box, inset=0, factor=1):
        return (round((box[0] + inset) * w / 1024 * factor), round((box[1] + inset) * h / 768 * factor),
                round((box[2] - inset) * w / 1024 * factor), round((box[3] - inset) * h / 768 * factor))

    big = (w * k, h * k)
    cover = scaled(BOOK_COVER, factor=k)
    radius = 18 * k

    # The cover: navy glass lit from the top left, with a gold rim and a pale inner line.
    shape = Image.new("L", big, 0)
    ImageDraw.Draw(shape).rounded_rectangle(cover, radius, fill=255)
    layer = Image.new("RGBA", big, (0, 0, 0, 0))
    layer.alpha_composite(Image.merge("RGBA", (*vertical_gradient(big, (46, 79, 176), (20, 31, 85)).split(), shape)))
    shine = Image.new("L", big, 0)
    ImageDraw.Draw(shine).ellipse((-w * k // 4, -h * k // 2, w * k // 2, h * k // 3), fill=90)
    shine = ImageChops.multiply(shine.filter(ImageFilter.GaussianBlur(60 * k)), shape)
    layer.alpha_composite(Image.merge("RGBA", (*Image.new("RGB", big, (143, 180, 255)).split(), shine)))
    rim = Image.new("L", big, 0)
    ImageDraw.Draw(rim).rounded_rectangle(cover, radius, outline=255, width=3 * k)
    layer.alpha_composite(Image.merge("RGBA", (*vertical_gradient(big, (255, 236, 170), (232, 150, 30)).split(), rim)))
    inner = Image.new("L", big, 0)
    ImageDraw.Draw(inner).rounded_rectangle(scaled(BOOK_COVER, 5, k), radius - 5 * k, outline=110, width=k)
    layer.alpha_composite(Image.merge("RGBA", (*Image.new("RGB", big, (157, 184, 255)).split(), inner)))
    result = layer.resize((w, h), Image.LANCZOS)

    # The pages: the parchment untouched, the stack's edges (anything not warm parchment) mapped to ivory.
    pages = scaled(BOOK_PAGES)
    sheet = scaled(BOOK_SHEET)
    ivory = gradient_lut(IVORY)
    source = image.load()
    page = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    pixels = page.load()
    for y in range(pages[1], pages[3]):
        for x in range(pages[0], pages[2]):
            r, g, b, a = source[x, y]
            inside = sheet[0] <= x < sheet[2] and sheet[1] <= y < sheet[3]
            if inside or (r - b >= 10 and r >= g - 2 and r + g + b > 450):
                pixels[x, y] = (r, g, b, a)
            else:
                pixels[x, y] = (*ivory[(r * 299 + g * 587 + b * 114) // 1000], a)
    result.alpha_composite(page)
    # A soft shadow where the pages sit in the cover.
    edge = Image.new("L", (w, h), 0)
    ImageDraw.Draw(edge).rectangle(pages, outline=120, width=1)
    result.alpha_composite(Image.merge("RGBA", (*Image.new("RGB", (w, h), (10, 16, 48)).split(), edge)))

    # Gold corner guards, an L along each corner of the cover (drawn for the top left, mirrored for the others, then
    # lit from the top like the rest of the theme's gold).
    gw, gh = 82 * k, 80 * k
    shape = Image.new("L", (gw, gh), 0)
    draw = ImageDraw.Draw(shape)
    draw.rounded_rectangle((0, 0, gw, 16 * k), 6 * k, fill=255)
    draw.rounded_rectangle((0, 0, 28 * k, gh), 6 * k, fill=255)
    draw.rectangle((0, 0, 28 * k, 16 * k), fill=255)
    corner = Image.new("L", (gw, gh), 0)
    ImageDraw.Draw(corner).rounded_rectangle((0, 0, gw * 2, gh * 2), radius, fill=255)
    shape = ImageChops.multiply(shape, corner)
    rivet = Image.new("L", (gw, gh), 0)
    ImageDraw.Draw(rivet).ellipse((8 * k, 4 * k, 20 * k, 16 * k), fill=255)
    size = (round(82 * w / 1024), round(80 * h / 768))
    left, top, right, bottom = scaled(BOOK_COVER)
    for flip_x, flip_y in ((False, False), (True, False), (False, True), (True, True)):
        outline, hole = shape, rivet
        if flip_x:
            outline, hole = outline.transpose(Image.FLIP_LEFT_RIGHT), hole.transpose(Image.FLIP_LEFT_RIGHT)
        if flip_y:
            outline, hole = outline.transpose(Image.FLIP_TOP_BOTTOM), hole.transpose(Image.FLIP_TOP_BOTTOM)
        face = outline.filter(ImageFilter.MinFilter(2 * k + 1))
        guard = Image.new("RGBA", (gw, gh), (0, 0, 0, 0))
        guard.alpha_composite(Image.merge("RGBA", (*Image.new("RGB", (gw, gh), (122, 64, 4)).split(), outline)))
        guard.alpha_composite(Image.merge("RGBA", (*vertical_gradient((gw, gh), (255, 222, 128), (226, 140, 24)).split(), face)))
        shine = ImageChops.multiply(face, ImageChops.subtract(face, face.filter(ImageFilter.MinFilter(3 * k + 1))))
        guard.alpha_composite(Image.merge("RGBA", (*Image.new("RGB", (gw, gh), (255, 246, 206)).split(), shine.point(lambda v: v * 3 // 4))))
        guard.alpha_composite(Image.merge("RGBA", (*Image.new("RGB", (gw, gh), (138, 78, 6)).split(), hole)))
        guard.alpha_composite(Image.merge("RGBA", (*Image.new("RGB", (gw, gh), (255, 238, 170)).split(), hole.filter(ImageFilter.MinFilter(2 * k + 1)))))
        guard = guard.resize(size, Image.LANCZOS)
        result.alpha_composite(guard, (right - size[0] if flip_x else left, bottom - size[1] if flip_y else top))
    return result


def photo_corners(image):
    """The plum photo corners holding the Adventurer's Journal's maps, in gold; the paper and maps stay as they are."""
    hsv = image.convert("RGB").convert("HSV")
    hue, saturation, value = hsv.split()
    low, high = round(PLUM_HUE[0] * 255), round(PLUM_HUE[1] * 255)
    plum = ImageChops.multiply(hue.point(lambda v: 255 if low <= v <= high else 0),
                               saturation.point(lambda v: 255 if v >= PLUM_SATURATION * 255 else 0))
    plum = ImageChops.multiply(plum, value.point(lambda v: 255 if v <= PLUM_VALUE * 255 else 0))
    plum = plum.filter(ImageFilter.MaxFilter(3)).filter(ImageFilter.GaussianBlur(0.8))
    gray = image.convert("L")
    box = plum.point(lambda v: 255 if v > 128 else 0).getbbox()
    if box is None:
        return image
    # Stretch the corners' own brightness range over the gold ramp.
    values = [v for v, m in zip(gray.getdata(), plum.getdata()) if m > 128]
    lo, hi = min(values), max(values)
    ramp = gradient_lut(GOLD)
    lut = [ramp[max(0, min(255, round((v - lo) * 255 / max(1, hi - lo))))] for v in range(256)]
    golden = Image.merge("RGB", [gray.point([lut[v][c] for v in range(256)]) for c in range(3)]).convert("RGBA")
    golden.putalpha(image.getchannel("A"))
    result = image.copy()
    result.paste(golden, (0, 0), plum)
    result.putalpha(image.getchannel("A"))
    return result


RECIPES = {"navy": navy, "glass": glass, "gold": gold, "ring": ring, "book": book, "photo_corners": photo_corners}
