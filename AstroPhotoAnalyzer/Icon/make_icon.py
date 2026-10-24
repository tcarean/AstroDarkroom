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

"""Generates AstroPhotoAnalyzer.ico - the icon shared by the console and the window.

The drawing: a dark sky with a large star at the top left and three histogram bars that
climb, at the bottom right - the image and its measurement, the two halves of the tool.

Why a script and not a hand-drawn file: each size in the .ico is drawn separately, not
shrunk from the large one. At 16 px a star with four thin points becomes a dot, and three
bars one pixel apart melt into one; the small sizes therefore have thicker shapes and fewer
background stars.

Pillow is used only here, for the generation - it is not a dependency of the tool. The
resulting .ico is put in the repo, so the script is run only when the drawing changes:

    python Icon/make_icon.py
"""

import os

from PIL import Image, ImageDraw

# The sizes in the .ico. 20 and 40 are for the title bar at 125% and 250% DPI.
SIZES = [16, 20, 24, 32, 40, 48, 64, 256]

# The drawing is described on a grid of 110 units and scaled to each size.
GRID = 110
SUPERSAMPLE = 8

BG = (15, 22, 38, 255)
STAR = (238, 242, 250, 255)
BAR = (245, 185, 66, 255)
DUST = (120, 134, 170, 255)

# The small background stars, only on the large sizes: (x, y, radius).
BACKGROUND_STARS = [(70, 18, 2.2), (88, 34, 1.6), (26, 52, 1.8), (58, 44, 1.3), (18, 84, 1.5)]


def star_polygon(cx, cy, outer, inner):
    """A polygon with four points, on the axes: the top point, then clockwise."""
    return [(cx, cy - outer), (cx + inner, cy - inner), (cx + outer, cy),
            (cx + inner, cy + inner), (cx, cy + outer), (cx - inner, cy + inner),
            (cx - outer, cy), (cx - inner, cy - inner)]


def draw(size):
    small = size <= 24
    px = size * SUPERSAMPLE
    s = px / GRID
    img = Image.new("RGBA", (px, px), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    d.rounded_rectangle([0, 0, px - 1, px - 1], radius=22 * s, fill=BG)

    if not small:
        for x, y, r in BACKGROUND_STARS:
            d.ellipse([(x - r) * s, (y - r) * s, (x + r) * s, (y + r) * s], fill=DUST)

    # The main star. On the small sizes it is fatter, so that the points are not lost.
    cx, cy = 38, 38
    outer, inner = (26, 7) if small else (24, 5)
    d.polygon([(x * s, y * s) for x, y in star_polygon(cx, cy, outer, inner)], fill=STAR)

    # The histogram: three climbing bars, anchored at the bottom right.
    heights = (18, 30, 44)
    width = 12 if small else 11
    gap = 4 if small else 6
    right, bottom = 96, 96
    x1 = right
    for h in reversed(heights):
        x0 = x1 - width
        d.rounded_rectangle([x0 * s, (bottom - h) * s, x1 * s, bottom * s],
                            radius=2 * s, fill=BAR)
        x1 = x0 - gap

    return img.resize((size, size), Image.LANCZOS)


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    frames = [draw(n) for n in SIZES]
    largest = frames[-1]
    largest.save(os.path.join(here, "AstroPhotoAnalyzer.ico"), format="ICO",
                 sizes=[(n, n) for n in SIZES], append_images=frames[:-1])


if __name__ == "__main__":
    main()
