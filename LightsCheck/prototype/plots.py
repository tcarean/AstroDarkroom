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

import sys, csv, numpy as np
import matplotlib; matplotlib.use('Agg')
import matplotlib.pyplot as plt
OUT = sys.argv[1]
rows = list(csv.DictReader(open(OUT + '/frames.csv')))
def col(k, d=np.nan):
    return np.array([float(r[k]) if r[k] not in ('', 'None') else d for r in rows])
idx = col('idx').astype(int); dx = col('dx'); dy = col('dy'); step = col('step'); dmin = col('dmin'); gap = col('gap_s')
fwhm = col('fwhm_med'); el = col('elong_med'); ndet = col('n_det'); bgG = col('G1_median'); bgR = col('R_median'); bgB = col('B_median'); halo = col('halo'); rot = col('rot_deg')
tmin = col('tsec') / 60

# 1. dither map
fig, ax = plt.subplots(figsize=(10, 8))
ax.plot(dx, dy, '-', color='0.7', lw=0.8)
sc = ax.scatter(dx, dy, c=idx, cmap='viridis', s=28, zorder=3)
for i in range(len(idx)):
    ax.annotate(str(idx[i]), (dx[i], dy[i]), fontsize=6, xytext=(2, 2), textcoords='offset points')
ax.set_xlabel('deplasare X fata de cadrul 1 (px, axa DEC)'); ax.set_ylabel('deplasare Y fata de cadrul 1 (px, axa RA)')
ax.set_aspect('equal'); ax.grid(alpha=0.3); ax.invert_yaxis()
plt.colorbar(sc, label='nr. cadru'); ax.set_title('Harta pozitiilor cadrelor (dithering + drift)')
plt.tight_layout(); plt.savefig(OUT + '/dither_map.png', dpi=110); plt.close()

# 2. steps + nearest other frame
fig, axes = plt.subplots(2, 1, figsize=(14, 7), sharex=True)
colors = ['tab:red' if (g is not np.nan and g > 5) else 'tab:gray' for g in gap]
axes[0].bar(idx, np.nan_to_num(step), color=colors)
axes[0].axhline(10, color='g', ls='--', lw=1, label='10 px (tinta)'); axes[0].axhline(3, color='r', ls='--', lw=1, label='3 px (inutil)')
axes[0].set_ylabel('pas fata de cadrul anterior (px)'); axes[0].legend(loc='upper left'); axes[0].set_title('rosu = cadru dupa o pauza (dithering manual), gri = cadru imediat urmator')
axes[1].bar(idx, dmin, color='tab:blue')
axes[1].axhline(10, color='g', ls='--', lw=1); axes[1].axhline(3, color='r', ls='--', lw=1)
axes[1].set_ylabel('distanta la cel mai apropiat alt cadru (px)'); axes[1].set_xlabel('nr. cadru')
plt.tight_layout(); plt.savefig(OUT + '/dither_steps.png', dpi=110); plt.close()

# 3. quality timeline
fig, axes = plt.subplots(5, 1, figsize=(14, 13), sharex=True)
axes[0].plot(idx, fwhm, 'o-', ms=3); axes[0].set_ylabel('FWHM median (px)'); axes[0].grid(alpha=0.3)
axes[1].plot(idx, el, 'o-', ms=3, color='tab:red'); axes[1].axhline(1.3, color='orange', ls='--'); axes[1].axhline(1.5, color='r', ls='--'); axes[1].set_ylabel('alungire mediana'); axes[1].grid(alpha=0.3)
axes[2].plot(idx, ndet, 'o-', ms=3, color='tab:green'); axes[2].set_ylabel('stele detectate'); axes[2].grid(alpha=0.3)
axes[3].plot(idx, bgR, 'o-', ms=3, color='r', label='R'); axes[3].plot(idx, bgG, 'o-', ms=3, color='g', label='G'); axes[3].plot(idx, bgB, 'o-', ms=3, color='b', label='B'); axes[3].set_ylabel('fond cer (ADU peste bias)'); axes[3].legend(); axes[3].grid(alpha=0.3)
axes[4].plot(idx, rot * 60, 'o-', ms=3, color='purple'); axes[4].set_ylabel('rotatie camp fata de cadrul 1 (arcmin)'); axes[4].set_xlabel('nr. cadru'); axes[4].grid(alpha=0.3)
plt.tight_layout(); plt.savefig(OUT + '/quality_timeline.png', dpi=110); plt.close()
print('plots ok')
