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

"""Genereaza AstroPhotoAnalyzer.ico - icoana comuna a consolei si a ferestrei.

Desenul: un cer intunecat cu o stea mare in stanga-sus si trei bare de histograma care
urca, in dreapta-jos - imaginea si masuratoarea ei, cele doua jumatati ale uneltei.

De ce un script si nu un fisier desenat de mana: fiecare dimensiune din .ico e desenata
separat, nu micsorata din cea mare. La 16 px o stea cu patru varfuri subtiri devine un
punct, si trei bare separate de un pixel se topesc intr-una; dimensiunile mici au deci
forme mai groase si mai putine stele de fundal.

Pillow e folosit doar aici, la generare - nu e o dependinta a uneltei. .ico-ul rezultat
e pus in repo, deci scriptul se ruleaza doar cand se schimba desenul:

    python Icon/make_icon.py
"""

import os

from PIL import Image, ImageDraw

# Dimensiunile din .ico. 20 si 40 sunt pentru bara de titlu la 125% si 250% DPI.
SIZES = [16, 20, 24, 32, 40, 48, 64, 256]

# Desenul e descris pe o grila de 110 unitati si scalat la fiecare dimensiune.
GRID = 110
SUPERSAMPLE = 8

BG = (15, 22, 38, 255)
STAR = (238, 242, 250, 255)
BAR = (245, 185, 66, 255)
DUST = (120, 134, 170, 255)

# Stelele mici de fundal, doar pe dimensiunile mari: (x, y, raza).
BACKGROUND_STARS = [(70, 18, 2.2), (88, 34, 1.6), (26, 52, 1.8), (58, 44, 1.3), (18, 84, 1.5)]


def star_polygon(cx, cy, outer, inner):
    """Un poligon cu patru varfuri, pe axe: varful de sus, apoi in sensul acelor."""
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

    # Steaua principala. Pe dimensiunile mici e mai grasa, ca varfurile sa nu se piarda.
    cx, cy = 38, 38
    outer, inner = (26, 7) if small else (24, 5)
    d.polygon([(x * s, y * s) for x, y in star_polygon(cx, cy, outer, inner)], fill=STAR)

    # Histograma: trei bare care urca, ancorate jos-dreapta.
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
