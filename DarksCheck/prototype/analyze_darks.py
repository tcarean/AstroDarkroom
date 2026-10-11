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

"""One-off check of the darks of a DSLR session (Nikon D3300, RGGB, 12-bit, black 150).
Reads the NEF files directly with rawpy (raw CFA, no demosaic), streams what it can,
holds only the darks in memory for a true median master.
Usage: python -I analyze_darks.py <out_dir> [session_dir] [library_darks_dir]
Writes <out_dir>/results.json, per-frame CSVs and PNG figures."""
import sys, os, glob, json, time, csv
import numpy as np
import rawpy
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from nefexif import read as exif

OUT = sys.argv[1]
SESSION = sys.argv[2] if len(sys.argv) > 2 else r'E:\Photografie\Astro\Milkyway-Core\2026-08-12-Smida'
LIB = sys.argv[3] if len(sys.argv) > 3 else r'E:\Photografie\Astro\Flats and Darks\Darks\2026-10-10-ISO1600-120s-15.8celsius\darks'
os.makedirs(OUT, exist_ok=True)
BLACK, WHITE = 150, 4095
PLANES = {'R': (0, 0), 'G1': (0, 1), 'G2': (1, 0), 'B': (1, 1)}   # RGGB
T0 = time.time()
R = {}   # results


def log(*a):
    print(f'[{time.time()-T0:6.0f}s]', *a, flush=True)


def load(p):
    with rawpy.imread(p) as r:
        return r.raw_image_visible.copy()


def plane(a, name):
    r0, c0 = PLANES[name]
    return a[r0::2, c0::2]


def mad_sigma(x):
    """Clipped standard deviation (the NEF is quantised in steps of 2-4 ADU, so a MAD is 0 or a step).
    Hot pixels are clipped away at 5 sigma, three passes. Returns (sigma, median)."""
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


def quant_step(x, lo, hi):
    """Smallest spacing between the values present in [lo, hi]."""
    v = np.unique(x[(x >= lo) & (x <= hi)])
    return int(np.diff(v).min()) if v.size > 1 else None


def frame_stats(a):
    s = {}
    for n in PLANES:
        p = plane(a, n)
        sig, med = mad_sigma(p)
        s[n] = dict(median=med - BLACK, sigma=sig, mean=float(p.mean()) - BLACK,
                    p999=float(np.percentile(p, 99.9)) - BLACK,
                    n_gt10=int((p > BLACK + 10).sum()), n_gt20=int((p > BLACK + 20).sum()), n_gt50=int((p > BLACK + 50).sum()),
                    n_gt100=int((p > BLACK + 100).sum()), n_sat=int((p >= WHITE).sum()),
                    n_low=int((p < 100).sum()), min=int(p.min()), max=int(p.max()))
    return s


def binned(a, b=16):
    """Median-free block mean per CFA plane, block b x b plane pixels (2b full-res)."""
    out = {}
    for n in PLANES:
        p = plane(a, n).astype(np.float32)
        h, w = (p.shape[0] // b) * b, (p.shape[1] // b) * b
        out[n] = p[:h, :w].reshape(h // b, b, w // b, b).mean(axis=(1, 3)) - BLACK
    return out


def regions(m):
    """Centre vs corners/edges of a binned plane map."""
    h, w = m.shape
    c = m[int(.4*h):int(.6*h), int(.4*w):int(.6*w)].mean()
    k = dict(center=float(c),
             top_left=float(m[:h//10, :w//10].mean()), top_right=float(m[:h//10, -(w//10):].mean()),
             bottom_left=float(m[-(h//10):, :w//10].mean()), bottom_right=float(m[-(h//10):, -(w//10):].mean()),
             top=float(m[:h//10].mean()), bottom=float(m[-(h//10):].mean()),
             left=float(m[:, :w//10].mean()), right=float(m[:, -(w//10):].mean()))
    return k


def banding(d):
    """d: float32 full-res residual. Row/col mean std vs. what white noise would give."""
    out = {}
    for n in PLANES:
        p = plane(d, n)
        sig, _ = mad_sigma(p)
        rows, cols = p.mean(axis=1), p.mean(axis=0)
        out[n] = dict(row_std=float(rows.std()), row_expected=float(sig / np.sqrt(p.shape[1])),
                      col_std=float(cols.std()), col_expected=float(sig / np.sqrt(p.shape[0])))
    return out


def local_excess(a):
    """Per CFA plane: pixel minus median of its 8 same-colour neighbours, and the plane sigma.
    Returns full-res float32 excess E and full-res Z = E/sigma(plane)."""
    E = np.empty(a.shape, np.float32)
    Z = np.empty(a.shape, np.float32)
    for n, (r0, c0) in PLANES.items():
        p = a[r0::2, c0::2].astype(np.float32)
        pp = np.pad(p, 1, mode='edge')
        H, W = p.shape
        st = np.stack([pp[0:H, 0:W], pp[0:H, 1:W+1], pp[0:H, 2:W+2],
                       pp[1:H+1, 0:W], pp[1:H+1, 2:W+2],
                       pp[2:H+2, 0:W], pp[2:H+2, 1:W+1], pp[2:H+2, 2:W+2]])
        med = np.median(st, axis=0)
        del st
        e = p - med
        sig, _ = mad_sigma(e[::4, ::4])
        E[r0::2, c0::2] = e
        Z[r0::2, c0::2] = e / sig
    return E, Z


def neighbour_max(Z):
    zp = np.pad(Z, 1, mode='constant', constant_values=0)
    H, W = Z.shape
    m = np.full(Z.shape, -np.inf, np.float32)
    for dr in (0, 1, 2):
        for dc in (0, 1, 2):
            if dr == 1 and dc == 1:
                continue
            np.maximum(m, zp[dr:dr+H, dc:dc+W], out=m)
    return m


def write_csv(path, rows):
    if not rows:
        return
    keys = list(rows[0].keys())
    with open(path, 'w', newline='') as f:
        w = csv.DictWriter(f, fieldnames=keys)
        w.writeheader()
        w.writerows(rows)


def flat_row(name, e, st):
    row = dict(file=name, time=e.get('DateTimeOriginal'), exposure=e.get('ExposureTime'), iso=e.get('ISO'))
    for n in PLANES:
        for k, v in st[n].items():
            row[f'{n}_{k}'] = v
    return row


# ----------------------------------------------------------------- darks (in memory)
dark_files = sorted(glob.glob(os.path.join(SESSION, 'darks', '*.NEF')))
log(f'darks: {len(dark_files)} files')
first = load(dark_files[0])
H, W = first.shape
g = plane(first, 'G1')
R['quantisation'] = dict(dark_step_near_black=quant_step(g, 140, 180), dark_unique_values=int(np.unique(g).size),
                         dark_values_near_black={int(v): int(c) for v, c in zip(*np.unique(g[(g >= 140) & (g <= 170)], return_counts=True))})
del g
stack = np.empty((len(dark_files), H, W), np.uint16)
dark_rows, dark_exif = [], []
for i, p in enumerate(dark_files):
    a = first if i == 0 else load(p)
    stack[i] = a
    e = exif(p)
    dark_exif.append(e)
    dark_rows.append(flat_row(os.path.basename(p), e, frame_stats(a)))
    log('  dark', os.path.basename(p), 'G1 median', round(dark_rows[-1]['G1_median'], 2),
        'sigma', round(dark_rows[-1]['G1_sigma'], 2), 'n>50', dark_rows[-1]['G1_n_gt50'])
del first
write_csv(os.path.join(OUT, 'darks_per_frame.csv'), dark_rows)
R['darks'] = dict(n=len(dark_files), exposures=sorted({r['exposure'] for r in dark_rows}),
                  isos=sorted({r['iso'] for r in dark_rows}),
                  first=dark_rows[0]['time'], last=dark_rows[-1]['time'],
                  gaps=[(dark_exif[i]['dt'] - dark_exif[i-1]['dt']).total_seconds() for i in range(1, len(dark_exif))])

# median master, per-pixel temporal std, in row chunks
log('median master ...')
master = np.empty((H, W), np.float32)
tstd = np.empty((H, W), np.float32)
CH = 256
for r0 in range(0, H, CH):
    blk = stack[:, r0:r0+CH, :]
    master[r0:r0+CH] = np.median(blk, axis=0)
    tstd[r0:r0+CH] = blk.astype(np.float32).std(axis=0)
mean_master = stack.mean(axis=0, dtype=np.float64).astype(np.float32)
excess = master - BLACK
R['dark_master'] = {n: dict(median=float(np.median(plane(excess, n))),
                            sigma=mad_sigma(plane(excess, n))[0],
                            n_gt20=int((plane(excess, n) > 20).sum()), n_gt50=int((plane(excess, n) > 50).sum()),
                            n_gt100=int((plane(excess, n) > 100).sum()), n_gt500=int((plane(excess, n) > 500).sum()),
                            n_sat=int((plane(master, n) >= WHITE - 1).sum()), n_low=int((plane(master, n) < 100).sum()))
                    for n in PLANES}
log('master:', json.dumps(R['dark_master']['G1']))

# per-frame residual: noise, banding, hot counts relative to the master
sig1 = {n: float(np.median([r[f'{n}_sigma'] for r in dark_rows])) for n in PLANES}
resid_rows = []
for i, p in enumerate(dark_files):
    d = stack[i].astype(np.float32) - master
    row = dict(file=os.path.basename(p))
    for n in PLANES:
        pd = plane(d, n)
        s, m = mad_sigma(pd)
        row[f'{n}_resid_sigma'] = s
        row[f'{n}_resid_median'] = m
        row[f'{n}_resid_mean'] = float(pd.mean())
    b = banding(d)
    for n in PLANES:
        row[f'{n}_row_std'] = b[n]['row_std']; row[f'{n}_row_exp'] = b[n]['row_expected']
        row[f'{n}_col_std'] = b[n]['col_std']; row[f'{n}_col_exp'] = b[n]['col_expected']
    resid_rows.append(row)
    log('  resid', row['file'], 'G1 sigma', round(row['G1_resid_sigma'], 2), 'median', round(row['G1_resid_median'], 2),
        'rows', round(row['G1_row_std'], 3), '/', round(row['G1_row_exp'], 3))
    del d
write_csv(os.path.join(OUT, 'darks_residual_per_frame.csv'), resid_rows)

# hot-pixel stability: pixels hot in the master, how many frames show them hot
HOT_T = 20   # ADU above black in the median master (10 quantisation steps, ~13 sigma at ISO 400)
hot_mask = excess > HOT_T
hot_idx = np.flatnonzero(hot_mask)
log(f'hot pixels (master > {HOT_T} ADU): {hot_idx.size}')
flat_stack = stack.reshape(len(dark_files), -1)
hot_vals = flat_stack[:, hot_idx].astype(np.float32) - BLACK      # (N, nhot)
frames_hot = (hot_vals > HOT_T / 2).sum(axis=0)                   # frames where the pixel is still at least half-hot
R['hot_stability'] = dict(threshold=HOT_T, n_hot=int(hot_idx.size),
                          frames_hot_hist=np.bincount(frames_hot, minlength=len(dark_files)+1).tolist(),
                          n_always=int((frames_hot == len(dark_files)).sum()),
                          n_flicker=int((frames_hot < len(dark_files) - 1).sum()),
                          excess_percentiles={str(q): float(np.percentile(excess[hot_mask], q)) for q in (10, 50, 90, 99)},
                          rts_candidates=int((plane(tstd, 'G1') > 6 * sig1['G1']).sum()))
# weaker hot pixels: 5 sigma of the master's own noise
sig_master = {n: mad_sigma(plane(excess, n))[0] for n in PLANES}
R['hot_levels'] = {n: dict(sigma_master=sig_master[n], n_gt5sig=int((plane(excess, n) > 5 * sig_master[n]).sum()))
                   for n in PLANES}
# temporal std as RTS indicator over hot pixels
R['hot_stability']['hot_temporal_std_median'] = float(np.median(tstd.reshape(-1)[hot_idx]))
R['hot_stability']['quiet_temporal_std_median'] = float(np.median(plane(tstd, 'G1')[::8, ::8]))

# noise versus N (master as mean of n frames), central crop, G1 plane, hot pixels excluded
log('noise vs N ...')
r0, c0 = PLANES['G1']
crop = stack[:, H//2-1024:H//2+1024, W//2-1024:W//2+1024][:, r0::2, c0::2].astype(np.float32)
cmask = plane(excess[H//2-1024:H//2+1024, W//2-1024:W//2+1024], 'G1') < 20
nvsN = []
N = len(dark_files)
for n in (1, 2, 4, 5, 10):
    if 2 * n > N:
        break
    a = crop[:n].mean(axis=0); b = crop[n:2*n].mean(axis=0)
    s, _ = mad_sigma((a - b)[cmask])
    nvsN.append(dict(n=n, sigma_master=s / np.sqrt(2), expected=sig1['G1'] / np.sqrt(n)))
s_med, _ = mad_sigma((crop[:N//2].mean(axis=0) - np.median(crop[N//2:], axis=0))[cmask])
R['noise_vs_n'] = dict(curve=nvsN, sigma_frame_G1=sig1['G1'],
                       median_of_half_vs_mean_of_half=s_med)
del crop
del stack, flat_stack

# light-leak / gradient maps of the dark master
bd = binned(master)
R['dark_master_regions'] = {n: regions(bd[n]) for n in PLANES}
mb = banding(master - np.median(master))   # master's own structured banding
R['dark_master_banding'] = mb
rowprof = {n: plane(excess, n).mean(axis=1) for n in PLANES}
colprof = {n: plane(excess, n).mean(axis=0) for n in PLANES}
from scipy.ndimage import median_filter
R['dark_master_banding_structured'] = {}
for n in PLANES:
    rp = rowprof[n] - median_filter(rowprof[n], 101, mode='nearest')
    cp = colprof[n] - median_filter(colprof[n], 101, mode='nearest')
    R['dark_master_banding_structured'][n] = dict(row_std=float(rp.std()), col_std=float(cp.std()),
                                                  row_p2p=float(rp.max() - rp.min()), col_p2p=float(cp.max() - cp.min()))
np.save(os.path.join(OUT, 'dark_master_binned_G1.npy'), bd['G1'])

# ----------------------------------------------------------------- biases (streamed)
bias_files = sorted(glob.glob(os.path.join(SESSION, 'biases', '*.NEF')))
log(f'biases: {len(bias_files)} files')
bias_rows, bsum, prev = [], np.zeros((H, W), np.float64), None
pair_noise = []
for p in bias_files:
    a = load(p)
    e = exif(p)
    bias_rows.append(flat_row(os.path.basename(p), e, frame_stats(a)))
    bsum += a
    if prev is not None:
        pair_noise.append({n: mad_sigma(plane(a.astype(np.float32) - prev, n))[0] / np.sqrt(2) for n in PLANES})
    prev = a.astype(np.float32)
    log('  bias', bias_rows[-1]['file'], 'G1 median', round(bias_rows[-1]['G1_median'], 2), 'sigma', round(bias_rows[-1]['G1_sigma'], 2),
        'min', bias_rows[-1]['G1_min'])
write_csv(os.path.join(OUT, 'biases_per_frame.csv'), bias_rows)
bias_master = (bsum / len(bias_files)).astype(np.float32)
del bsum, prev
R['biases'] = dict(n=len(bias_files), exposures=sorted({r['exposure'] for r in bias_rows}), isos=sorted({r['iso'] for r in bias_rows}),
                   first=bias_rows[0]['time'], last=bias_rows[-1]['time'],
                   read_noise_pairs={n: float(np.median([q[n] for q in pair_noise])) for n in PLANES},
                   master={n: dict(median=float(np.median(plane(bias_master, n))) - BLACK, sigma=mad_sigma(plane(bias_master, n))[0])
                           for n in PLANES})
# low-end clipping check on one bias: how much of the distribution sits at/below black - 3 sigma
a = load(bias_files[0]).astype(np.float32)
g = plane(a, 'G1')
R['biases']['clip_check'] = dict(min=int(g.min()), frac_below_black=float((g < BLACK).mean()),
                                 frac_below_black_minus_2sig=float((g < BLACK - 2 * mad_sigma(g)[0]).mean()),
                                 expected_gaussian_below_minus_2sig=0.0228)
del a, g
bb = binned(bias_master)
dc = {n: bd[n] - bb[n] for n in PLANES}       # dark current map (dark - bias), binned
R['dark_current_regions'] = {n: regions(dc[n]) for n in PLANES}
R['bias_master_regions'] = {n: regions(bb[n]) for n in PLANES}
np.save(os.path.join(OUT, 'dark_current_binned_G1.npy'), dc['G1'])
np.save(os.path.join(OUT, 'bias_master_binned_G1.npy'), bb['G1'])

# ----------------------------------------------------------------- lights (streamed)
light_files = sorted(glob.glob(os.path.join(SESSION, 'lights', '*.NEF')))
log(f'lights: {len(light_files)} files')
hot_count = np.zeros((H, W), np.uint8)      # isolated hot detections per pixel
star_count = np.zeros((H, W), np.uint8)
amp_sel = (excess > 20) & (master < 3000)   # dark hot pixels usable for the amplitude ratio
amp_idx = np.flatnonzero(amp_sel)
amp_dark = excess.reshape(-1)[amp_idx]
light_rows = []
for p in light_files:
    a = load(p)
    e = exif(p)
    row = dict(file=os.path.basename(p), time=e.get('DateTimeOriginal'), exposure=e.get('ExposureTime'), iso=e.get('ISO'))
    if not light_rows:
        R['quantisation']['light_step_near_sky'] = quant_step(plane(a, 'G1'), 500, 800)
        R['quantisation']['light_step_near_2000'] = quant_step(plane(a, 'G1'), 1900, 2200)
    for n in PLANES:
        s, m = mad_sigma(plane(a, n)[::4, ::4])
        row[f'{n}_sky_median'] = m - BLACK
        row[f'{n}_sky_sigma'] = s
    E, Z = local_excess(a)
    nm = neighbour_max(Z)
    cand = (Z > 6) & (a < 4000)
    iso = cand & (nm < 3)
    hot_count += iso
    star_count += cand & ~iso
    row['n_candidates'] = int(cand.sum()); row['n_isolated_hot'] = int(iso.sum())
    ratio = E.reshape(-1)[amp_idx] / amp_dark
    row['amp_ratio_median'] = float(np.median(ratio))
    row['amp_ratio_q25'] = float(np.percentile(ratio, 25)); row['amp_ratio_q75'] = float(np.percentile(ratio, 75))
    # ratio by dark-excess bin
    for lo, hi in ((20, 40), (40, 80), (80, 160), (160, 3000)):
        sel = (amp_dark >= lo) & (amp_dark < hi)
        row[f'amp_ratio_{lo}_{hi}'] = float(np.median(ratio[sel])) if sel.sum() > 20 else None
    light_rows.append(row)
    log('  light', row['file'], 'sky G1', round(row['G1_sky_median'], 1), 'cand', row['n_candidates'], 'iso hot', row['n_isolated_hot'],
        'amp ratio', round(row['amp_ratio_median'], 3))
    del a, E, Z, nm, cand, iso
write_csv(os.path.join(OUT, 'lights_per_frame.csv'), light_rows)
NL = len(light_files)
persist = hot_count >= int(np.ceil(0.8 * NL))
R['lights'] = dict(n=NL, exposures=sorted({r['exposure'] for r in light_rows}), isos=sorted({r['iso'] for r in light_rows}),
                   first=light_rows[0]['time'], last=light_rows[-1]['time'],
                   hot_count_hist=np.bincount(hot_count.reshape(-1), minlength=NL+1).tolist(),
                   n_persistent=int(persist.sum()), persistent_threshold_frames=int(np.ceil(0.8 * NL)),
                   amp_ratio_median_over_night=float(np.median([r['amp_ratio_median'] for r in light_rows])),
                   amp_n_pixels=int(amp_idx.size))
np.save(os.path.join(OUT, 'light_hot_count.npy'), hot_count)

# ----------------------------------------------------------------- 9: lights vs dark
ov = {}
for T in (10, 20, 50, 100):
    dmask = excess > T
    both = dmask & persist
    ov[str(T)] = dict(n_dark=int(dmask.sum()), n_light_persistent=int(persist.sum()), n_both=int(both.sum()),
                      frac_light_in_dark=float(both.sum() / max(persist.sum(), 1)),
                      frac_dark_in_light=float(both.sum() / max(dmask.sum(), 1)))
# where are the persistent light hot pixels that the dark does not show as > 20?
missed = persist & ~(excess > 20)
R['overlap'] = dict(by_threshold=ov,
                    missed_dark_excess_percentiles={str(q): float(np.percentile(excess[missed], q)) for q in (10, 50, 90)} if missed.any() else None,
                    missed_count=int(missed.sum()),
                    missed_hot_count_median=float(np.median(hot_count[missed])) if missed.any() else None,
                    # light persistent pixels, by how many lights flagged them, vs dark excess
                    dark_excess_of_persistent_percentiles={str(q): float(np.percentile(excess[persist], q)) for q in (1, 10, 50, 90)})
# how many dark hot pixels (>50) does the light flag in how many frames?
R['overlap']['hot50_light_count_hist'] = np.bincount(hot_count[excess > 50], minlength=NL+1).tolist()

# ----------------------------------------------------------------- library darks (streamed, ISO 1600)
lib_files = sorted(glob.glob(os.path.join(LIB, '*.NEF')))
if lib_files:
    log(f'library darks: {len(lib_files)} files')
    lib_rows, lsum = [], np.zeros((H, W), np.float64)
    for p in lib_files:
        a = load(p)
        e = exif(p)
        if not lib_rows:
            R['quantisation']['library_step_near_black'] = quant_step(plane(a, 'G1'), 140, 200)
        lib_rows.append(flat_row(os.path.basename(p), e, frame_stats(a)))
        lsum += a
        log('  lib', lib_rows[-1]['file'], 'G1 median', round(lib_rows[-1]['G1_median'], 2), 'sigma', round(lib_rows[-1]['G1_sigma'], 2),
            'n>50', lib_rows[-1]['G1_n_gt50'], 'n>100', lib_rows[-1]['G1_n_gt100'])
    write_csv(os.path.join(OUT, 'library_darks_per_frame.csv'), lib_rows)
    lib_master = (lsum / len(lib_files)).astype(np.float32)
    lex = lib_master - BLACK
    lb = binned(lib_master)
    sel = (excess > 50) & (master < 2000) & (lib_master < 4000)
    ratio = lex[sel] / excess[sel]
    R['library'] = dict(n=len(lib_files), exposures=sorted({r['exposure'] for r in lib_rows}), isos=sorted({r['iso'] for r in lib_rows}),
                        first=lib_rows[0]['time'], last=lib_rows[-1]['time'],
                        master={n: dict(median=float(np.median(plane(lex, n))), sigma=mad_sigma(plane(lex, n))[0],
                                        n_gt50=int((plane(lex, n) > 50).sum()), n_gt200=int((plane(lex, n) > 200).sum()),
                                        n_sat=int((plane(lib_master, n) >= WHITE - 1).sum())) for n in PLANES},
                        regions={n: regions(lb[n]) for n in PLANES},
                        smida_hot50_in_lib_gt200=float(((lex > 200) & (excess > 50)).sum() / max((excess > 50).sum(), 1)),
                        lib_gt200_in_smida_gt20=float(((lex > 200) & (excess > 20)).sum() / max((lex > 200).sum(), 1)),
                        amp_ratio_lib_over_smida_median=float(np.median(ratio)), amp_ratio_q25=float(np.percentile(ratio, 25)),
                        amp_ratio_q75=float(np.percentile(ratio, 75)), amp_n=int(sel.sum()))
    np.save(os.path.join(OUT, 'library_master_binned_G1.npy'), lb['G1'])

with open(os.path.join(OUT, 'results.json'), 'w') as f:
    json.dump(R, f, indent=1, default=str)
log('done')
