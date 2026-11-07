"""Regenerates the TT Tools icons: two overlapping T letters on a warm tile.

Run from the repository root: python tools/icons/generate_app_icon.py
Writes src/OverTranslate/icons/app.ico, app-compact.ico and docs/images/icon.svg.
Every size is drawn on its own rather than scaled from 256px, so small sizes stay crisp.
"""
from PIL import Image, ImageChops, ImageDraw

SIZES = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256]
TILE_START, TILE_END = (244, 142, 44), (214, 62, 44)
BACK, FRONT = (255, 204, 128), (255, 246, 232)
CORNER = 22.5  # All geometry is in a 100-unit square.
BAR, STEM, RADIUS, GAP = 14, 14, 3.5, 3.5
BACK_T = (17, 17, 48, 62)  # x, y, width, height
FRONT_T = (35, 29, 48, 54)


def scaled(t, scale):
    """Scales a T about the tile's centre; the compact icon fills more of the tile."""
    x, y, w, h = t
    return (50 + (x - 50) * scale, 50 + (y - 50) * scale, w * scale, h * scale)


def t_mask(n, t, scale, grow=0):
    k = n / 100
    x, y, w, h = scaled(t, scale)
    bar, stem, r, g = BAR * scale, STEM * scale, RADIUS * scale, grow * scale
    mask = Image.new("L", (n, n), 0)
    draw = ImageDraw.Draw(mask)
    sx = x + (w - stem) / 2
    for a, b, c, d in ((x, y, x + w, y + bar), (sx, y, sx + stem, y + h)):
        draw.rounded_rectangle(
            [(a - g) * k, (b - g) * k, (c + g) * k, (d + g) * k], radius=(r + g) * k, fill=255)
    return mask


def tile(n):
    image = Image.new("RGB", (n, n))
    pixels = image.load()
    for y in range(n):
        for x in range(n):
            f = (x + y) / (2 * n - 2)
            pixels[x, y] = tuple(int(TILE_START[i] + (TILE_END[i] - TILE_START[i]) * f) for i in range(3))
    mask = Image.new("L", (n, n), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, n - 1, n - 1], radius=n * CORNER / 100, fill=255)
    image = image.convert("RGBA")
    image.putalpha(mask)
    return image


def paint(image, mask, color):
    clipped = ImageChops.multiply(mask, image.getchannel("A"))
    image.paste(Image.new("RGBA", image.size, color + (255,)), (0, 0), clipped)


def render(size, scale):
    n = size * (8 if size < 64 else 4)
    image = tile(n)
    paint(image, t_mask(n, BACK_T, scale), BACK)
    image.paste(tile(n), (0, 0), t_mask(n, FRONT_T, scale, GAP))
    paint(image, t_mask(n, FRONT_T, scale), FRONT)
    return image.resize((size, size), Image.LANCZOS)


def save_ico(path, scale):
    images = [render(s, scale) for s in SIZES]
    images[-1].save(path, format="ICO", sizes=[(s, s) for s in SIZES], append_images=images[:-1])


def t_rects(t, grow=0):
    x, y, w, h = t
    sx = x + (w - STEM) / 2
    return "".join(
        f'<rect x="{a - grow}" y="{b - grow}" width="{c + 2 * grow}" height="{d + 2 * grow}" rx="{RADIUS + grow}"/>'
        for a, b, c, d in ((x, y, w, BAR), (sx, y, STEM, h)))


def hex_color(c):
    return "#%02X%02X%02X" % c


def save_svg(path):
    svg = f'''<?xml version="1.0" encoding="utf-8"?>
<svg xmlns="http://www.w3.org/2000/svg" width="512" height="512" viewBox="0 0 100 100" role="img" aria-label="TT Tools">
  <defs>
    <linearGradient id="tile" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0" stop-color="{hex_color(TILE_START)}"/>
      <stop offset="1" stop-color="{hex_color(TILE_END)}"/>
    </linearGradient>
    <mask id="gap">
      <rect width="100" height="100" fill="#fff"/>
      <g fill="#000">{t_rects(FRONT_T, GAP)}</g>
    </mask>
  </defs>
  <rect width="100" height="100" rx="{CORNER}" fill="url(#tile)"/>
  <g fill="{hex_color(BACK)}" mask="url(#gap)">{t_rects(BACK_T)}</g>
  <g fill="{hex_color(FRONT)}">{t_rects(FRONT_T)}</g>
</svg>
'''
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(svg)


if __name__ == "__main__":
    save_ico("src/OverTranslate/icons/app.ico", 1.0)
    save_ico("src/OverTranslate/icons/app-compact.ico", 1.14)
    save_svg("docs/images/icon.svg")
