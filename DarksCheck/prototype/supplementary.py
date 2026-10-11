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

"""Second pass: mean-master maps, per-frame warming curves (hot-pixel amplitude relative to the master),
edge-vs-centre per frame, and the dark hot pixels the lights never flag.
Usage: python -I supplementary.py <report_dir> <scratch_dir>"""
import sys, os, glob, json, csv
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from analyze_darks_lib import load, plane, binned, regions, mad_sigma, PLANES, BLACK, WHITE, exif, write_csv, log

OUT, SCR = sys.argv[1], sys.argv[2]
SESSION = r'E:\Photografie\Astro\Milkyway-Core\2026-08-12-Smida'
LIB = r'E:\Photografie\Astro\Flats and Darks\Darks\2026-10-10-ISO1600-120s-15.8celsius\darks'
R = json.load(open(os.path.join(OUT, 'results.json')))
S = {}


def series(files, hot_level, label):
    """Load a series, return (median master, mean master, per-frame rows with amplitude ratio and edge-centre)."""
    stack = None
    rows = []
    for i, p in enumerate(files):
        a = load(p)
        if stack is None:
            stack = np.empty((len(files),) + a.shape, np.uint16)
        stack[i] = a
        log(f'  {label} loaded', os.path.basename(p))
    H, W = stack.shape[1:]
    master = np.empty((H, W), np.float32)
    for r0 in range(0, H, 256):
        master[r0:r0+256] = np.median(stack[:, r0:r0+256], axis=0)
    mean = stack.mean(axis=0, dtype=np.float64).astype(np.float32)
    excess = master - BLACK
    sel = (excess > hot_level) & (master < 3000)
    idx = np.flatnonzero(sel)
    ex = excess.reshape(-1)[idx]
    flat = stack.reshape(len(files), -1)
    for i, p in enumerate(files):
        e = exif(p)
        fe = flat[i, idx].astype(np.float32) - BLACK
        ratio = fe / ex
        b = binned(stack[i])['G1']
        rg = regions(b)
        rows.append(dict(file=os.path.basename(p), time=e.get('DateTimeOriginal'),
                         amp_ratio_median=float(np.median(ratio)), amp_ratio_q25=float(np.percentile(ratio, 25)),
                         amp_ratio_q75=float(np.percentile(ratio, 75)),
                         mean_above_black=float(stack[i].mean(dtype=np.float64)) - BLACK,
                         left_minus_center=rg['left'] - rg['center'], right_minus_center=rg['right'] - rg['center'],
                         top_minus_center=rg['top'] - rg['center'], bottom_minus_center=rg['bottom'] - rg['center'],
                         topleft_minus_center=rg['top_left'] - rg['center']))
    del stack, flat
    return master, mean, rows, int(idx.size)


# Smida darks
dark_files = sorted(glob.glob(os.path.join(SESSION, 'darks', '*.NEF')))
master, mean, rows, nsel = series(dark_files, 20, 'dark')
write_csv(os.path.join(OUT, 'darks_warming_per_frame.csv'), rows)
np.save(os.path.join(SCR, 'smida_dark_master_median.npy'), master)
np.save(os.path.join(SCR, 'smida_dark_master_mean.npy'), mean)
bm = binned(mean)
np.save(os.path.join(OUT, 'dark_mean_binned_G1.npy'), bm['G1'])
bb = np.load(os.path.join(OUT, 'bias_master_binned_G1.npy'))
dc = bm['G1'] - bb
np.save(os.path.join(OUT, 'dark_current_mean_binned_G1.npy'), dc)
S['dark_mean_regions'] = {n: regions(bm[n]) for n in PLANES}
S['dark_current_mean_regions_G1'] = regions(dc)
S['dark_current_mean_G1_median'] = float(np.median(dc))
S['dark_current_mean_G1_p1_p99'] = [float(np.percentile(dc, 1)), float(np.percentile(dc, 99))]
S['smida_amp_pixels'] = nsel

# dark hot pixels (> 50) that the lights never flag: are they isolated?
hot_count = np.load(os.path.join(OUT, 'light_hot_count.npy'))
excess = master - BLACK
strong = excess > 50
never = strong & (hot_count == 0)
ys, xs = np.nonzero(never)
H, W = excess.shape
nb_max = []
for y, x in zip(ys, xs):
    y0, y1, x0, x1 = max(y-1, 0), min(y+2, H), max(x-1, 0), min(x+2, W)
    win = excess[y0:y1, x0:x1].copy()
    win[y-y0, x-x0] = -999
    nb_max.append(float(win.max()))
nb_max = np.array(nb_max)
S['strong_never_flagged'] = dict(n=int(never.sum()), neighbour_excess_max_percentiles={str(q): float(np.percentile(nb_max, q)) for q in (10, 50, 90)} if nb_max.size else None,
                                 n_with_hot_neighbour=int((nb_max > 10).sum()), own_excess_median=float(np.median(excess[never])) if never.any() else None,
                                 positions_sample=[(int(y), int(x)) for y, x in list(zip(ys, xs))[:8]])
# how do the strong ones that ARE flagged compare
flagged = strong & (hot_count >= 20)
S['strong_flagged'] = dict(n=int(flagged.sum()), own_excess_median=float(np.median(excess[flagged])))
del master, mean, excess

# library darks
lib_files = sorted(glob.glob(os.path.join(LIB, '*.NEF')))
lmaster, lmean, lrows, lnsel = series(lib_files, 100, 'lib')
write_csv(os.path.join(OUT, 'library_warming_per_frame.csv'), lrows)
np.save(os.path.join(SCR, 'library_dark_master_mean.npy'), lmean)
S['library_amp_pixels'] = lnsel
lex = lmean - BLACK
# edge structure of the library mean master, all planes
lb = binned(lmean)
S['library_mean_regions'] = {n: regions(lb[n]) for n in PLANES}
# amplitude ratio library/smida at ISO 1600 vs 400, using the saved Smida median master
sm = np.load(os.path.join(SCR, 'smida_dark_master_median.npy')) - BLACK
sel = (sm > 50) & (sm < 2000) & (lex < 3500)
ratio = lex[sel] / sm[sel]
S['lib_over_smida'] = dict(n=int(sel.sum()), median=float(np.median(ratio)), q25=float(np.percentile(ratio, 25)), q75=float(np.percentile(ratio, 75)))
sel2 = (sm > 20) & (sm <= 50) & (lex < 3500)
S['lib_over_smida_weak'] = dict(n=int(sel2.sum()), median=float(np.median(lex[sel2] / sm[sel2])))

with open(os.path.join(OUT, 'results_supplementary.json'), 'w') as f:
    json.dump(S, f, indent=1)
print(json.dumps(S, indent=1))
log('done')
