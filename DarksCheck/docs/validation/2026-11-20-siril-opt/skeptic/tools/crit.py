"""Siril 1.4.4's dark-optimisation criterion, re-implemented from src/core/preprocess.c.

evaluateNoiseOfCalibratedImage(): copy light and master, master *= k (float32), light -= master
(float32, imoper_to_float clips >1 to 1 and < -1 to 0), then statistics(STATS_SIGMEAN) on the
centre box: area.x = (rx-512)/2, area.y = (ry-512)/2, 512x512, rows taken from the bottom-up
buffer at (ry - y - h) (select_area_float); FnMeanSigma_float: mean and sqrt(sum2/n - mean^2)
over the pixels that are not exactly 0 and not NaN, in double; noise is a float.
goldenSectionSearch(): float32, [0, 2], tol 0.001f, "if (fc == fd) return 1.f", result (a+b)/2.
The light is the one before any bias/level subtraction (darkOptimization runs before
preprocess() in prepro_image_hook).

Usage as a module: box(path) -> float32 centre box in Siril's 0..1 scale; siril_k0(L, D);
k_star(L, D) = cov/var on the same box (double).
"""
import numpy as np
from astropy.io import fits

INV_USHRT = np.float32(0.000015259022)  # INV_USHRT_MAX_SINGLE in siril.h
SIZE = 512


def full(path):
    """Whole frame in Siril's 0..1 float scale, buffer order as in the file (bottom-up)."""
    with fits.open(path, memmap=False) as f:
        h = f[0].header
        data = f[0].data
    if h["BITPIX"] == 16:
        return data.astype(np.float32) * INV_USHRT
    if h["BITPIX"] == -32:
        return data.astype(np.float32)
    raise SystemExit(f"unexpected BITPIX {h['BITPIX']} in {path}")


def box_slices(ry, rx, size=SIZE):
    x = (rx - size) // 2
    y = (ry - size) // 2
    r0 = ry - y - size  # select_area_float: fpdata + (ry - y - h) * rx + x
    return slice(r0, r0 + size), slice(x, x + size)


def box(path, size=SIZE):
    with fits.open(path, memmap=False) as f:
        h = f[0].header
        ry, rx = h["NAXIS2"], h["NAXIS1"]
        sy, sx = box_slices(ry, rx, size)
        data = np.array(f[0].data[sy, sx])
        bitpix = h["BITPIX"]
    if bitpix == 16:
        return data.astype(np.float32) * INV_USHRT
    if bitpix == -32:
        return data.astype(np.float32)
    raise SystemExit(f"unexpected BITPIX {bitpix} in {path}")


def noise(L, D, k):
    """evaluateNoiseOfCalibratedImage on the box (float result, as Siril keeps it)."""
    r = L - D * np.float32(k)
    r = np.where(r > 1.0, np.float32(1.0), r)
    r = np.where(r < -1.0, np.float32(0.0), r)
    v = r[(r != 0) & ~np.isnan(r)].astype(np.float64)
    n = v.size
    mean = v.sum() / n
    sigma = np.sqrt(v.dot(v) / n - mean * mean)
    return np.float32(sigma)  # noise += sigma / normValue (normValue cancels), kept as float


def siril_k0(L, D, trace=False):
    f32 = np.float32
    GR = f32((np.sqrt(f32(5.0)) - f32(1.0)) / f32(2.0))
    a, b, tol = f32(0.0), f32(2.0), f32(0.001)
    c = f32(b - f32(GR * f32(b - a)))
    d = f32(a + f32(GR * f32(b - a)))
    fc, fd = noise(L, D, c), noise(L, D, d)
    steps = [(float(c), float(fc)), (float(d), float(fd))]
    if fc == fd:
        return 1.0, steps
    while True:
        if fc < 0 or fd < 0:
            return -1.0, steps
        if fc < fd:
            b = d
            d = c
            fd = fc
            c = f32(b - f32(GR * f32(b - a)))
            fc = noise(L, D, c)
            steps.append((float(c), float(fc)))
        else:
            a = c
            c = d
            fc = fd
            d = f32(a + f32(GR * f32(b - a)))
            fd = noise(L, D, d)
            steps.append((float(d), float(fd)))
        if not abs(f32(c - d)) > tol:
            break
    return float(f32(f32(b + a) * f32(0.5))), steps


def k_star(L, D):
    Ld, Dd = L.astype(np.float64), D.astype(np.float64)
    Lc, Dc = Ld - Ld.mean(), Dd - Dd.mean()
    return float((Lc * Dc).sum() / (Dc * Dc).sum())


SITES = [(0, 0), (0, 1), (1, 0), (1, 1)]


def decompose(L, D):
    """cov(L, D) and var(D) (ADU^2) split into the colour-site-mean part and the within-site rest."""
    Ld, Dd = L.astype(np.float64) * 65535.0, D.astype(np.float64) * 65535.0
    n = Ld.size
    Lm, Dm = Ld.mean(), Dd.mean()
    cov_site = var_site = cov_in = var_in = 0.0
    lsite, dsite = [], []
    for oy, ox in SITES:
        l, d = Ld[oy::2, ox::2], Dd[oy::2, ox::2]
        lm, dm = l.mean(), d.mean()
        lsite.append(lm)
        dsite.append(dm)
        w = l.size / n
        cov_site += w * (lm - Lm) * (dm - Dm)
        var_site += w * (dm - Dm) ** 2
        cov_in += ((l - lm) * (d - dm)).sum() / n
        var_in += ((d - dm) ** 2).sum() / n
    return dict(cov_site=cov_site, cov_within=cov_in, var_site=var_site, var_within=var_in,
                k_star=(cov_site + cov_in) / (var_site + var_in), k_within=cov_in / var_in,
                light_sites=[round(x, 2) for x in lsite], dark_sites=[round(x, 3) for x in dsite])
