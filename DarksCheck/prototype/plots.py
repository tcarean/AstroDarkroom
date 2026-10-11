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

"""Figures for the darks report. Usage: python -I plots.py <report_dir>"""
import sys, os, json, csv
import numpy as np
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from datetime import datetime

D = sys.argv[1]
R = json.load(open(os.path.join(D, 'results.json')))
PL = ['R', 'G1', 'G2', 'B']
COL = {'R': '#b2453c', 'G1': '#2e8b57', 'G2': '#7fb77e', 'B': '#3b6fb6'}
LS = {'R': '-', 'G1': '-', 'G2': '--', 'B': '-'}
plt.rcParams.update({'font.size': 9, 'axes.spines.top': False, 'axes.spines.right': False,
                     'axes.grid': True, 'grid.alpha': 0.25, 'lines.linewidth': 1.4})


def rows(name):
    with open(os.path.join(D, name), newline='') as f:
        return [{k: (float(v) if v not in ('', 'None') and k not in ('file', 'time') else v) for k, v in r.items()}
                for r in csv.DictReader(f)]


def minutes(rs):
    t = [datetime.strptime(r['time'], '%Y:%m:%d %H:%M:%S') for r in rs]
    return [(x - t[0]).total_seconds() / 60 for x in t]


def save(fig, name):
    fig.tight_layout()
    fig.savefig(os.path.join(D, name), dpi=130)
    plt.close(fig)


# 1. darks over the series: level and hot counts
dk = rows('darks_per_frame.csv')
t = minutes(dk)
fig, ax = plt.subplots(2, 1, figsize=(8, 5.2), sharex=True)
for n in PL:
    ax[0].plot(t, [r[f'{n}_mean'] for r in dk], LS[n], color=COL[n], label=n)
    ax[1].plot(t, [r[f'{n}_n_gt20'] for r in dk], LS[n], color=COL[n], label=n)
ax[0].set_ylabel('mean above black [ADU]'); ax[0].set_title('Darks, frame by frame (first frame = 0 min)')
ax[1].set_ylabel('pixels > black+20 [per plane]'); ax[1].set_xlabel('minutes since the first dark')
ax[0].legend(ncol=4, frameon=False)
save(fig, 'fig1_darks_series.png')

# 2. maps: dark master, bias master, dark current (binned G1)
mean_maps = os.path.exists(os.path.join(D, 'dark_mean_binned_G1.npy'))
dm = np.load(os.path.join(D, 'dark_mean_binned_G1.npy' if mean_maps else 'dark_master_binned_G1.npy'))
bm = np.load(os.path.join(D, 'bias_master_binned_G1.npy'))
dc = np.load(os.path.join(D, 'dark_current_mean_binned_G1.npy' if mean_maps else 'dark_current_binned_G1.npy'))
fig, ax = plt.subplots(1, 3, figsize=(12, 3.4))
for a, m, title in zip(ax, (dm, bm, dc), ('dark master - black', 'bias master - black', 'dark - bias (dark current)')):
    lo, hi = np.percentile(m, 1), np.percentile(m, 99)
    im = a.imshow(m, cmap='Blues', vmin=lo, vmax=hi, aspect='auto')
    a.set_title(f'{title}  [{lo:.2f} .. {hi:.2f} ADU]'); a.grid(False); a.set_xticks([]); a.set_yticks([])
    fig.colorbar(im, ax=a, fraction=0.03, pad=0.02)
save(fig, 'fig2_maps_G1.png')

# 2b. row / column profiles of the dark current map
fig, ax = plt.subplots(1, 2, figsize=(10, 3))
ax[0].plot(dc.mean(axis=1), color=COL['G1']); ax[0].set_title('dark current, row profile (top -> bottom)'); ax[0].set_ylabel('ADU'); ax[0].set_xlabel('row bin')
ax[1].plot(dc.mean(axis=0), color=COL['G1']); ax[1].set_title('dark current, column profile (left -> right)'); ax[1].set_xlabel('column bin')
save(fig, 'fig2b_profiles_G1.png')

# 3. hot-pixel stability
hs = R['hot_stability']
hist = hs['frames_hot_hist']
fig, ax = plt.subplots(1, 2, figsize=(10, 3.4))
ax[0].bar(range(len(hist)), hist, color=COL['G1'], width=0.8)
ax[0].set_xlabel(f"frames (of {len(hist)-1}) in which the pixel stays above {hs['threshold']/2:.0f} ADU")
ax[0].set_ylabel('pixels'); ax[0].set_title(f"stability of the {hs['n_hot']} pixels > {hs['threshold']} ADU in the master")
ax[0].set_yscale('log')
lh = R['lights']['hot_count_hist']
ax[1].bar(range(len(lh)), lh, color=COL['B'], width=0.8)
ax[1].set_yscale('log'); ax[1].set_xlabel(f"lights (of {len(lh)-1}) that flag the pixel as an isolated hot pixel")
ax[1].set_ylabel('pixels'); ax[1].set_title('hot pixels seen in the lights')
save(fig, 'fig3_hot_stability.png')

# 4. noise
rr = rows('darks_residual_per_frame.csv')
fig, ax = plt.subplots(1, 2, figsize=(10, 3.4))
for n in PL:
    ax[0].plot(range(1, len(rr) + 1), [r[f'{n}_resid_sigma'] for r in rr], LS[n], color=COL[n], label=n)
ax[0].set_xlabel('dark frame'); ax[0].set_ylabel('sigma of (frame - master) [ADU]'); ax[0].set_title('per-frame noise'); ax[0].legend(ncol=4, frameon=False)
c = R['noise_vs_n']['curve']
ax[1].plot([k['n'] for k in c], [k['sigma_master'] for k in c], 'o-', color=COL['G1'], label='measured')
ax[1].plot([k['n'] for k in c], [k['expected'] for k in c], '--', color='#777777', label='sigma / sqrt(n)')
ax[1].set_xscale('log'); ax[1].set_yscale('log'); ax[1].set_xlabel('frames averaged n'); ax[1].set_ylabel('noise of the mean [ADU]')
ax[1].set_title('master noise vs frames (G1, centre)'); ax[1].legend(frameon=False)
save(fig, 'fig4_noise.png')

# 5. hot-pixel amplitude in the lights relative to the dark master
lt = rows('lights_per_frame.csv')
tl = minutes(lt)
fig, ax = plt.subplots(figsize=(8, 3.4))
ax.fill_between(tl, [r['amp_ratio_q25'] for r in lt], [r['amp_ratio_q75'] for r in lt], color=COL['B'], alpha=0.15, label='quartiles')
ax.plot(tl, [r['amp_ratio_median'] for r in lt], color=COL['B'], label='median')
ax.axhline(1, color='#777777', lw=1, ls='--')
ax.set_xlabel('minutes since the first light'); ax.set_ylabel('light excess / dark excess')
ax.set_title(f"hot-pixel amplitude in each light relative to the dark master ({R['lights']['amp_n_pixels']} pixels > 20 ADU)")
ax.legend(frameon=False)
save(fig, 'fig5_amp_ratio.png')

# 6. overlap lights vs dark
ov = R['overlap']['by_threshold']
Ts = list(ov.keys())
fig, ax = plt.subplots(figsize=(7, 3.2))
x = np.arange(len(Ts))
ax.bar(x - 0.2, [ov[T]['frac_light_in_dark'] for T in Ts], 0.4, color=COL['B'], label='lights-hot pixels present in the dark')
ax.bar(x + 0.2, [ov[T]['frac_dark_in_light'] for T in Ts], 0.4, color=COL['G1'], label='dark-hot pixels confirmed by the lights')
ax.set_xticks(x); ax.set_xticklabels([f'> {T} ADU\n({ov[T]["n_dark"]} px)' for T in Ts]); ax.set_ylim(0, 1.05)
ax.set_ylabel('fraction'); ax.set_title(f"overlap, dark threshold vs {ov[Ts[0]]['n_light_persistent']} persistent light hot pixels")
ax.legend(frameon=False, loc='lower right')
save(fig, 'fig6_overlap.png')

# 7. library darks
if 'library' in R:
    lb = rows('library_darks_per_frame.csv')
    t2 = minutes(lb)
    fig, ax = plt.subplots(2, 1, figsize=(8, 5.2), sharex=True)
    for n in PL:
        ax[0].plot(t2, [r[f'{n}_mean'] for r in lb], LS[n], color=COL[n], label=n)
        ax[1].plot(t2, [r[f'{n}_n_gt50'] for r in lb], LS[n], color=COL[n], label=n)
    ax[0].set_ylabel('mean above black [ADU]'); ax[0].set_title('Library darks 2026-10-10 (ISO 1600), frame by frame'); ax[0].legend(ncol=4, frameon=False)
    ax[1].set_ylabel('pixels > black+50 [per plane]'); ax[1].set_xlabel('minutes since the first dark')
    save(fig, 'fig7_library_series.png')
    lm = np.load(os.path.join(D, 'library_master_binned_G1.npy'))
    fig, ax = plt.subplots(figsize=(5, 3.4))
    lo, hi = np.percentile(lm, 1), np.percentile(lm, 99)
    im = ax.imshow(lm, cmap='Blues', vmin=lo, vmax=hi, aspect='auto'); ax.grid(False); ax.set_xticks([]); ax.set_yticks([])
    ax.set_title(f'library dark master - black, G1 [{lo:.2f} .. {hi:.2f} ADU]'); fig.colorbar(im, ax=ax, fraction=0.03, pad=0.02)
    save(fig, 'fig7b_library_map_G1.png')
# 8. warming: hot-pixel amplitude relative to the dark master, lights then darks, one time axis
if os.path.exists(os.path.join(D, 'darks_warming_per_frame.csv')):
    dw = rows('darks_warming_per_frame.csv')
    t0 = datetime.strptime(lt[0]['time'], '%Y:%m:%d %H:%M:%S')
    tl2 = [(datetime.strptime(r['time'], '%Y:%m:%d %H:%M:%S') - t0).total_seconds() / 60 for r in lt]
    td2 = [(datetime.strptime(r['time'], '%Y:%m:%d %H:%M:%S') - t0).total_seconds() / 60 for r in dw]
    fig, ax = plt.subplots(figsize=(9, 3.6))
    ax.fill_between(tl2, [r['amp_ratio_q25'] for r in lt], [r['amp_ratio_q75'] for r in lt], color=COL['B'], alpha=0.15)
    ax.plot(tl2, [r['amp_ratio_median'] for r in lt], 'o-', ms=3, color=COL['B'], label='lights')
    ax.fill_between(td2, [r['amp_ratio_q25'] for r in dw], [r['amp_ratio_q75'] for r in dw], color=COL['R'], alpha=0.15)
    ax.plot(td2, [r['amp_ratio_median'] for r in dw], 's-', ms=3, color=COL['R'], label='darks')
    ax.axhline(1, color='#777777', lw=1, ls='--')
    sec = ax.secondary_yaxis('right', functions=(lambda r: 6 * np.log2(np.maximum(r, 1e-3)), lambda d: 2 ** (d / 6)))
    sec.set_ylabel('temperature offset [°C], 6 °C doubling')
    ax.set_xlabel('minutes since the first light'); ax.set_ylabel('hot-pixel amplitude / dark master')
    ax.set_title('sensor warming through the night, read from the hot pixels (median and quartiles)')
    ax.legend(frameon=False)
    save(fig, 'fig8_warming.png')

# 9. library darks: warming and edge-vs-centre per frame
if os.path.exists(os.path.join(D, 'library_warming_per_frame.csv')):
    lw = rows('library_warming_per_frame.csv')
    t3 = minutes(lw)
    fig, ax = plt.subplots(1, 2, figsize=(10, 3.4))
    ax[0].fill_between(t3, [r['amp_ratio_q25'] for r in lw], [r['amp_ratio_q75'] for r in lw], color=COL['R'], alpha=0.15)
    ax[0].plot(t3, [r['amp_ratio_median'] for r in lw], 's-', ms=3, color=COL['R'])
    ax[0].axhline(1, color='#777777', lw=1, ls='--')
    ax[0].set_xlabel('minutes since the first library dark'); ax[0].set_ylabel('hot-pixel amplitude / library master')
    ax[0].set_title('library darks 2026-10-10: warming during the series')
    for k, c, ls in (('left_minus_center', COL['G1'], '-'), ('right_minus_center', COL['G1'], '--'),
                     ('top_minus_center', COL['B'], '-'), ('bottom_minus_center', COL['B'], '--')):
        ax[1].plot(t3, [r[k] for r in lw], ls, color=c, label=k.replace('_minus_center', ''))
    ax[1].set_xlabel('minutes since the first library dark'); ax[1].set_ylabel('edge - centre [ADU], G1')
    ax[1].set_title('edges relative to the centre, frame by frame'); ax[1].legend(ncol=4, frameon=False, fontsize=8)
    save(fig, 'fig9_library_warming_edges.png')
print('figures written')
