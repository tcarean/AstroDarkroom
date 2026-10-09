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

import sys, glob, numpy as np, rawpy
from scipy import ndimage
from astropy.stats import sigma_clipped_stats
BLACK=150.0; names={0:'R',1:'G1',2:'B',3:'G2'}
def chans(p):
    r=rawpy.imread(p); a=r.raw_image_visible.astype(np.float32)-BLACK; pat=r.raw_pattern; r.close()
    return {names[int(pat[i,j])]: a[i::2,j::2] for i in range(2) for j in range(2)}
def hp(sub):
    c=sub[700:1300,1200:1800]; return c-ndimage.median_filter(c,5), np.median(c)
b0,b1=chans(sys.argv[1]+'/DSC_0128.NEF'),chans(sys.argv[1]+'/DSC_0129.NEF')
rn={k: sigma_clipped_stats((b0[k]-b1[k])[700:1300,1200:1800], sigma=4)[2]/np.sqrt(2) for k in b0}
rnhp={k: sigma_clipped_stats(hp(b0[k])[0], sigma=4)[2] for k in b0}
print('read noise ADU (pair diff):', {k: round(float(v),2) for k,v in rn.items()}, ' hp estimator on bias:', {k: round(float(v),2) for k,v in rnhp.items()})
for f in ('DSC_0004.NEF','DSC_0040.NEF','DSC_0080.NEF'):
    ch=chans(sys.argv[2]+'/'+f)
    for k in ('R','G1','B'):
        h,m=hp(ch[k]); s=sigma_clipped_stats(h, sigma=4)[2]; s=s*rn[k]/rnhp[k]  # correct hp estimator bias using the bias frames
        shot=s*s-rn[k]**2
        print(f, k, 'sky', round(float(m),1), 'total sigma', round(float(s),2), 'shot sigma', round(float(np.sqrt(shot)),2), 'gain e/ADU', round(float(m/shot),3), 'shotvar/rn^2', round(float(shot/rn[k]**2),1), 'noise penalty %', round(100*(np.sqrt(1+rn[k]**2/shot)-1),1))
