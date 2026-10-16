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

"""Generates darkscheck.ico, the one icon of the console and of the window.

The drawing: a charcoal rounded square, the dark frame itself, with a faint pixel grid and
a handful of hot pixels glowing in amber and orange, the thing every check of the tool
measures; one of them is the strongest, the master's "idx" pixel, drawn with a soft halo.

Why a script and not a file drawn by hand: every size in the .ico is drawn on its own, not
shrunk from the large one (the same reason as LightsCheck's and FlatCheck's Icon/make_icon.py).
At 16 px the grid would blur into grey and the small hot pixels would vanish, so the small
sizes drop the grid and draw fewer, larger dots.

Pillow is used only here, at generation time; nothing in the applications touches it. The
.ico is committed, so the script runs only when the drawing changes:

    python Icon/make_icon.py
"""

import os

from PIL import Image, ImageDraw

# The sizes in the .ico. 20 and 40 are the title bar at 125 % and 250 % DPI, where Windows
# would otherwise scale 16 or 32 and blur.
SIZES = [16, 20, 24, 32, 40, 48, 64, 256]

# The drawing is described on a grid of 110 units and scaled to every size.
GRID = 110
SUPERSAMPLE = 8

BG = (26, 27, 32, 255)
GRID_LINE = (44, 46, 54, 255)
HALO = (120, 70, 30, 255)
HOT = (245, 150, 50, 255)
HOT_FAINT = (190, 110, 45, 255)
STRONGEST = (255, 205, 90, 255)

# The hot pixels: position on the grid and radius, the strongest last.
PIXELS = [(26, 34, 4), (74, 24, 3), (82, 70, 5), (36, 80, 3), (60, 52, 7)]
SMALL_PIXELS = [(30, 36, 9), (78, 30, 7), (36, 78, 7), (72, 72, 12)]


def draw(size):
    small = size <= 24
    px = size * SUPERSAMPLE
    s = px / GRID
    img = Image.new("RGBA", (px, px), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    d.rounded_rectangle([0, 0, px - 1, px - 1], radius=22 * s, fill=BG)

    if not small:
        # The pixel grid of the sensor, faint, every 11 units.
        width = max(1, round(1.2 * s))
        for k in range(1, 10):
            d.line([(k * 11 * s, 6 * s), (k * 11 * s, (GRID - 6) * s)], fill=GRID_LINE, width=width)
            d.line([(6 * s, k * 11 * s), ((GRID - 6) * s, k * 11 * s)], fill=GRID_LINE, width=width)

    pixels = SMALL_PIXELS if small else PIXELS
    for i, (x, y, r) in enumerate(pixels):
        last = i == len(pixels) - 1
        if last:
            hr = r * 2.2
            d.ellipse([(x - hr) * s, (y - hr) * s, (x + hr) * s, (y + hr) * s], fill=HALO)
        colour = STRONGEST if last else (HOT if i % 2 == 0 else HOT_FAINT)
        d.ellipse([(x - r) * s, (y - r) * s, (x + r) * s, (y + r) * s], fill=colour)

    return img.resize((size, size), Image.LANCZOS)


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    frames = [draw(n) for n in SIZES]
    out = os.path.join(here, "darkscheck.ico")
    # The largest frame is the base image: Pillow drops every size above it.
    frames[-1].save(out, format="ICO", sizes=[(n, n) for n in SIZES], append_images=frames[:-1])
    print("wrote", out)


if __name__ == "__main__":
    main()
