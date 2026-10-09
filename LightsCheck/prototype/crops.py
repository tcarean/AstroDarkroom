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

import sys, json, os, numpy as np, rawpy
import matplotlib; matplotlib.use('Agg')
import matplotlib.pyplot as plt
from scipy import ndimage
L = sys.argv[1]; OUT = sys.argv[2]; frames = [int(x) for x in sys.argv[3].split(',')]
BLACK = 150.0

def green(p):
    r = rawpy.imread(p); a = r.raw_image_visible.astype(np.float32) - BLACK; pat = r.raw_pattern; r.close()
    mask = np.zeros(a.shape, bool)
    for i in range(2):
        for j in range(2):
            if pat[i, j] in (1, 3): mask[i::2, j::2] = True
    k = np.array([[0, 1, 0], [1, 0, 1], [0, 1, 0]], np.float32)
    num = ndimage.convolve(np.where(mask, a, 0), k, mode='nearest'); den = ndimage.convolve(mask.astype(np.float32), k, mode='nearest')
    return np.where(mask, a, num / np.maximum(den, 1))

fig, axes = plt.subplots(len(frames), 6, figsize=(18, 3 * len(frames)))
for row, fi in enumerate(frames):
    p = f'{L}/DSC_{fi:04d}.NEF'
    g = green(p)
    with open(f'{OUT}/DSC_{fi:04d}.NEF.json') as fh: js = json.load(fh)
    stars = sorted(js['stars'], key=lambda s: -s['flux'])
    H, W = g.shape
    # pick: 3 bright stars near center, 1 top-left corner, 1 top-right, 1 bottom-right region
    def pick(cond):
        c = [s for s in stars if cond(s)]
        return c[0] if c else None
    sel = [pick(lambda s: abs(s['y'] - H / 2) < 800 and abs(s['x'] - W / 2) < 1200),
           pick(lambda s: abs(s['y'] - H / 2) < 800 and abs(s['x'] - W / 2) < 1200 and 400 < s['peak'] < 2000),
           pick(lambda s: s['y'] < 700 and s['x'] < 1000),
           pick(lambda s: s['y'] < 700 and s['x'] > W - 1000),
           pick(lambda s: s['y'] > H - 700 and s['x'] < 1000),
           pick(lambda s: s['y'] > H - 700 and s['x'] > W - 1000)]
    for col, s in enumerate(sel):
        ax = axes[row, col]; ax.set_xticks([]); ax.set_yticks([])
        if s is None: continue
        y, x = int(round(s['y'])), int(round(s['x'])); r = 12
        cut = g[max(y - r, 0):y + r + 1, max(x - r, 0):x + r + 1]
        ax.imshow(cut, cmap='gray', vmin=np.percentile(cut, 5), vmax=cut.max(), interpolation='nearest')
        ax.set_title(f"#{fi} ({x},{y}) fwhm {s['fwhm']:.1f} el {s['elong']:.2f} pa {s['pa']:.0f}", fontsize=8)
plt.tight_layout(); plt.savefig(f'{OUT}/crops.png', dpi=80)
print('ok')
