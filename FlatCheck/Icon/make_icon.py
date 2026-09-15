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

"""Genereaza flatcheck.ico - icoana comuna a consolei si a ferestrei.

Desenul: inele concentrice care se intuneca spre margini (vignetarea unui flat corect,
pur radiala) si un reticul pe centrul senzorului - ideea pe care sta toata unealta.

De ce un script si nu un fisier desenat de mana: fiecare dimensiune din .ico e desenata
separat, nu micsorata din cea mare. La 16 px reticulul de la 256 px ar iesi sub un pixel
si ar disparea, iar cinci inele s-ar topi intr-o pata; dimensiunile mici au deci mai
putine inele si linii mai groase. O singura imagine micsorata de Windows nu poate face asta.

Pillow e folosit doar aici, la generare - nu e o dependinta a uneltei, nimic din
aplicatii nu il atinge. .ico-ul rezultat e pus in repo, deci scriptul se ruleaza doar
cand se schimba desenul:

    python Icon/make_icon.py
"""

import os

from PIL import Image, ImageDraw

# Dimensiunile din .ico. 20 si 40 sunt pentru bara de titlu la 125% si 250% DPI, unde
# altfel Windows ar scala 16 sau 32 si ar iesi neclar.
SIZES = [16, 20, 24, 32, 40, 48, 64, 256]

# Desenul e descris pe o grila de 110 unitati si scalat la fiecare dimensiune.
GRID = 110
SUPERSAMPLE = 8

BG = (15, 22, 38, 255)
RINGS_FULL = [(46, (42, 53, 82)), (36, (70, 85, 122)), (26, (116, 134, 173)),
              (16, (180, 194, 222)), (7, (238, 242, 250))]
RINGS_SMALL = [(46, (42, 53, 82)), (30, (96, 112, 150)), (15, (238, 242, 250))]
TICK = (245, 185, 66, 255)


def draw(size):
    small = size <= 24
    px = size * SUPERSAMPLE
    s = px / GRID
    img = Image.new("RGBA", (px, px), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    d.rounded_rectangle([0, 0, px - 1, px - 1], radius=22 * s, fill=BG)

    c = GRID / 2
    for r, rgb in (RINGS_SMALL if small else RINGS_FULL):
        d.ellipse([(c - r) * s, (c - r) * s, (c + r) * s, (c + r) * s], fill=rgb + (255,))

    # Reticulul: patru segmente pe axe, din afara inelului central spre margine. Grosimea
    # creste pe masura ce icoana scade, ca linia sa nu iasa sub un pixel.
    width = (9 if small else 5 if size <= 48 else 3) * s
    inner, outer = (28, 8) if not small else (30, 6)
    half = width / 2
    for x0, y0, x1, y1 in [(c, outer, c, inner), (c, GRID - inner, c, GRID - outer),
                           (outer, c, inner, c), (GRID - inner, c, GRID - outer, c)]:
        d.rounded_rectangle([x0 * s - half, y0 * s - half, x1 * s + half, y1 * s + half],
                            radius=half, fill=TICK)

    return img.resize((size, size), Image.LANCZOS)


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    frames = [draw(n) for n in SIZES]
    largest = frames[-1]
    largest.save(os.path.join(here, "flatcheck.ico"), format="ICO",
                 sizes=[(n, n) for n in SIZES], append_images=frames[:-1])


if __name__ == "__main__":
    main()
