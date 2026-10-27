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


RECIPES = {"navy": navy, "glass": glass, "gold": gold, "ring": ring}
