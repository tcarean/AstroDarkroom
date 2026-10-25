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

"""Generates astrodarkroom.ico, the icon of the launcher.

The drawing: a dark red-brown rounded square, the darkroom's safelight, with one white star
in the middle: a soft halo, a round core and four spikes. The suite's name is the picture:
the darkroom is the red, the astro is the star.

Why a script and not a file drawn by hand: every size in the .ico is drawn on its own, not
shrunk from the large one. At 16 px the spikes of the 256 px drawing would fall under a
pixel and vanish; the small sizes therefore draw a larger core and no spikes. One image
scaled by Windows cannot do that (the same reason as LightsCheck's Icon/make_icon.py, whose
shape this file keeps).

Pillow is used only here, at generation time; nothing in the application touches it. The
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

BG = (74, 22, 20, 255)        # the safelight's red-brown
HALO = (140, 62, 54, 255)     # the star's glow, the red lit up
STAR = (250, 246, 240, 255)   # warm white


def draw(size):
    small = size <= 24
    px = size * SUPERSAMPLE
    s = px / GRID
    img = Image.new("RGBA", (px, px), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    d.rounded_rectangle([0, 0, px - 1, px - 1], radius=22 * s, fill=BG)

    c = GRID / 2

    # The star: a soft halo, then on the larger sizes four spikes, then the core.
    hr = 22 if small else 20
    d.ellipse([(c - hr) * s, (c - hr) * s, (c + hr) * s, (c + hr) * s], fill=HALO)
    if not small:
        width = (5 if size <= 48 else 3) * s
        half = width / 2
        reach = 36
        for x0, y0, x1, y1 in [(c, c - reach, c, c + reach), (c - reach, c, c + reach, c)]:
            d.rounded_rectangle([x0 * s - half, y0 * s - half, x1 * s + half, y1 * s + half],
                                radius=half, fill=STAR)
    cr = 15 if small else 11
    d.ellipse([(c - cr) * s, (c - cr) * s, (c + cr) * s, (c + cr) * s], fill=STAR)

    return img.resize((size, size), Image.LANCZOS)


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    frames = [draw(n) for n in SIZES]
    largest = frames[-1]
    largest.save(os.path.join(here, "astrodarkroom.ico"), format="ICO",
                 sizes=[(n, n) for n in SIZES], append_images=frames[:-1])


if __name__ == "__main__":
    main()
