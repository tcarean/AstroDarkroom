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

"""Shared helpers for the darks check (Nikon D3300, RGGB, 12-bit, black 150)."""
import os, sys, time, csv
import numpy as np
import rawpy
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from nefexif import read as exif  # noqa: F401

BLACK, WHITE = 150, 4095
PLANES = {'R': (0, 0), 'G1': (0, 1), 'G2': (1, 0), 'B': (1, 1)}
T0 = time.time()


def log(*a):
    print(f'[{time.time()-T0:6.0f}s]', *a, flush=True)


def load(p):
    with rawpy.imread(p) as r:
        return r.raw_image_visible.copy()


def plane(a, name):
    r0, c0 = PLANES[name]
    return a[r0::2, c0::2]


def mad_sigma(x):
    """Clipped standard deviation (the NEF is quantised in steps of 2-4 ADU, so a MAD is 0 or a step)."""
    x = np.asarray(x)
    if x.size > 3_000_000:
        x = x.reshape(-1)[::max(1, x.size // 3_000_000)]
    x = x.astype(np.float32).reshape(-1)
    med = float(np.median(x))
    s = float(x.std())
    for _ in range(3):
        sel = np.abs(x - med) < 5 * max(s, 1.0)
        s = float(x[sel].std())
        med = float(x[sel].mean())
    return s, float(np.median(x))


def binned(a, b=16):
    out = {}
    for n in PLANES:
        p = plane(a, n).astype(np.float32)
        h, w = (p.shape[0] // b) * b, (p.shape[1] // b) * b
        out[n] = p[:h, :w].reshape(h // b, b, w // b, b).mean(axis=(1, 3)) - BLACK
    return out


def regions(m):
    h, w = m.shape
    c = m[int(.4*h):int(.6*h), int(.4*w):int(.6*w)].mean()
    return dict(center=float(c),
                top_left=float(m[:h//10, :w//10].mean()), top_right=float(m[:h//10, -(w//10):].mean()),
                bottom_left=float(m[-(h//10):, :w//10].mean()), bottom_right=float(m[-(h//10):, -(w//10):].mean()),
                top=float(m[:h//10].mean()), bottom=float(m[-(h//10):].mean()),
                left=float(m[:, :w//10].mean()), right=float(m[:, -(w//10):].mean()))


def write_csv(path, rows):
    if not rows:
        return
    with open(path, 'w', newline='') as f:
        w = csv.DictWriter(f, fieldnames=list(rows[0].keys()))
        w.writeheader()
        w.writerows(rows)
