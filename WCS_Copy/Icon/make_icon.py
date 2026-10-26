# SPDX-License-Identifier: GPL-3.0-or-later
#
# Copyright (C) 2026 Tudor Carean
#
# This program is free software: you can redistribute it and/or modify
# it under the terms of the GNU General Public License as published by
# the Free Software Foundation, either version 3 of the License, or
# (at your option) any later version.
#
# This program is distributed in the hope that it will be useful,
# but WITHOUT ANY WARRANTY; without even the implied warranty of
# MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
# GNU General Public License for more details.
#
# You should have received a copy of the GNU General Public License
# along with this program.  If not, see <https://www.gnu.org/licenses/>.

"""Generates copywcs.ico, the one icon of the console and of the window (ADR 0007, Q8).

The drawing: a charcoal rounded square, the family's; inside it a plate-solved frame, with a
thin celestial grid in blue and a few stars, and a short arrow from it to a second, empty
frame: the solve going from one file into the other. The sizes up to 24 px keep the grid and
the arrow only, drawn across the whole square: at 16 px two frames would be two smudges.

Why a script and not a file drawn by hand: every size in the .ico is drawn on its own, not
shrunk from the large one (the same reason as the siblings' Icon/make_icon.py).

Pillow is used only here, at generation time; nothing in the applications touches it. The
.ico is committed, so the script runs only when the drawing changes:

    python Icon/make_icon.py
"""

import math
import os

from PIL import Image, ImageDraw

# The sizes in the .ico. 20 and 40 are the title bar at 125 % and 250 % DPI, where Windows
# would otherwise scale 16 or 32 and blur.
SIZES = [16, 20, 24, 32, 40, 48, 64, 256]

# The drawing is described on a grid of 110 units and scaled to every size.
GRID = 110
SUPERSAMPLE = 8

BG = (26, 27, 32, 255)
FRAME_FILL = (34, 36, 46, 255)
FRAME_EDGE = (160, 165, 180, 255)
EMPTY_FILL = (30, 31, 38, 255)
EMPTY_EDGE = (96, 100, 112, 255)
GRID_LINE = (70, 125, 190, 255)
STAR = (240, 240, 235, 255)
STAR_WARM = (255, 215, 140, 255)
ARROW = (78, 168, 222, 255)

# The solved frame and the empty one, as (left, top, right, bottom) on the grid.
SOLVED = (4, 8, 48, 44)
EMPTY = (62, 66, 106, 102)

# The stars of the solved frame: position on the grid and radius; the warm one last.
STARS = [(13, 17, 2.2), (31, 14, 1.6), (40, 24, 1.9), (20, 33, 1.5), (36, 38, 1.3), (26, 25, 2.8)]


def celestial_grid(d, box, meridians, parallels, width, bulge):
    """Draws a gently curved grid inside a box: meridians bow sideways, parallels bow down."""
    left, top, right, bottom = box
    w, h = right - left, bottom - top
    for k in range(1, meridians + 1):
        x = left + w * k / (meridians + 1)
        side = (k - (meridians + 1) / 2)
        pts = []
        for i in range(17):
            y = top + h * i / 16
            t = (y - (top + h / 2)) / (h / 2)
            pts.append((x + side * bulge * (1 - t * t), y))
        d.line(pts, fill=GRID_LINE, width=width, joint="curve")
    for k in range(1, parallels + 1):
        y = top + h * k / (parallels + 1)
        pts = []
        for i in range(17):
            x = left + w * i / 16
            t = (x - (left + w / 2)) / (w / 2)
            pts.append((x, y + bulge * 0.8 * (1 - t * t)))
        d.line(pts, fill=GRID_LINE, width=width, joint="curve")


def arrow(d, start, end, width, head):
    """A straight arrow with a filled triangular head at the end."""
    (x0, y0), (x1, y1) = start, end
    dx, dy = x1 - x0, y1 - y0
    length = math.hypot(dx, dy)
    ux, uy = dx / length, dy / length
    bx, by = x1 - ux * head, y1 - uy * head
    d.line([(x0, y0), (bx, by)], fill=ARROW, width=width)
    px, py = -uy, ux
    half = head * 0.6
    d.polygon([(x1, y1), (bx + px * half, by + py * half), (bx - px * half, by - py * half)], fill=ARROW)


def scaled(box, s):
    return [v * s for v in box]


def draw(size):
    small = size <= 24
    px = size * SUPERSAMPLE
    s = px / GRID
    img = Image.new("RGBA", (px, px), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    d.rounded_rectangle([0, 0, px - 1, px - 1], radius=22 * s, fill=BG)

    if small:
        # The grid across the square and one bold arrow: the two things that still read.
        celestial_grid(d, scaled((10, 12, 100, 98), s), 2, 2, max(1, round(2.4 * s)), 5 * s)
        arrow(d, (16 * s, 62 * s), (94 * s, 62 * s), round(11 * s), 26 * s)
        return img.resize((size, size), Image.LANCZOS)

    edge = max(1, round(1.6 * s))

    # The empty frame first: the arrow's head lies over its corner.
    d.rounded_rectangle(scaled(EMPTY, s), radius=3 * s, fill=EMPTY_FILL, outline=EMPTY_EDGE, width=edge)

    # The solved frame: its grid clipped to the inside through a layer and a mask.
    d.rounded_rectangle(scaled(SOLVED, s), radius=3 * s, fill=FRAME_FILL, outline=FRAME_EDGE, width=edge)
    layer = Image.new("RGBA", (px, px), (0, 0, 0, 0))
    ld = ImageDraw.Draw(layer)
    celestial_grid(ld, scaled(SOLVED, s), 3, 2, max(1, round(1.1 * s)), 4 * s)
    mask = Image.new("L", (px, px), 0)
    inner = (SOLVED[0] + 1.2, SOLVED[1] + 1.2, SOLVED[2] - 1.2, SOLVED[3] - 1.2)
    ImageDraw.Draw(mask).rounded_rectangle(scaled(inner, s), radius=2 * s, fill=255)
    img.paste(layer, (0, 0), Image.composite(layer.getchannel("A"), mask, mask).point(lambda v: v))
    d = ImageDraw.Draw(img)
    for i, (x, y, r) in enumerate(STARS):
        colour = STAR_WARM if i == len(STARS) - 1 else STAR
        d.ellipse([(x - r) * s, (y - r) * s, (x + r) * s, (y + r) * s], fill=colour)

    arrow(d, (45 * s, 47 * s), (65 * s, 67 * s), round(5 * s), 13 * s)

    return img.resize((size, size), Image.LANCZOS)


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    frames = [draw(n) for n in SIZES]
    out = os.path.join(here, "copywcs.ico")
    # The largest frame is the base image: Pillow drops every size above it.
    frames[-1].save(out, format="ICO", sizes=[(n, n) for n in SIZES], append_images=frames[:-1])
    print("wrote", out)


if __name__ == "__main__":
    main()
