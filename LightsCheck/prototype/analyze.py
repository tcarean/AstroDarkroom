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

import sys, os, glob, json, time, math
import numpy as np, rawpy
from scipy import ndimage
from PIL import Image
from multiprocessing import Pool

LIGHTS = sys.argv[1]; OUT = sys.argv[2]
BLACK = 150.0; WHITE = 4095.0
NSTAR = 400

def exif(p):
    ex = Image.open(p).getexif(); ifd = ex.get_ifd(0x8769)
    return dict(datetime=ifd.get(0x9003), subsec=ifd.get(0x9291, '0'), exposure=float(ifd.get(0x829A)),
                iso=int(ifd.get(0x8827)), fnumber=float(ifd.get(0x829D)), focal=float(ifd.get(0x920A)))

def load(p):
    r = rawpy.imread(p); a = r.raw_image_visible.astype(np.float32); pat = r.raw_pattern.copy(); r.close()
    return a - BLACK, pat

def cfa_stats(a, pat):
    out = {}
    names = {0: 'R', 1: 'G1', 2: 'B', 3: 'G2'}
    for i in range(2):
        for j in range(2):
            sub = a[i::2, j::2]
            nm = names[int(pat[i, j])]
            out[nm + '_median'] = float(np.median(sub[::4, ::4]))
            out[nm + '_sat'] = int((sub >= WHITE - BLACK - 1).sum())
    return out

def green_full(a, pat):
    mask = np.zeros(a.shape, bool)
    for i in range(2):
        for j in range(2):
            if pat[i, j] in (1, 3): mask[i::2, j::2] = True
    k = np.array([[0, 1, 0], [1, 0, 1], [0, 1, 0]], np.float32)
    num = ndimage.convolve(np.where(mask, a, 0).astype(np.float32), k, mode='nearest')
    den = ndimage.convolve(mask.astype(np.float32), k, mode='nearest')
    return np.where(mask, a, num / np.maximum(den, 1)).astype(np.float32)

def background(g, tile=128):
    H, W = g.shape
    h, w = H // tile, W // tile
    t = g[:h * tile, :w * tile].reshape(h, tile, w, tile)[:, ::4, :, ::4]
    med = np.median(t, axis=(1, 3))
    bg = ndimage.zoom(med, (H / h, W / w), order=1)
    return bg[:H, :W], med

def mad_sigma(x):
    m = np.median(x); return 1.4826 * np.median(np.abs(x - m))

def moments(d, cy, cx, rad=6):
    H, W = d.shape
    y0, y1 = int(cy) - rad - 2, int(cy) + rad + 3; x0, x1 = int(cx) - rad - 2, int(cx) + rad + 3
    if y0 < 0 or x0 < 0 or y1 > H or x1 > W: return None
    cut = np.clip(d[y0:y1, x0:x1], 0, None)
    yy, xx = np.mgrid[y0:y1, x0:x1]
    for _ in range(3):
        w = cut * (((yy - cy) ** 2 + (xx - cx) ** 2) <= rad * rad)
        s = w.sum()
        if s <= 0: return None
        cy, cx = (w * yy).sum() / s, (w * xx).sum() / s
    mxx = (w * (xx - cx) ** 2).sum() / s; myy = (w * (yy - cy) ** 2).sum() / s; mxy = (w * (xx - cx) * (yy - cy)).sum() / s
    tr = mxx + myy; det = mxx * myy - mxy * mxy
    disc = max(tr * tr / 4 - det, 0)
    l1, l2 = tr / 2 + math.sqrt(disc), tr / 2 - math.sqrt(disc)
    if l2 <= 0: return None
    fwhm = 2.3548 * math.sqrt(tr / 2)
    elong = math.sqrt(l1 / l2)
    pa = 0.5 * math.degrees(math.atan2(2 * mxy, mxx - myy))
    return dict(y=float(cy), x=float(cx), flux=float(s), fwhm=fwhm, elong=elong, pa=pa,
                fwhm_a=2.3548 * math.sqrt(l1), fwhm_b=2.3548 * math.sqrt(l2))

def halo(d, cy, cx):
    H, W = d.shape; r = 11
    y0, y1, x0, x1 = int(cy) - r, int(cy) + r + 1, int(cx) - r, int(cx) + r + 1
    if y0 < 0 or x0 < 0 or y1 > H or x1 > W: return None
    cut = d[y0:y1, x0:x1]; yy, xx = np.mgrid[y0:y1, x0:x1]
    rr = np.sqrt((yy - cy) ** 2 + (xx - cx) ** 2)
    core = cut[rr <= 3].sum(); ann = cut[(rr > 5) & (rr <= 10)].sum()
    return float(ann / core) if core > 0 else None

def analyze(args):
    idx, p = args
    t0 = time.time()
    a, pat = load(p)
    res = dict(idx=idx, file=os.path.basename(p)); res.update(exif(p)); res.update(cfa_stats(a, pat))
    g = green_full(a, pat); del a
    bg, med = background(g)
    H, W = g.shape
    res['bg_median'] = float(np.median(med)); res['bg_min'] = float(med.min()); res['bg_max'] = float(med.max())
    hm, wm = med.shape
    res['bg_center'] = float(med[hm // 2 - 2:hm // 2 + 2, wm // 2 - 2:wm // 2 + 2].mean())
    res['bg_corners'] = [float(med[:3, :3].mean()), float(med[:3, -3:].mean()), float(med[-3:, :3].mean()), float(med[-3:, -3:].mean())]
    d = g - bg; del g, bg
    c = d[H // 2 - 1000:H // 2 + 1000:3, W // 2 - 1000:W // 2 + 1000:3]
    sigma = mad_sigma(c); res['sigma_green'] = float(sigma)
    sm = ndimage.gaussian_filter(d, 1.0)
    ssig = mad_sigma(sm[H // 2 - 1000:H // 2 + 1000:3, W // 2 - 1000:W // 2 + 1000:3])
    lab, n = ndimage.label(sm > 5 * ssig)
    del sm
    idxs = np.arange(1, n + 1)
    area = ndimage.sum_labels(np.ones(lab.shape, dtype=np.uint8), lab, idxs)
    peak = ndimage.maximum(d, lab, idxs)
    flux = ndimage.sum_labels(d, lab, idxs)
    objs = ndimage.find_objects(lab)
    trails = []
    for k, sl in enumerate(objs):
        if area[k] > 250:
            hh = sl[0].stop - sl[0].start; ww = sl[1].stop - sl[1].start
            L = math.hypot(hh, ww)
            if L > 80 and area[k] < 0.25 * hh * ww + 60:
                trails.append(dict(len=float(L), area=int(area[k]), y=int(sl[0].start), x=int(sl[1].start)))
    res['trails'] = trails
    keep = (area >= 5) & (area <= 400)
    res['n_det'] = int(keep.sum())
    res['n_det_bright'] = int((keep & (peak > 50 * sigma)).sum())
    res['n_sat_stars'] = int((peak >= WHITE - BLACK - 2).sum())
    cand = np.where(keep & (peak < 0.8 * (WHITE - BLACK)) & (peak > 20 * sigma))[0]
    cand = cand[np.argsort(-flux[cand])][:NSTAR]
    com = ndimage.center_of_mass(d, lab, (cand + 1).tolist()) if len(cand) else []
    stars = []
    for k, (cy, cx) in zip(cand, com):
        m = moments(d, cy, cx)
        if m is None or m['fwhm'] < 1.0 or m['fwhm'] > 15: continue
        m['peak'] = float(peak[k]); m['area'] = int(area[k]); stars.append(m)
    hs = [halo(d, s['y'], s['x']) for s in stars[:40]]
    hs = [h for h in hs if h is not None]
    res['halo'] = float(np.median(hs)) if hs else None
    reg = np.where(keep)[0]; reg = reg[np.argsort(-flux[reg])][:600]
    rc = np.array(ndimage.center_of_mass(d, lab, (reg + 1).tolist())) if len(reg) else np.zeros((0, 2))
    res['reg_xy'] = np.c_[rc[:, 1], rc[:, 0], flux[reg]].tolist()
    f = np.array([s['fwhm'] for s in stars]); e = np.array([s['elong'] for s in stars])
    if len(stars):
        res['n_shape'] = len(stars); res['fwhm_med'] = float(np.median(f)); res['fwhm_p90'] = float(np.percentile(f, 90))
        res['elong_med'] = float(np.median(e)); res['elong_p90'] = float(np.percentile(e, 90))
        res['frac_elong13'] = float((e > 1.3).mean())
        pa = np.array([s['pa'] for s in stars])[e > 1.2]
        if len(pa) > 5:
            ang = np.radians(2 * pa); R = math.hypot(np.cos(ang).mean(), np.sin(ang).mean())
            res['pa_mean'] = float(0.5 * math.degrees(math.atan2(np.sin(ang).mean(), np.cos(ang).mean()))); res['pa_coherence'] = float(R)
        yy = np.array([s['y'] for s in stars]); xx = np.array([s['x'] for s in stars])
        rr = np.hypot(yy - H / 2, xx - W / 2)
        for nm, sel in (('fwhm_center', rr < 1000), ('fwhm_mid', (rr >= 1000) & (rr < 2200)), ('fwhm_edge', rr >= 2200)):
            res[nm] = float(np.median(f[sel])) if sel.sum() >= 5 else None
            res[nm.replace('fwhm', 'elong')] = float(np.median(e[sel])) if sel.sum() >= 5 else None
    res['stars'] = stars
    res['secs'] = time.time() - t0
    return res

if __name__ == '__main__':
    files = sorted(glob.glob(os.path.join(LIGHTS, '*.NEF')))
    os.makedirs(OUT, exist_ok=True)
    with Pool(4) as pool:
        for res in pool.imap(analyze, list(enumerate(files))):
            with open(os.path.join(OUT, res['file'] + '.json'), 'w') as fh: json.dump(res, fh)
            print(f"{res['file']} n={res['n_det']} fwhm={res.get('fwhm_med', -1):.2f} el={res.get('elong_med', -1):.2f} bg={res['bg_median']:.0f} sat={res['n_sat_stars']} trails={len(res['trails'])} {res['secs']:.1f}s", flush=True)
