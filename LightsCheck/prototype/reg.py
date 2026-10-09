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

import sys, os, glob, json, math, csv
import numpy as np
from datetime import datetime, timedelta
from scipy.spatial import cKDTree

OUT = sys.argv[1]
W, H = 6016, 4016
PXSCALE = 206.265 * 3.89 / 135.0  # arcsec/px (D3300 3.89 um, 135 mm)

frames = []
for p in sorted(glob.glob(os.path.join(OUT, '*.json'))):
    with open(p) as fh: frames.append(json.load(fh))
frames.sort(key=lambda r: r['idx'])
N = len(frames)

def ptime(r):
    t = datetime.strptime(r['datetime'], '%Y:%m:%d %H:%M:%S')
    return t + timedelta(seconds=int(r['subsec']) / 100.0)

def fit_similarity(src, dst):
    # dst = s*R*src + t ; linear in a,b,tx,ty
    n = len(src)
    A = np.zeros((2 * n, 4)); bvec = np.zeros(2 * n)
    A[0::2, 0] = src[:, 0]; A[0::2, 1] = -src[:, 1]; A[0::2, 2] = 1; bvec[0::2] = dst[:, 0]
    A[1::2, 0] = src[:, 1]; A[1::2, 1] = src[:, 0]; A[1::2, 3] = 1; bvec[1::2] = dst[:, 1]
    sol, *_ = np.linalg.lstsq(A, bvec, rcond=None)
    a, b, tx, ty = sol
    pred = np.c_[a * src[:, 0] - b * src[:, 1] + tx, b * src[:, 0] + a * src[:, 1] + ty]
    return sol, np.hypot(*(pred - dst).T)

def register(ref, pts):
    ref = np.array(ref)[:300, :2]; pts = np.array(pts)[:300, :2]
    off = (pts[:, None, :] - ref[None, :, :]).reshape(-1, 2)
    hist, xe, ye = np.histogram2d(off[:, 0], off[:, 1], bins=500, range=[[-2000, 2000], [-2000, 2000]])
    i, j = np.unravel_index(np.argmax(hist), hist.shape)
    dx, dy = (xe[i] + xe[i + 1]) / 2, (ye[j] + ye[j + 1]) / 2
    votes = int(hist[i, j])
    sol = np.array([1, 0, dx, dy]); tol = 8.0
    allref = np.array(frames_ref_full)[:, :2]; allpts = np.array(pts_full)[:, :2]
    for it in range(4):
        a, b, tx, ty = sol
        pred = np.c_[a * allref[:, 0] - b * allref[:, 1] + tx, b * allref[:, 0] + a * allref[:, 1] + ty]
        tree = cKDTree(allpts)
        dist, idx = tree.query(pred, distance_upper_bound=tol)
        ok = np.isfinite(dist)
        if ok.sum() < 10: return None
        sol, resid = fit_similarity(allref[ok], allpts[idx[ok]])
        tol = 3.0
    a, b, tx, ty = sol
    theta = math.degrees(math.atan2(b, a)); scale = math.hypot(a, b)
    cx, cy = W / 2, H / 2
    ncx, ncy = a * cx - b * cy + tx, b * cx + a * cy + ty
    return dict(n_match=int(ok.sum()), votes=votes, dx=ncx - cx, dy=ncy - cy, rot_deg=theta, scale=scale, rms=float(np.sqrt(np.mean(resid ** 2))))

# reference: frame 1
ref = frames[0]
frames_ref_full = ref['reg_xy']
rows = []
t0 = ptime(ref)
for r in frames:
    pts_full = r['reg_xy']
    g = register(ref['reg_xy'], r['reg_xy'])
    t = ptime(r)
    row = dict(idx=r['idx'] + 1, file=r['file'], time=t.strftime('%H:%M:%S'), tsec=(t - t0).total_seconds(), exposure=r['exposure'], iso=r['iso'])
    for k in ('n_det', 'n_det_bright', 'fwhm_med', 'fwhm_p90', 'elong_med', 'elong_p90', 'frac_elong13', 'pa_mean', 'pa_coherence', 'halo', 'bg_median', 'bg_center', 'sigma_green', 'R_median', 'G1_median', 'B_median', 'R_sat', 'G1_sat', 'G2_sat', 'B_sat', 'fwhm_center', 'fwhm_mid', 'fwhm_edge', 'elong_center', 'elong_edge'):
        row[k] = r.get(k)
    row['bg_corner_mean'] = float(np.mean(r['bg_corners'])); row['bg_corner_spread'] = float(np.max(r['bg_corners']) - np.min(r['bg_corners']))
    row['n_trails'] = len(r['trails']); row['trail_maxlen'] = max([t['len'] for t in r['trails']], default=0)
    row.update(g if g else dict(n_match=0, votes=0, dx=np.nan, dy=np.nan, rot_deg=np.nan, scale=np.nan, rms=np.nan))
    rows.append(row)

# timing and dither
for i, row in enumerate(rows):
    if i == 0:
        row['gap_s'] = None; row['step'] = None; row['step_dx'] = None; row['step_dy'] = None
    else:
        prev = rows[i - 1]
        row['gap_s'] = row['tsec'] - prev['tsec'] - prev['exposure']
        row['step_dx'] = row['dx'] - prev['dx']; row['step_dy'] = row['dy'] - prev['dy']
        row['step'] = math.hypot(row['step_dx'], row['step_dy'])
pos = np.array([[r['dx'], r['dy']] for r in rows])
D = np.hypot(*(pos[:, None, :] - pos[None, :, :]).T)
np.fill_diagonal(D, np.inf)
for i, row in enumerate(rows):
    row['dmin'] = float(np.nanmin(D[i])); row['dmin_frame'] = int(np.nanargmin(D[i]) + 1)
    row['n_within3'] = int((D[i] < 3).sum()); row['n_within10'] = int((D[i] < 10).sum())

with open(os.path.join(OUT, 'frames.csv'), 'w', newline='') as fh:
    w = csv.DictWriter(fh, fieldnames=list(rows[0].keys())); w.writeheader()
    for row in rows: w.writerow(row)

np.set_printoptions(linewidth=200)
print(f'N={N}  pixel scale {PXSCALE:.2f} arcsec/px')
print('frame  time      gap   n_det  fwhm  el    pa   coh   halo   bgG   bgR  Gsat   dx       dy      rot(deg)  rms  nmatch  step   dmin(frame)  trails')
for r in rows:
    print(f"{r['idx']:3d} {r['time']} {('%5.0f' % r['gap_s']) if r['gap_s'] is not None else '    -'} {r['n_det']:6d} {r['fwhm_med']:5.2f} {r['elong_med']:4.2f} {('%5.0f' % r['pa_mean']) if r['pa_mean'] is not None else '    -'} {('%4.2f' % r['pa_coherence']) if r['pa_coherence'] is not None else '   -'} {r['halo']:5.3f} {r['G1_median']:5.0f} {r['R_median']:5.0f} {r['G1_sat']:5d} {r['dx']:8.1f} {r['dy']:8.1f} {r['rot_deg']:8.4f} {r['rms']:5.2f} {r['n_match']:5d} {('%6.1f' % r['step']) if r['step'] is not None else '     -'} {r['dmin']:6.1f}({r['dmin_frame']:2d}) {r['n_trails']}{('(%.0f)' % r['trail_maxlen']) if r['n_trails'] else ''}")
