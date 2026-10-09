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

import csv, numpy as np, math
rows = list(csv.DictReader(open('out/frames.csv')))
f = lambda k: np.array([float(r[k]) if r[k] not in ('', 'None') else np.nan for r in rows])
idx=f('idx').astype(int); el=f('elong_med'); fw=f('fwhm_med'); gap=f('gap_s'); step=f('step'); sdx=f('step_dx'); sdy=f('step_dy'); dx=f('dx'); dy=f('dy'); dmin=f('dmin'); tsec=f('tsec')
good = el < 1.35; marg = (el >= 1.35) & (el < 1.6); bad = el >= 1.6
print('classes: good', good.sum(), 'marginal', marg.sum(), 'bad', bad.sum())
print('bad:', idx[bad].tolist()); print('marginal:', idx[marg].tolist())
print('session', rows[0]['time'], '->', rows[-1]['time'], 'span min', round((tsec[-1]+120.2)/60,1), 'integration min', round(87*120.2/60,1))
pause = gap > 5
print('pauses (dither events):', pause.sum(), ' gaps median', np.nanmedian(gap[pause]), 'min', np.nanmin(gap[pause]), 'max', np.nanmax(gap[pause]), ' no-pause gaps:', np.unique(gap[~pause & ~np.isnan(gap)]))
st = step[pause]
print('dither steps: median', round(np.median(st),1), 'mean', round(st.mean(),1), ' <3:', (st<3).sum(), ' 3-5:', ((st>=3)&(st<5)).sum(), ' 5-10:', ((st>=5)&(st<10)).sum(), ' >=10:', (st>=10).sum(), ' >=20:', (st>=20).sum())
print('dither step components |dx| median', round(np.median(np.abs(sdx[pause])),1), '|dy| median', round(np.median(np.abs(sdy[pause])),1), ' |dx|>=10:', (np.abs(sdx[pause])>=10).sum(), ' |dy|>=10:', (np.abs(sdy[pause])>=10).sum())
print('dither dx list:', np.round(sdx[pause]).astype(int).tolist()); print('dither dy list:', np.round(sdy[pause]).astype(int).tolist())
np_ = ~pause & ~np.isnan(gap)
print('no-dither pairs: step median', round(np.median(step[np_]),1), 'mean dx', round(sdx[np_].mean(),2), 'mean dy', round(sdy[np_].mean(),2), 'p90 step', round(np.percentile(step[np_],90),1))
print('nearest other frame: median', round(np.median(dmin),1), ' frames with another frame within 3 px:', (dmin<3).sum(), ' within 5:', (dmin<5).sum(), ' >=10 from everything:', (dmin>=10).sum())
# runs: consecutive frames staying within R px of the run start
for R in (5, 10):
    runs=[]; s=0
    for i in range(1,len(idx)+1):
        if i==len(idx) or math.hypot(dx[i]-dx[s], dy[i]-dy[s])>R:
            if i-s>=3: runs.append((int(idx[s]), int(idx[i-1]), i-s)); 
            s=i
    print(f'runs of >=3 consecutive frames within {R} px of the run start:', runs)
# clusters: greedy
for R in (5, 10):
    cl=[]
    for i in range(len(idx)):
        for c in cl:
            if math.hypot(dx[i]-c[0], dy[i]-c[1])<=R: c[2].append(int(idx[i])); break
        else: cl.append([dx[i],dy[i],[int(idx[i])]])
    sizes=sorted([len(c[2]) for c in cl], reverse=True)
    print(f'distinct positions at {R} px: {len(cl)} of 87; biggest clusters:', sizes[:8], ' clusters>=4:', [c[2] for c in cl if len(c[2])>=4])
print('span x', round(dx.max()-dx.min()), 'span y', round(dy.max()-dy.min()))
rot=f('rot_deg'); print('rotation end', round(rot[-1],3), 'deg =', round(rot[-1]*60,1), 'arcmin; rate deg/h', round(rot[-1]/((tsec[-1])/3600),4))
for k in ('fwhm_center','fwhm_mid','fwhm_edge','elong_center','elong_edge','halo','n_det','G1_median','R_median','B_median','bg_corner_spread','fwhm_p90'):
    v=f(k); print(k, 'good frames median', round(np.nanmedian(v[good]),3), ' first10 good', round(np.nanmedian(v[good][:10]),3), ' last10 good', round(np.nanmedian(v[good][-10:]),3))
print('frame 75 bg', rows[74]['G1_median'], rows[74]['bg_corner_spread'], 'neighbours', rows[73]['G1_median'], rows[75]['G1_median'], 'spread neigh', rows[73]['bg_corner_spread'], rows[75]['bg_corner_spread'])
print('trails:', [(int(r['idx']), r['n_trails'], r['trail_maxlen']) for r in rows if int(r['n_trails'])>0])
print('sat px per green sub-channel: median', np.median(f('G1_sat')), 'R', np.median(f('R_sat')), 'B', np.median(f('B_sat')))
print('siril rejected', [17,22,23,43,50,54,56], 'my el of those', [round(el[i-1],2) for i in (17,22,23,43,50,54,56)])
print('bad frames by position in pair: first-after-pause', int((bad & pause).sum()), 'second', int((bad & np_).sum()), '; marginal first', int((marg & pause).sum()), 'second', int((marg & np_).sum()))
