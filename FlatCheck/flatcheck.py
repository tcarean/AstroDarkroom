#!/usr/bin/env python3
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

"""
flatcheck.py - diagnostic cantitativ pentru flat frames de astrofoto.

Prototipul din care a fost portat flatcheck.exe (C#). Nu se mentine in paralel cu el,
dar ramane ORACOLUL NUMERIC al portului: crosscheck.py ruleaza amandoua implementarile
pe aceiasi octeti si compara fiecare cifra.

DOCUMENTATIA DE UTILIZATOR E IN README.md, si numai acolo. Cele patru moduri, cand se
poate si cand NU se poate folosi --light, scara de reziduu, pragurile, codurile de
iesire si capcanele de workflow Siril - toate sunt explicate acolo. Nu le duplica aici:
au divergat deja o data, si versiunea din docstring era cea gresita.

Singurul lucru care se repeta aici, fiindca e cea mai frecventa cauza de rezultat
gresit si nu vrei sa depinda de citirea altui fisier:

    CE FISIER II DAI. Modurile 1-3 vor un flat CALIBRAT - din Siril, fisierul cu
    prefixul "pp_", adica process/pp_flat_stacked.fit. Un stack necalibrat mai are
    un piedestal care aplatizeaza profilul, iar toate testele vor spune "OK" oricum,
    fiindca profilul ramane simetric radial si doar amplitudinea e gresita. Nu e
    detectabil, doar evitabil.

    Modul --expo e exceptia: vrea un cadru BRUT, NECALIBRAT, cu mozaicul CFA intact.

Dependinte:

    pip install numpy astropy matplotlib
    pip install rawpy        # optional, doar daca dai .NEF direct la --expo
"""

import argparse
import os
import sys
import numpy as np

# Versiunea uneltei. Se incrementeaza MANUAL la fiecare feature/fix; nu o schimba de
# capul tau. Trebuie sa ramana identica cu VersionInfo.Value din C# (FlatCheck/Version.cs).
__version__ = "2.0.0"

# Notita de copyright, pe a doua linie a oricarei rulari - sub banner. GPLv3 cere ca
# fiecare copie sa o poarte. Identica cu VersionInfo.Copyright din C#.
__copyright__ = "Copyright (C) 2026 Tudor Carean"

# Notita de garantie ceruta de GPLv3 sectiunea 5(d): un program care afiseaza notite
# legale trebuie sa o afiseze si pe asta. Banner-ul de la fiecare rulare poarta doar
# copyright-ul - disclaimerul intreg ar ingrosa fiecare raport degeaba, deci sta in
# --help, unde omul vine oricum sa citeasca. Identic cu ce scrie C#-ul in WriteUsage.
__warranty__ = """Acest program vine FARA NICIO GARANTIE, nici macar cea implicita de
vandabilitate sau potrivire pentru un scop anume. E software liber:
il poti redistribui si modifica in termenii GPLv3 (sau, la alegerea
ta, orice versiune ulterioara). Textul complet e in fisierul LICENSE,
sau la <https://www.gnu.org/licenses/gpl-3.0.html>."""

# ---------------------------------------------------------------- praguri
# (procente; ajusteaza dupa ce vezi cifre reale pe setup-ul tau)
# Scara de reziduu, folosita de toate trei metricile de abatere de la simetria radiala:
# imprastierea colturilor, asimetria azimutala si reziduul dupa model. Toate trei
# masoara aceeasi abatere din unghiuri diferite, deci impart scara:
#     sub  2 %   calitate perfecta
#      2 - 10 %  utilizabil; background extraction scoate ce ramane
#     10 - 20 %  utilizabil, dar confirma cu --light inainte de un stretch dur
#     peste 20 % refa flat-urile
TH_RESID_PERFECT, TH_RESID_USABLE, TH_RESID_CHECK = 2.0, 10.0, 20.0

# Offsetul centrului de iluminare nu mai are praguri, fiindca nu mai e un test. Pana in
# 1.2.0 avea 4%/10% pe scara OK/SUSPECT/PROBLEMA, si era gresit: offsetul e ~ -b/(2d),
# adica raportul dintre gradient si curbura, deci nu e o fractie si nu e scale-invariant.
# Pe un obiectiv cu vignetare mica exploda, iar cand varful ajungea sa iasa din cadru
# cadea la 0.00% = "OK", adica devenea si nemonoton. Ce se masoara acum e TH_RESID_* pe
# "inclinare": acelasi gradient, luat ca amplitudine. Vezi fit_center.
TH_RATIO_OK, TH_RATIO_BAD = 1.0, 3.0       # structura in A/B (testul 180)

# --- end-to-end ---
TH_LIGHT_RAD_OK, TH_LIGHT_RAD_BAD = 2.0, 5.0

# Garda de aplicabilitate, pe DIAGONALA campului acoperit, in grade. Testul separa
# fondul in "plan de poluare luminoasa" + "termeni radiali = eroare de flat", iar
# separarea tine doar cat timp cupola de poluare e cvasi-liniara peste cadru. Pe camp
# larg ea se CURBEAZA in cadru, iar partea ei radial simetrica are exact forma
# vignetarii - doua lucruri cu aceeasi forma nu se separa printr-un fit.
# Nu e prag de calitate, e limita de valabilitate: peste TH_LIGHT_FOV_MAX modul refuza
# sa raspunda in loc sa dea un raspuns prost.
TH_LIGHT_FOV_OK, TH_LIGHT_FOV_MAX = 20.0, 40.0

# Cand headerul da doar FOCALLEN, fara marimea pixelului, campul real nu se poate
# calcula. Se presupune cel mai mare senzor plauzibil (full-frame) si se compara focala
# direct - conservator prin constructie. Pentru senzori mici exista --fov.
TH_LIGHT_FOCAL_OK, TH_LIGHT_FOCAL_MIN = 120.0, 60.0

# Gradient de cer peste care se tipareste un avertisment. Doar informativ.
TH_LIGHT_PLANE_WARN = 40.0

# --- end-to-end pe o serie ---
# Media pe serie e mai buna decat orice cadru singur, dar numai daca termenul radial al
# CERULUI se mediaza la zero peste sesiune. Conditiile de mai jos verifica exact asta.
# Sunt cele mai putin calibrate praguri din fisier: vin dintr-o singura sesiune reala.
TH_SERIES_MIN_FRAMES = 5
TH_SERIES_SEM_MAX = TH_LIGHT_RAD_OK
TH_SERIES_SKY_CORR = 0.70
TH_SERIES_SKY_SPAN_MIN = 10.0


# ---------------------------------------------------------------- I/O
def load_image(path):
    return load_image_with_header(path)[0]


def load_image_with_header(path):
    """Ca load_image, dar intoarce si cardurile HDU-ului din care a venit imaginea.

    --light are nevoie de header ca sa afle focala si scara, deci sa poata refuza un
    cadru cu camp prea larg pentru test (vezi light_suitability). Pentru raw-uri nu
    exista header FITS, deci se intoarce un dict gol.
    """
    p = path.lower()
    if p.endswith(('.fit', '.fits', '.fts')):
        from astropy.io import fits
        data, header = None, {}
        with fits.open(path) as hdul:
            for h in hdul:
                if h.data is not None and h.data.size > 1:
                    data = np.asarray(h.data, dtype=np.float64)
                    header = dict(h.header)
                    break
        if data is None:
            raise ValueError(f"Niciun array de imagine in {path}")
    elif p.endswith(('.nef', '.cr2', '.cr3', '.arw', '.dng', '.raf')):
        import rawpy
        header = {}
        with rawpy.imread(path) as raw:
            data = raw.raw_image_visible.astype(np.float64)
    else:
        raise ValueError(f"Format nerecunoscut: {path} (accept .fit/.fits, raw)")

    if data.ndim == 3:
        # Siril color = (3, H, W); alte tool-uri = (H, W, 3)
        data = data.mean(axis=0) if data.shape[0] in (3, 4) else data.mean(axis=2)
    if data.ndim != 2:
        raise ValueError(f"Astept 2D, am primit {data.shape}")
    return data, header


# ------------------------------------------------- garda de aplicabilitate --light
# Modul --light separa fondul in "plan de poluare luminoasa" + "termeni radiali =
# eroare de flat". Separarea tine cat timp cupola de poluare e cvasi-liniara peste
# cadru. Pe camp larg ea se CURBEAZA in interiorul cadrului, iar partea radial
# simetrica a acelei curburi are exact forma vignetarii: doua lucruri cu aceeasi forma
# nu se pot separa printr-un fit, oricat de bine scris.
#
# Nu e o limitare a implementarii, e o degenerare geometrica. De aceea peste
# TH_LIGHT_FOV_MAX modul REFUZA sa raspunda in loc sa dea un raspuns prost - exact
# cazul care a motivat codul asta: 20 mm pe APS-C (~70 grade) raporta "+3.13 % SUSPECT,
# SUB-corectie" pe un flat care nu avea nimic radial gresit.
#
# Ultimul strat conteaza cel mai mult ca decizie: cand nu se poate afla nimic, modul
# CONTINUA cu o nota, nu refuza. Altfel orice FITS cu header minimal ar fi de neanalizat.
FOV_OK, FOV_MARGINAL, FOV_TOO_WIDE, FOV_UNKNOWN = "OK", "MARGINAL", "TOO_WIDE", "UNKNOWN"


class Suitability:
    def __init__(self, level, fov=float('nan'), focal=float('nan'), source=None):
        self.level, self.fov, self.focal, self.source = level, fov, focal, source

    @property
    def can_run(self):
        return self.level != FOV_TOO_WIDE


def _hnum(header, key, default=0.0):
    try:
        return float(header.get(key, default))
    except (TypeError, ValueError):
        return default


def _num(v):
    """Focala fara zecimale inutile: '20mm', nu '20.0mm'."""
    return f"{v:.0f}" if abs(v - round(v)) < 1e-9 else f"{v:.1f}"


def _fov_level(fov_deg, source):
    level = (FOV_OK if fov_deg < TH_LIGHT_FOV_OK else
             FOV_MARGINAL if fov_deg < TH_LIGHT_FOV_MAX else FOV_TOO_WIDE)
    return Suitability(level, fov=fov_deg, source=source)


def light_suitability(header, fov_override=None):
    # 1. Declarat de om. Bate orice autodetectie - exista tocmai pentru cazul in care
    #    straturile de mai jos nu pot sti (DSLR fara XPIXSZ, senzor mic).
    if fov_override is not None:
        return _fov_level(fov_override, "--fov")
    if not header:
        return Suitability(FOV_UNKNOWN)

    n1, n2 = int(_hnum(header, "NAXIS1", 0)), int(_hnum(header, "NAXIS2", 0))
    if n1 <= 0 or n2 <= 0:
        return Suitability(FOV_UNKNOWN)
    diag_px = np.hypot(n1, n2)

    # 2. Plate solve: scara in grade/pixel e masurata, nu dedusa.
    scale = abs(_hnum(header, "CDELT1", 0.0))
    if scale <= 0:
        cd11, cd21 = _hnum(header, "CD1_1", 0.0), _hnum(header, "CD2_1", 0.0)
        if cd11 or cd21:
            scale = np.hypot(cd11, cd21)
    if scale > 0:
        return _fov_level(diag_px * scale, "WCS")

    focal = _hnum(header, "FOCALLEN", 0.0)
    if focal <= 0:
        return Suitability(FOV_UNKNOWN)

    # 3. Marimea pixelului + focala: campul real, exact. Siril scrie XPIXSZ pentru
    #    camere astro; pentru DSLR-uri nu, deci stratul asta sare des.
    xpix = _hnum(header, "XPIXSZ", 0.0)
    ypix = _hnum(header, "YPIXSZ", xpix)
    if xpix > 0 and ypix > 0:
        xbin = max(1, int(_hnum(header, "XBINNING", 1)))
        ybin = max(1, int(_hnum(header, "YBINNING", 1)))
        w_mm, h_mm = n1 * xbin * xpix / 1000.0, n2 * ybin * ypix / 1000.0
        fov = np.degrees(2 * np.arctan(np.hypot(w_mm, h_mm) / (2 * focal)))
        return _fov_level(fov, f"FOCALLEN={_num(focal)}mm + XPIXSZ")

    # 4. Doar focala. Campul nu se poate calcula - depinde de senzor - deci NU se
    #    raporteaza o cifra de camp, ca ar fi inventata. Se compara focala cu pragurile
    #    calculate pentru cel mai mare senzor plauzibil (full-frame): conservator, deci
    #    pe un senzor mic un cadru bun poate iesi "marginal". Pentru ala exista --fov.
    level = (FOV_OK if focal >= TH_LIGHT_FOCAL_OK else
             FOV_MARGINAL if focal >= TH_LIGHT_FOCAL_MIN else FOV_TOO_WIDE)
    return Suitability(level, focal=focal,
                       source=f"FOCALLEN={_num(focal)}mm, fara marimea pixelului")


def print_suitability(s):
    """Linia de camp acoperit, plus avertismentul cand e la limita.

    Cand campul nu s-a putut afla se scrie o linie care spune exact asta: necunoscut
    nu inseamna suspect, inseamna neverificat, iar tacerea ar lasa impresia ca s-a
    verificat.
    """
    if not np.isnan(s.fov):
        print(f"    camp diagonal           : {s.fov:5.1f} grade  ({s.source})")
    elif not np.isnan(s.focal):
        print(f"    camp acoperit           : nedeterminat ({s.source})")
    else:
        print("    camp acoperit           : necunoscut (headerul nu da nici focala, nici scara)")

    if s.level == FOV_MARGINAL:
        print(f"\n    ATENTIE: camp la limita. Separarea plan/radial slabeste peste {TH_LIGHT_FOV_OK:.0f}")
        print("    grade diagonala, iar cifra de mai jos poate contine si cer, nu doar flat.")
        print("    Citeste-o ca pe un indiciu, nu ca pe o masuratoare.")
    elif s.level == FOV_UNKNOWN:
        print("    (nu pot verifica daca testul se aplica; vezi --fov si README)")


def print_too_wide(s, path):
    """Ce se scrie in locul unui raport cand campul e prea larg."""
    print(f"\n=== TEST END-TO-END: {path} ===")
    print_suitability(s)
    print("\n  TESTUL NU SE APLICA PE CADRELE ASTEA")
    print(f"    Peste {TH_LIGHT_FOV_MAX:.0f} de grade diagonala, cupola de poluare luminoasa se")
    print("    CURBEAZA in interiorul cadrului. Partea radial simetrica a acelei curburi")
    print("    are exact forma vignetarii, deci fitul nu le mai poate separa: orice cifra")
    print("    ar iesi ar fi cerul, nu flat-ul.")
    print("    Nu e o limitare a uneltei, e o degenerare geometrica.")
    print("\n    Pentru un setup cu camp larg, testul care raspunde e rotatia la 180 de")
    print("    grade (--ratio): valideaza flat-ul fara sa implice cerul deloc.")
    if np.isnan(s.fov) and not np.isnan(s.focal):
        print("\n    Decizia s-a luat doar pe focala, presupunand cel mai mare senzor")
        print("    plauzibil. Daca al tau e mai mic, da campul real cu --fov <grade>.")


def glob_match(name, pattern):
    """Potrivire cu '*' si '?', fara distinctie de litere mari/mici.

    NU fnmatch: clasele de caractere ('[abc]') nu sunt suportate, sunt tratate literal.
    Acelasi algoritm ca GlobMatch din C#, ca sa nu apara o divergenta tacuta.
    """
    name, pattern = name.lower(), pattern.lower()
    n = p = 0
    star_p, star_n = -1, 0
    while n < len(name):
        if p < len(pattern) and pattern[p] in ('?', name[n]):
            p += 1
            n += 1
        elif p < len(pattern) and pattern[p] == '*':
            star_p, star_n = p, n
            p += 1
        elif star_p >= 0:
            star_n += 1
            p, n = star_p + 1, star_n
        else:
            return False
    while p < len(pattern) and pattern[p] == '*':
        p += 1
    return p == len(pattern)


def find_series(dirpath, pattern):
    """Fisierele din folder care se potrivesc cu tiparul, sortate ordinal pe nume.

    Sortarea trebuie sa fie identica cu StringComparer.Ordinal din C#, altfel tabelul
    iese in ordini diferite si crosscheck-ul raporteaza diferente care nu exista.
    """
    hits = [os.path.join(dirpath, f) for f in os.listdir(dirpath)
            if os.path.isfile(os.path.join(dirpath, f)) and glob_match(f, pattern)]
    return sorted(hits, key=lambda p: os.path.basename(p))


def block_mean(a, k):
    if k <= 1:
        return a
    h, w = a.shape
    a = a[:h // k * k, :w // k * k]
    return a.reshape(h // k, k, w // k, k).mean(axis=(1, 3))


def prepare(data, target_long=1200):
    """Block-mean 2x2 (omoara CFA) + downsample la ~target_long."""
    img = block_mean(data, 2)
    k = max(1, int(max(img.shape) / target_long))
    img = block_mean(img, k)
    return img


# ---------------------------------------------------------------- geometrie
def fit_center(img):
    """Paraboloid fitat pe log(I). Doua lucruri diferite, din acelasi fit:

    - varful (vertexul) = centrul de iluminare. E o POZITIE, deci NU e scale-invariant:
      vertexul e ~ -b/(2d), raportul dintre gradient si curbura. Pe un obiectiv aproape
      fara vignetare (d mic) o rampa modesta il arunca oricat de departe, iar cand iese
      din cadru garda de mai jos cade pe centrul senzorului - deci metrica e si
      NEMONOTONA: peste un prag, cu cat defectul e mai mare, cu atat offsetul raportat e
      mai mic. De asta offsetul e doar informativ si nu mai da verdict.
    - inclinarea = amplitudinea varf-la-varf a termenului LINIAR peste cadru, adica
      numaratorul de mai sus luat singur. E o fractie, deci scale-invarianta si direct
      comparabila cu scara de reziduu. Un camp radial genuin decentrat produce si el un
      termen liniar (~ offset x curbura), deci prinde si decentrarea reala, ponderata cu
      cat costa ea fotometric.

    Termenul incrucisat f (astigmatism) e lasat deliberat afara: nu e o inclinare, iar
    reziduul dupa modelul radial il prinde oricum.
    """
    h, w = img.shape
    y, x = np.mgrid[0:h, 0:w].astype(np.float64)
    xn, yn = (x - w / 2) / (w / 2), (y - h / 2) / (h / 2)
    v = np.log(np.maximum(img, 1e-12))
    s = max(1, min(h, w) // 200)
    xs, ys, vs = xn[::s, ::s].ravel(), yn[::s, ::s].ravel(), v[::s, ::s].ravel()
    A = np.column_stack([np.ones_like(xs), xs, ys, xs**2, ys**2, xs * ys])
    coef, *_ = np.linalg.lstsq(A, vs, rcond=None)
    _, b, c, d, e, f = coef
    # |b| + |c| e valoarea termenului liniar in colt (unde xn = yn = 1); sinh face
    # conversia exacta din log in intensitate, nu aproximarea liniara 2*(|b| + |c|).
    tilt = float(2 * np.sinh(abs(b) + abs(c)) * 100)
    M = np.array([[2 * d, f], [f, 2 * e]])
    try:
        sol = np.linalg.solve(M, np.array([-b, -c]))
    except np.linalg.LinAlgError:
        return w / 2.0, h / 2.0, tilt, False
    cx, cy = sol[0] * (w / 2) + w / 2, sol[1] * (h / 2) + h / 2
    if not (0 < cx < w and 0 < cy < h):   # fit degenerat -> fallback
        return w / 2.0, h / 2.0, tilt, False
    return cx, cy, tilt, True


def radial_model(img, cx, cy, nbins=80, pct=50):
    """Profil radial robust + model 2D reconstruit prin interpolare."""
    h, w = img.shape
    y, x = np.mgrid[0:h, 0:w]
    r = np.hypot(x - cx, y - cy)
    rmax = r.max()
    idx = np.minimum((r / rmax * nbins).astype(int), nbins - 1)
    centers, prof = [], []
    for i in range(nbins):
        m = idx == i
        if m.sum() >= 8:
            centers.append((i + 0.5) / nbins * rmax)
            prof.append(np.percentile(img[m], pct))
    centers, prof = np.array(centers), np.array(prof)
    model = np.interp(r.ravel(), centers, prof).reshape(img.shape)
    return r, centers, prof, model


# ---------------------------------------------------------------- metrici
def corner_stats(img, frac=0.05):
    h, w = img.shape
    bh, bw = max(4, int(h * frac)), max(4, int(w * frac))
    boxes = {
        'stanga-sus':   img[:bh, :bw],
        'dreapta-sus':  img[:bh, -bw:],
        'stanga-jos':   img[-bh:, :bw],
        'dreapta-jos':  img[-bh:, -bw:],
    }
    vals = {k: float(np.median(v)) for k, v in boxes.items()}
    ctr = float(np.median(img[h // 2 - bh:h // 2 + bh, w // 2 - bw:w // 2 + bw]))
    return vals, ctr


def azimuthal_asymmetry(img, cx, cy, nsect=12, nrings=10):
    """Ptp intre sectoare, per inel. Doar in raza inscrisa (toate azimuturile
    sunt esantionate acolo; in colturi nu, deci comparatia ar fi invalida)."""
    h, w = img.shape
    y, x = np.mgrid[0:h, 0:w]
    dx, dy = x - cx, y - cy
    r = np.hypot(dx, dy)
    th = np.arctan2(dy, dx)
    r_ins = min(cx, cy, w - cx, h - cy)
    edges = np.linspace(0.15 * r_ins, r_ins, nrings + 1)
    sect = np.minimum(((th + np.pi) / (2 * np.pi) * nsect).astype(int), nsect - 1)
    out = []
    for i in range(nrings):
        ring = (r >= edges[i]) & (r < edges[i + 1])
        meds = [np.median(img[ring & (sect == s)])
                for s in range(nsect) if (ring & (sect == s)).sum() >= 8]
        if len(meds) >= nsect - 1:
            meds = np.array(meds)
            out.append(((edges[i] + edges[i + 1]) / 2 / r_ins,
                        float((meds.max() - meds.min()) / np.median(meds) * 100)))
    return out


def robust_ptp(a, lo=2, hi=98):
    return float(np.percentile(a, hi) - np.percentile(a, lo))


def large_scale_ptp(resid, cx, cy, r_ins, ntiles=40):
    """Ptp al structurii de scala mare, izolata de zgomot.

    Mediem reziduul pe tile-uri: zgomotul scade cu sqrt(N_px/tile), structura
    supravietuieste. Fara asta, metrica masoara zgomot, nu erori.
    """
    h, w = resid.shape
    k = max(2, int(w / ntiles))
    t = block_mean(resid, k)
    th_, tw = t.shape
    y, x = np.mgrid[0:th_, 0:tw]
    tr = np.hypot((x + 0.5) * k - cx, (y + 0.5) * k - cy)
    inside = tr < r_ins
    if inside.sum() < 10:
        return float('nan')
    return robust_ptp(t[inside], 5, 95)


def verdict(val, ok, bad):
    return "OK" if val < ok else ("SUSPECT" if val < bad else "PROBLEMA")


# numele celor patru benzi de reziduu, in ordinea severitatii
RESID_BANDS = ["PERFECT", "BUN", "ACCEPTABIL", "REFA"]


def _band(rng, meaning):
    """O linie din scara de reziduu, cu descrierile aliniate."""
    print(f"    {rng:<13}{meaning}")


def resid_verdict(val):
    """Scara de reziduu din modul structura. Ca si verdict(), NaN cade in banda cea mai
    proasta: toate comparatiile ies false, deci un input degenerat iese "REFA"."""
    if val < TH_RESID_PERFECT:
        return "PERFECT"
    if val < TH_RESID_USABLE:
        return "BUN"
    if val < TH_RESID_CHECK:
        return "ACCEPTABIL"
    return "REFA"


_SEVERITY = {"OK": 0, "PERFECT": 0, "BUN": 1,
             "SUSPECT": 2, "ACCEPTABIL": 2, "PROBLEMA": 3, "REFA": 3}


def severity(verdict_name):
    """Severitate 0-3, unificata peste ambele scari, ca sa se poata lua "cel mai prost"
    dintr-un amestec de verdicte. Orice necunoscut e tratat ca cel mai rau."""
    return _SEVERITY.get(verdict_name, 3)


# ---------------------------------------------------------------- moduri
def mode_structure(path, plot_path):
    raw = load_image(path)
    img = prepare(raw)
    h, w = img.shape

    print(f"\n=== STRUCTURA: {path} ===")
    print(f"    dimensiune analiza : {w} x {h} (dupa 2x2 + downsample)")
    print(f"    median / max ADU   : {np.median(raw):.0f} / {raw.max():.0f}")

    # ATENTIE: modelul si asimetria se calculeaza in jurul centrului SENZORULUI,
    # nu al centrului fitat. Axa optica e la centrul senzorului prin constructie
    # (obiectivul e centrat pe montura). Daca ai centra pe varful de iluminare,
    # fitul ar absorbi exact gradientul liniar pe care il cauti: un gradient
    # peste vignetare cvasi-patratica doar muta varful. Testul s-ar autoanula.
    cx, cy = w / 2.0, h / 2.0
    # Din acelasi fit ies doua lucruri: inclinarea, care e un test, si offsetul varfului,
    # care e doar informativ - nu e scale-invariant, si peste un prag nici macar monoton.
    fx, fy, tilt, centroid_valid = fit_center(img)
    off = np.hypot(fx - cx, fy - cy) / np.hypot(cx, cy) * 100

    cor, ctr = corner_stats(img)
    cvals = np.array(list(cor.values()))
    vign_stops = np.log2(ctr / cvals.mean())
    spread = (cvals.max() - cvals.min()) / cvals.mean() * 100

    r, rc, prof, model = radial_model(img, cx, cy)
    resid = img / np.maximum(model, 1e-12)
    r_ins = min(cx, cy, w - cx, h - cy)
    res_ptp = large_scale_ptp(resid, cx, cy, r_ins) * 100

    az = azimuthal_asymmetry(img, cx, cy)
    az_max = max(v for _, v in az) if az else float('nan')

    # Cele doua marimi care NU sunt teste: vignetarea, care e o proprietate a
    # obiectivului, si offsetul centrului de iluminare, care a fost un test pana in 1.2.0.
    print(f"\n  INFORMATIV (nu sunt teste)")
    print(f"    vignetare centru->colt  : {vign_stops:.2f} stopuri "
          f"(colt = {cvals.mean()/ctr*100:.0f}% din centru)")
    if centroid_valid:
        print(f"    offset centru iluminare : {off:5.2f} %")
        print(f"        varf iluminare = ({fx:.0f}, {fy:.0f})  vs  centru senzor = ({cx:.0f}, {cy:.0f})")
    else:
        print("    offset centru iluminare : nedeterminat (varf in afara cadrului sau fit degenerat)")
    print("        E raportul dintre gradient si curbura, deci nu e scale-invariant: pe un")
    print("        obiectiv aproape fara vignetare creste mult fara ca eroarea sa creasca,")
    print('        iar peste un prag cade inapoi spre 0. Amplitudinea o da "inclinare".')

    # Toate patru masoara aceeasi abatere de la simetria radiala, din unghiuri diferite,
    # si toate patru sunt amplitudini fractionare in procente, deci impart scara de
    # reziduu. Offsetul centrului masoara altceva, o pozitie, si nu mai da verdict.
    v_corner = resid_verdict(spread)
    v_azim = resid_verdict(az_max)
    v_resid = resid_verdict(res_ptp)
    v_tilt = resid_verdict(tilt)

    print(f"\n  TESTE")
    print(f"    imprastiere 4 colturi   : {spread:5.2f} %   [{v_corner}]")
    for k, v in cor.items():
        print(f"        {k:<14}: {v/cvals.mean()*100:6.2f} % din media colturilor")
    print(f"    asimetrie azimutala max : {az_max:5.2f} %   [{v_azim}]")
    print(f"    reziduu dupa model rad. : {res_ptp:5.2f} %   [{v_resid}]  <- CEL MAI IMPORTANT")
    print(f"    inclinare (gradient)    : {tilt:5.2f} %   [{v_tilt}]")

    # Scara comuna a celor patru teste de abatere. Nu se aplica offsetului centrului.
    print(f"\n  SCARA celor patru teste de abatere de mai sus")
    _band(f"sub {TH_RESID_PERFECT:.0f} %", "calitate perfecta")
    _band(f"{TH_RESID_PERFECT:.0f} - {TH_RESID_USABLE:.0f} %", "utilizabil; background extraction scoate ce ramane")
    _band(f"{TH_RESID_USABLE:.0f} - {TH_RESID_CHECK:.0f} %", "utilizabil, dar confirma cu --light inainte de un stretch dur")
    _band(f"peste {TH_RESID_CHECK:.0f} %", "refa flat-urile")
    print('    Cel mai important e "reziduu dupa model rad.": masoara direct cat din')
    print("    cadru NU se explica printr-un model pur radial, adica exact eroarea de")
    print("    flat. Imprastierea colturilor, asimetria azimutala si inclinarea sunt")
    print("    vederi partiale ale aceleiasi abateri.")

    # verdictul general e cel mai prost dintre cele patru, exprimat in vocabularul
    # benzilor de reziduu
    worst = RESID_BANDS[max(severity(v) for v in (v_corner, v_azim, v_resid, v_tilt))]
    print(f"\n  VERDICT: {worst}")
    if worst == "PERFECT":
        print("    Abaterea de la simetria radiala e sub zgomot. Flat-ul e curat.")
    elif worst == "BUN":
        print("    Utilizabil ca atare. Ce ramane sta in intervalul pe care background")
        print("    extraction il scoate fara efort.")
    elif worst == "ACCEPTABIL":
        print("    Utilizabil, dar ruleaza modul --light pe un light calibrat inainte de")
        print("    un stretch dur, ca sa confirmi ca flat-ul chiar corecteaza.")
    else:
        print("    Abatere prea mare de la simetria radiala => sursa neuniforma, light")
        print("    leak, camera inclinata, sau ambient. NU e vignetare. Refa flat-urile.")
        print("    Vezi harta reziduala pentru forma problemei.")

    make_plot(plot_path, img, resid, rc, prof, az, cx, cy, r_ins, path, "reziduu / model radial")
    return worst


def mode_ratio(path_a, path_b, plot_path):
    a, b = prepare(load_image(path_a)), prepare(load_image(path_b))
    if a.shape != b.shape:
        sys.exit("Cele doua imagini au dimensiuni diferite.")
    ratio = a / np.maximum(b, 1e-12)
    ratio /= np.median(ratio)
    h, w = ratio.shape

    print(f"\n=== TEST ROTATIE 180: {path_a}  /  {path_b} ===")
    print("    Vignetarea si praful sunt fixe pe senzor -> se anuleaza in raport.")
    print("    Orice gradient extern se inverseaza -> apare aici, la ~2x amplitudine.")

    ptp = large_scale_ptp(ratio, w / 2, h / 2, np.hypot(w, h)) * 100
    cor, ctr = corner_stats(ratio)
    cvals = np.array(list(cor.values()))
    spread = (cvals.max() - cvals.min()) / cvals.mean() * 100

    print(f"\n    structura in raport A/B : {ptp:5.2f} %   [{verdict(ptp, TH_RATIO_OK, TH_RATIO_BAD)}]")
    print(f"    imprastiere 4 colturi   : {spread:5.2f} %   [{verdict(spread, TH_RATIO_OK, TH_RATIO_BAD)}]")
    print(f"    (eroarea reala in fiecare master e ~jumatate din cifrele astea)")

    worst = max(verdict(ptp, TH_RATIO_OK, TH_RATIO_BAD),
                verdict(spread, TH_RATIO_OK, TH_RATIO_BAD),
                key=lambda s: {"OK": 0, "SUSPECT": 1, "PROBLEMA": 2}[s])
    print(f"\n  VERDICT: {worst}")
    if worst == "OK":
        print("    Setup-ul de flats e curat. Poti construi biblioteca pe el.")
    else:
        print("    Setup-ul contamineaza flat-ul. Repara sursa inainte de biblioteca.")

    r, rc, prof, _ = radial_model(ratio, w / 2, h / 2)
    az = azimuthal_asymmetry(ratio, w / 2, h / 2)
    make_plot(plot_path, ratio, ratio, rc, prof, az, w / 2, h / 2,
              min(w, h) / 2, f"{path_a} / {path_b}", "raport A/B")
    return worst


def tile_background(img, ntiles=40, pct=15):
    """Fond local per tile. Percentila MICA in interiorul fiecarui tile ->
    robust la stele si nebulozitate (ambele adauga semnal pozitiv).
    Local, nu global: un gradient de cer e cvasi-constant pe un tile mic."""
    h, w = img.shape
    k = max(4, int(w / ntiles))
    hh, ww = h // k, w // k
    t = img[:hh * k, :ww * k].reshape(hh, k, ww, k).transpose(0, 2, 1, 3).reshape(hh, ww, -1)
    bg = np.percentile(t, pct, axis=2)
    y, x = np.mgrid[0:hh, 0:ww]
    return bg, (x + 0.5) * k, (y + 0.5) * k


def analyze_light(img):
    """Fitul plan + radial pe fondul din tile-uri. Pur: raster -> cifrele.

    Extras din mode_light ca sa poata fi refolosit de rularea pe serie, exact ca
    LightAnalyzer.Analyze in C#. Nu tipareste nimic.
    """
    h, w = img.shape
    cx, cy = w / 2.0, h / 2.0
    r_corner = np.hypot(cx, cy)

    bg, bx, by = tile_background(img)

    # Fit simultan: fond = plan + componenta radiala.
    #   plan (a + b*x + c*y)  absoarbe gradientul de poluare luminoasa
    #   r^2 si r^4            capteaza vignetare reziduala (eroare de flat)
    # De asta testul merge si pe cer poluat: cele doua au forme diferite si
    # se separa.
    #   DAR NUMAI PE CAMP INGUST. Separarea tine cat timp cupola de poluare e
    #   cvasi-liniara peste cadru. Pe camp larg ea se curbeaza in interiorul
    #   cadrului, iar partea radial simetrica a curburii are exact forma
    #   vignetarii - si atunci nimic din ce e scris aici nu le mai deosebeste.
    #   Vezi light_suitability, care opreste modul inainte sa ajunga aici.
    # ATENTIE: NU folosi percentile pe inele circulare - p20 pe un inel prinde
    # partea intunecata a gradientului de cer si o raporteaza ca falsa
    # vignetare. (Am facut fix greseala asta prima data.)
    X = (bx - cx).ravel() / r_corner
    Y = (by - cy).ravel() / r_corner
    R2 = X**2 + Y**2
    V = bg.ravel().astype(np.float64)
    A = np.column_stack([np.ones_like(X), X, Y, R2, R2**2])

    keep = np.ones_like(V, bool)
    for _ in range(4):                       # sigma-clip: scot nebulozitatea
        coef, *_ = np.linalg.lstsq(A[keep], V[keep], rcond=None)
        res = V - A @ coef
        s = 1.4826 * np.median(np.abs(res[keep] - np.median(res[keep])))
        keep = res < 2.5 * max(s, 1e-9)

    a, b, c, d, e = coef
    radial_at_corner = d + e                 # R2 = 1 in colt
    rad_pct = -radial_at_corner / a * 100    # + = fondul cade spre margini
    plane_pct = np.hypot(b, c) / a * 100

    return dict(a=a, b=b, c=c, d=d, e=e,
                rad_pct=rad_pct, plane_pct=plane_pct,
                kept=int(keep.sum()), total=int(keep.size),
                verdict=verdict(abs(rad_pct), TH_LIGHT_RAD_OK, TH_LIGHT_RAD_BAD),
                radial_only=(d * R2 + e * R2**2).reshape(bg.shape) / a,
                bg=bg, cx=cx, cy=cy, r_corner=r_corner, img_shape=(h, w))


def print_light_diagnosis(rad_pct):
    """Diagnosticul din semnul componentei radiale.

    Separat fiindca il foloseste si raportul pe serie: aceeasi cifra, aceeasi citire,
    deci acelasi text - daca ar fi duplicat, cele doua ar diverge la prima reformulare.
    """
    if abs(rad_pct) < TH_LIGHT_RAD_OK:
        print("    Fondul n-are componenta radiala. Flat-ul face treaba.")
        print("    Asta e testul care conteaza - toate celelalte sunt proxy pentru el.")
    elif rad_pct > 0:
        print("    Fondul cade spre margini => SUB-corectie. Flat-ul nu prinde toata")
        print("    vignetarea: alta diafragma decat la lights, sau flat contaminat.")
    else:
        print("    Fondul creste spre margini => SUPRA-corectie. Cel mai probabil")
        print("    flat-darks lipsa/gresite: imparti la un flat care contine offset,")
        print("    iar eroarea se amplifica exact in colturi.")


def light_plot(res, plot_path, path):
    d, e = res['d'], res['e']
    bg, r_corner = res['bg'], res['r_corner']
    h, w = res['img_shape']
    prof_r = np.linspace(0, 1, 40)
    make_plot(plot_path, bg, res['radial_only'] + 1.0,
              prof_r * r_corner, 1 + d * prof_r**2 + e * prof_r**4,
              [], res['cx'] / (w / bg.shape[1]), res['cy'] / (h / bg.shape[0]),
              min(bg.shape) / 2, path, "componenta radiala extrasa (fara plan)")


def mode_light(path, plot_path, fov=None):
    raw, header = load_image_with_header(path)

    # Garda inainte de orice calcul: pe un camp prea larg cifra n-ar insemna nimic,
    # deci nu se produce deloc, in loc sa se produca si sa se explice.
    s = light_suitability(header, fov)
    if not s.can_run:
        print_too_wide(s, path)
        return

    res = analyze_light(prepare(raw))

    print(f"\n=== TEST END-TO-END: {path} ===")
    print("    Astept un stack CALIBRAT cu flat, dar FARA background extraction.")
    print_suitability(s)

    print(f"\n    tile-uri folosite       : {res['kept']} / {res['total']} "
          f"(restul = nebulozitate/stele, respinse)")
    print(f"    gradient de cer (plan)  : {res['plane_pct']:5.2f} %  <- poluare luminoasa, IGNORA"
          + ("  (mare!)" if res['plane_pct'] > TH_LIGHT_PLANE_WARN else ""))
    print(f"    componenta radiala      : {res['rad_pct']:+6.2f} %   "
          f"[{res['verdict']}]  <- ASTA e flat-ul")

    print("\n  INTERPRETARE")
    print_light_diagnosis(res['rad_pct'])

    # Un cadru singur amesteca eroarea de flat cu termenul radial al cerului din acel
    # moment, iar al doilea se schimba in timpul sesiunii. Pe o serie reala cifra a
    # baleiat 55 de puncte in 81 de minute, deci merita spus la fiecare rulare.
    print("\n    Cifra asta e dintr-un singur cadru, deci contine si cerul din acel moment.")
    print("    Da-i folderul in loc de fisier ca sa rulez pe toata seria si sa mediez:")
    print("        flatcheck process/ --light")

    light_plot(res, plot_path, path)
    return res['verdict']


# ---------------------------------------------------------------- serie de cadre
# DE CE UN CADRU SINGUR NU AJUNGE. Componenta radiala raportata de --light e suma a
# doua lucruri: eroarea de flat, care e ACEEASI pe toate cadrele prin constructie
# (acelasi master flat s-a impartit din fiecare), si un termen radial al cerului, care
# se schimba in timpul sesiunii. Pe o serie reala (32 de cadre, 81 de minute, Calea
# Lactee la 20 mm) cifra a baleiat neted de la +3.13 % prin -19.20 % pana la +35.73 %,
# cu doar 1.36 puncte intre cadre consecutive. Tot ce se misca asa e cer.
#
# Media pe serie estimeaza eroarea de flat DOAR daca termenul de cer se mediaza la
# zero. Nu e garantat, deci nu se presupune - se verifica prin trei conditii, si daca
# vreuna pica verdictul e NEDETERMINAT in loc de un numar care arata increzator.
#
# A doua conditie e cea interesanta: daca "eroarea de flat" URMARESTE gradientul de cer
# peste serie, ce se masoara e cerul, fiindca flat-ul nu se poate schimba de la un cadru
# la altul. Pe seria de mai sus corelatia a iesit -0.94.
#
# CE NU FACE: nu extrapoleaza la gradient de cer zero. Regresia rad_pct ~ plane_pct pe
# seria reala da intercept +122 %, extrapolare mult in afara datelor (gradientul
# observat sta intre 49 si 88 %). Corelatia e buna ca diagnostic si inutila ca
# estimator; nu o transforma in unul.
UNDETERMINED = "NEDETERMINAT"


def pearson(a, b):
    """Corelatia Pearson. NaN daca vreuna din serii e constanta - caz in care
    intrebarea 'se misca impreuna?' nu are raspuns, nu are raspunsul 'nu'."""
    a, b = np.asarray(a, float), np.asarray(b, float)
    if a.size < 2 or b.size != a.size:
        return float('nan')
    da, db = a - a.mean(), b - b.mean()
    den = np.sqrt((da**2).sum() * (db**2).sum())
    return float('nan') if den <= 0 else float((da * db).sum() / den)


def combine_light_series(frames):
    """Pur: lista de (nume, rezultat) -> cifrele pe serie si verdictul."""
    rad = np.array([f[1]['rad_pct'] for f in frames], float)
    plane = np.array([f[1]['plane_pct'] for f in frames], float)
    n = rad.size

    mean = float(rad.mean())
    std = float(rad.std(ddof=1)) if n >= 2 else float('nan')
    sem = std / np.sqrt(n) if n >= 2 else float('nan')
    sky_span = float(plane.max() - plane.min())
    corr_applies = sky_span > TH_SERIES_SKY_SPAN_MIN
    corr = pearson(rad, plane)

    # Comparatiile sunt scrise ca "not (x < prag)" si nu ca "x >= prag" intentionat: asa
    # NaN cade pe partea proasta, la fel ca in verdict(). Un sem NaN inseamna ca nu s-a
    # putut calcula imprastierea, ceea ce e motiv de NEDETERMINAT, nu de trecere tacuta.
    reasons = []
    if n < TH_SERIES_MIN_FRAMES:
        reasons.append(f"prea putine cadre ({n}, minim {TH_SERIES_MIN_FRAMES})")
    if not (sem < TH_SERIES_SEM_MAX):
        reasons.append("sem")
    if corr_applies and not (abs(corr) < TH_SERIES_SKY_CORR):
        reasons.append("corelatie")

    return dict(names=[f[0] for f in frames], frames=[f[1] for f in frames],
                count=n, mean=mean, lo=float(rad.min()), hi=float(rad.max()),
                std=std, sem=sem, corr=corr, sky_span=sky_span,
                corr_applies=corr_applies, reasons=reasons,
                verdict=UNDETERMINED if reasons
                else verdict(abs(mean), TH_LIGHT_RAD_OK, TH_LIGHT_RAD_BAD))


def _trim(name):
    """Numele lungi ar rupe coloanele; se taie de la stanga, pastrand coada, fiindca
    acolo sta numarul cadrului."""
    return name if len(name) <= 30 else "..." + name[-27:]


def _print_reason(r, reason):
    if reason == "sem":
        print(f"      - eroarea mediei ({r['sem']:.2f}) depaseste chiar pragul de OK ({TH_LIGHT_RAD_OK:.2f}),")
        print("        deci seria nu rezolva flat-ul la precizia la care se pronunta")
        print("        pragurile. Un verdict aici ar fi o parere, nu o masuratoare.")
    elif reason == "corelatie":
        print(f"      - componenta radiala urmareste gradientul de cer (r = {r['corr']:.2f}")
        print(f"        pe un interval de {r['sky_span']:.0f} puncte de gradient), semn ca ce se")
        print("        masoara e cerul.")
    else:
        print(f"      - {reason}.")


def mode_light_series(dirpath, pattern, plot_path, fov=None):
    files = find_series(dirpath, pattern)
    if not files:
        raise ValueError(f"Niciun fisier care sa se potriveasca cu '{pattern}' in "
                         f"{dirpath}. Da alt tipar cu --pattern.")

    # Aplicabilitatea se decide pe primul cadru: toate vin din aceeasi sesiune, deci au
    # aceeasi focala si aceeasi scara.
    first_raw, first_header = load_image_with_header(files[0])
    s = light_suitability(first_header, fov)
    if not s.can_run:
        print_too_wide(s, dirpath)
        return

    print(f"\n=== TEST END-TO-END (SERIE): {dirpath} ===")
    print("    Astept cadre CALIBRATE cu flat, FARA background extraction si NEREGISTRATE.")
    print(f"    cadre gasite            : {len(files)}  (tipar {pattern})")
    print_suitability(s)
    print()
    print("    cadru                              radial   gradient cer     tile-uri")

    frames = []
    for i, path in enumerate(files):
        # Cadrele se incarca pe rand si nu se pastreaza: un cadru de 24 MP ocupa sute de
        # MB, deci 32 tinute simultan ar fi de neexecutat.
        raw = first_raw if i == 0 else load_image(path)
        first_raw = None
        res = analyze_light(prepare(raw))
        name = os.path.basename(path)
        print(f"    {_trim(name):<30} {res['rad_pct']:+8.2f} % "
              f"{res['plane_pct']:10.2f} %  {res['kept']:6d}/{res['total']}")
        frames.append((name, res))

    r = combine_light_series(frames)

    print("\n  SINTEZA PE SERIE")
    print(f"    componenta radiala, medie : {r['mean']:+7.2f} %")
    print(f"    imprastiere (min .. max)  : {r['lo']:+7.2f} .. {r['hi']:+.2f} %")
    print(f"    eroarea mediei            : {r['sem']:7.2f} puncte"
          + (f"   <- peste pragul de {TH_SERIES_SEM_MAX:.2f}" if "sem" in r['reasons'] else ""))
    if r['corr_applies']:
        print(f"    corelatie cu grad. de cer : {r['corr']:7.2f}"
              + (f"          <- peste pragul de {TH_SERIES_SKY_CORR:.2f}"
                 if "corelatie" in r['reasons'] else ""))
    else:
        print("    corelatie cu grad. de cer : nu se aplica (gradientul variaza doar "
              f"{r['sky_span']:.1f} puncte)")

    print(f"\n    VERDICT: {r['verdict']}")
    if not r['reasons']:
        print_light_diagnosis(r['mean'])
        print(f"\n    Media pe {r['count']} cadre e estimarea erorii de flat: imprastierea e mica,")
        print("    deci termenul radial al cerului e stabil peste serie si nu o contamineaza.")
    else:
        print("    Media NU se poate citi ca eroare de flat:")
        for reason in r['reasons']:
            _print_reason(r, reason)
        print("\n    Flat-ul e acelasi pe toate cadrele prin constructie - s-a impartit din")
        print("    fiecare. Deci tot ce se misca in timpul seriei e cer, nu flat.")
        print("    Pe date ca astea raspunsul il da testul de rotatie 180 (--ratio), care")
        print("    valideaza flat-ul fara sa implice cerul deloc. Vezi README, sectiunea")
        print("    despre cand se poate folosi --light.")

    # Harta radiala medie peste cadre. Toate trebuie sa aiba aceeasi grila de tile-uri.
    shapes = {f[1]['radial_only'].shape for f in frames}
    if len(shapes) > 1:
        raise ValueError("Cadrele din serie nu au aceeasi dimensiune - nu pot fi mediate. "
                         "Foloseste doar cadre din aceeasi sesiune, necropate.")
    mean_map = np.mean([f[1]['radial_only'] for f in frames], axis=0)
    ref = frames[0][1]
    make_plot(plot_path, ref['bg'], mean_map + 1.0,
              np.linspace(0, 1, 40) * ref['r_corner'], np.ones(40),
              [], ref['bg'].shape[1] / 2, ref['bg'].shape[0] / 2,
              min(ref['bg'].shape) / 2, dirpath,
              "componenta radiala medie pe serie (fara plan)")
    return r['verdict']


# ---------------------------------------------------------------- plot
def make_plot(out, img, resid, rc, prof, az, cx, cy, r_ins, title, resid_label):
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt

    fig, ax = plt.subplots(2, 2, figsize=(12, 8))
    fig.suptitle(title, fontsize=11)

    m = np.percentile(img, 99.5)
    im0 = ax[0, 0].imshow(img / m, cmap='inferno')
    ax[0, 0].plot(cx, cy, 'c+', ms=12)
    ax[0, 0].set_title("imagine (normalizata)")
    plt.colorbar(im0, ax=ax[0, 0], fraction=0.04)

    ax[0, 1].plot(rc / rc.max(), prof / prof.max(), 'k-')
    ax[0, 1].set_title("profil radial")
    ax[0, 1].set_xlabel("raza normalizata")
    ax[0, 1].grid(alpha=0.3)

    lo, hi = np.percentile(resid, [1, 99])
    pad = max(hi - lo, 1e-4) * 0.1
    im2 = ax[1, 0].imshow(resid, cmap='RdBu_r', vmin=lo - pad, vmax=hi + pad)
    ax[1, 0].set_title(f"{resid_label}  (ORICE structura aici = eroare)")
    plt.colorbar(im2, ax=ax[1, 0], fraction=0.04)

    if az:
        ax[1, 1].plot([a for a, _ in az], [v for _, v in az], 'o-')
    ax[1, 1].axhline(TH_RESID_PERFECT, color='g', ls='--', lw=0.8)
    ax[1, 1].axhline(TH_RESID_USABLE, color='r', ls='--', lw=0.8)
    ax[1, 1].set_title("asimetrie azimutala per inel")
    ax[1, 1].set_xlabel("raza / raza inscrisa")
    ax[1, 1].set_ylabel("ptp intre 12 sectoare [%]")
    ax[1, 1].grid(alpha=0.3)

    plt.tight_layout()
    plt.savefig(out, dpi=110)
    print(f"\n    -> harta salvata: {out}")


# ---------------------------------------------------------------- expunere
# Ruleaza pe un SINGUR flat BRUT (NEF direct, sau un flat convertit in Siril dar
# NECALIBRAT). NU pe master - stacking-ul normalizeaza si sterge nivelul absolut.
# Masoara cat de sus e canalul cel mai luminos fata de saturatie si spune in ce
# directie sa modifici expunerea. Inlocuieste histograma de pe camera (care e pe
# JPEG si te minte cu ~2 stopuri fata de RAW).
TH_EXPO_LO, TH_EXPO_HI = 50.0, 60.0   # tinta: canalul cel mai luminos in [50,60]%
TH_EXPO_CLIP = 0.05                   # % pixeli saturati peste care e clipping

def load_raw_for_expo(path):
    """Intoarce (data2D, white, black, exposure_str). Pt NEF foloseste rawpy
    (white/black exacte). Pt FITS auto-detecteaza scala si o anunta."""
    p = path.lower()
    if p.endswith(('.nef', '.cr2', '.cr3', '.arw', '.dng', '.raf')):
        import rawpy
        raw = rawpy.imread(path)
        data = raw.raw_image_visible.astype(np.float64)
        white = float(raw.white_level)
        black = float(np.median(raw.black_level_per_channel))
        exp = None
        try:
            import exifread
            with open(path, 'rb') as f:
                t = exifread.process_file(f, details=False)
            exp = str(t.get('EXIF ExposureTime')) if 'EXIF ExposureTime' in t else None
        except Exception:
            pass
        return data, white, black, exp, "rawpy (exact)"
    # FITS: auto-detect scala din valoarea maxima
    data = load_image(path)
    mx = data.max()
    if mx <= 1.5:
        white = 1.0
    elif mx <= 4096 * 1.05:
        white = 4095.0
    elif mx <= 16384 * 1.05:
        white = 16383.0
    else:
        white = 65535.0
    return data, white, 0.0, None, f"auto-detectat din max={mx:.0f} (foloseste --sat/--black daca e gresit)"


def mode_expo(path, sat_override=None, black_override=None, sky=False):
    data, white, black, exp, src = load_raw_for_expo(path)
    if sat_override is not None:
        white = sat_override
        src = f"--sat={sat_override}"
    if black_override is not None:
        black = black_override

    # canale CFA Bayer (4 pozitii). Verdele clipeaza primul, deci canalul cel mai
    # luminos = cel limitant. Daca cele 4 sunt ~egale, e mono/procesat -> global.
    ch = [data[0::2, 0::2], data[0::2, 1::2], data[1::2, 0::2], data[1::2, 1::2]]
    meds = np.array([np.median(c) for c in ch])
    is_cfa = meds.max() / max(meds.min(), 1e-9) > 1.15
    rng = white - black
    bright = int(np.argmax(meds))

    # CENTRUL cadrului, nu mediana globala. Un flat are prin definitie o distributie
    # larga (asta E vignetarea): centru luminos, colturi intunecate. Mediana globala e
    # trasa in jos de colturi si subestimeaza centrul -> daca urmezi mediana, supraexpui
    # si clipezi centrul. Ce conteaza pt "e bine expus / clipeaza?" e CENTRUL, unde
    # semnalul e maxim si unde satureaza intai. Masor o fereastra centrala 20%x20%.
    def central(chan, frac=0.2):
        h, w = chan.shape
        y0, y1 = int(h * (0.5 - frac / 2)), int(h * (0.5 + frac / 2))
        x0, x1 = int(w * (0.5 - frac / 2)), int(w * (0.5 + frac / 2))
        return chan[y0:y1, x0:x1]

    src_chan = ch[bright] if is_cfa else data
    cen = central(src_chan)
    cen_pct = (np.median(cen) - black) / rng * 100          # metrica de decizie
    glob_pct = (np.median(src_chan) - black) / rng * 100    # informativ
    cen_clip = np.mean(cen >= 0.98 * white) * 100           # clipping unde conteaza

    print(f"\n=== EXPUNERE: {path} ===")
    print(f"    saturatie (white) : {white:.0f}   black : {black:.0f}   sursa: {src}")
    if exp:
        print(f"    timp expunere EXIF: {exp} s")

    if is_cfa:
        pcts = (meds - black) / rng * 100
        names = ['CFA[0,0]', 'CFA[0,1]', 'CFA[1,0]', 'CFA[1,1]']
        print("    nivel median GLOBAL per canal CFA (informativ; nu stiu care e R/G/B):")
        for n, pc in zip(names, pcts):
            marker = "  <- cel mai luminos" if n == names[bright] else ""
            print(f"        {n}: {pc:5.1f}%{marker}")
    else:
        print("    imagine mono/procesata (fara CFA)")

    print(f"    canal cel mai luminos, mediana GLOBALA : {glob_pct:5.1f}%  (trasa in jos de colturi)")
    print(f"    canal cel mai luminos, CENTRU 20%      : {cen_pct:5.1f}%  <- ASTA decide expunerea")
    print(f"    clipping in centru (>=98% white)       : {cen_clip:.3f}%")

    # SANITY: --expo are nevoie de un flat BRUT necalibrat (mozaic Bayer intact, scala
    # ADU originala). Doua semne ca fisierul NU e brut:
    #   1. nivel non-fizic (<1% sau negativ) -> flat-dark deja scazut / normalizat
    #   2. cele 4 pozitii CFA quasi-identice -> debayerizat (mozaicul nu mai exista)
    cfa_spread = meds.max() / max(meds.min(), 1e-9)
    if cen_pct < 1.0 or (is_cfa == False and cfa_spread < 1.02) or cfa_spread < 1.02:
        print()
        print("    !! FISIERUL NU PARE UN FLAT BRUT !!")
        if cen_pct < 1.0:
            print(f"    Nivelul e ~{cen_pct:.1f}% (langa zero/negativ) -> flat-dark deja scazut,")
            print("    sau imagine normalizata. --expo are nevoie de nivelul ADU original.")
        if cfa_spread < 1.02:
            print(f"    Cele 4 canale CFA sunt quasi-identice (spread {cfa_spread:.3f}) -> fisierul")
            print("    e DEBAYERIZAT. --expo are nevoie de mozaicul Bayer intact.")
        print()
        print("    Cauza tipica: ai deschis NEF-ul in Siril si ai dat Save (asta debayerizeaza).")
        print("    SOLUTII:")
        print("      - ruleaza direct pe NEF (Python citeste brutul exact):")
        print("          python3 flatcheck.py flat.NEF --expo")
        print("      - SAU produ un FITS brut cu tab-ul Conversion din Siril (NU Open+Save),")
        print("        care pastreaza mozaicul CFA nedebayerizat.")
        return

    # verdict pe CENTRU. Tinta: centru la 50-65% (mai sus decat mediana globala, pt ca
    # centrul e varful distributiei; 50-65% centru ~ flat bine expus fara clipping).
    print()
    def suggest_shutter(stops):
        if not exp or '/' not in exp:
            return ""
        try:
            num, den = exp.split('/')
            cur = float(num) / float(den)
            new = cur * (2 ** stops)
            if new < 1:
                return f"  (ex: {exp}s -> ~1/{max(1, round(1/new))}s)"
            return f"  (ex: {exp}s -> ~{new:.1f}s)"
        except Exception:
            return ""

    CEN_LO, CEN_HI = 50.0, 65.0   # tinta pt CENTRU (panou, sursa stabila)
    if sky:
        # SKY FLAT: cerul se intuneca continuu si e plin de stele slabe. Nu impinge in
        # sus - un nivel modest e corect, iar SNR-ul masterului vine din NUMARUL de
        # cadre, nu din a urca un cadru la 55%. Tinta pt canalul dominant: 15-50%.
        # Peste ~50% incepi sa risti stele vizibile si lucrezi contra ceasului.
        SKY_LO, SKY_HI = 15.0, 50.0
        print("    [mod --sky: praguri pt flats pe cer, nu pe panou]")
        if cen_clip > TH_EXPO_CLIP or cen_pct > 60:
            print(f"    VERDICT: PREA SUS" + (" (CLIPPING in centru!)" if cen_clip > TH_EXPO_CLIP else " (risc stele in cadru)"))
            print(f"    Scade expunerea.{suggest_shutter(np.log2(30.0 / cen_pct) if cen_pct > 0 else -1)}")
        elif cen_pct < SKY_LO:
            stops = np.log2(30.0 / cen_pct)
            print(f"    VERDICT: PREA JOS pentru sky flat")
            print(f"    Creste cu ~{stops:.1f} stopuri (dar lucreaza repede, cerul se stinge).{suggest_shutter(stops)}")
        elif cen_pct <= SKY_HI:
            print(f"    VERDICT: OK pentru sky flat  (centru {SKY_LO:.0f}-{SKY_HI:.0f}%)")
            print("    La sky flat, nivelul modest e corect. SNR-ul vine din numarul de cadre,")
            print("    nu din a urca expunerea (ai risca stele in cadru). NU forta mai sus.")
        else:
            print(f"    VERDICT: cam sus, dar acceptabil")
            print("    Sub 60% e ok; peste, risti stele. Nu urca mai mult.")
    elif cen_clip > TH_EXPO_CLIP or cen_pct > 80:
        stops = np.log2(57.0 / cen_pct) if cen_pct > 0 else -1
        print(f"    VERDICT: PREA SUS" + (" (CLIPPING in centru!)" if cen_clip > TH_EXPO_CLIP else ""))
        print(f"    Scade expunerea cu ~{-stops:.1f} stopuri.{suggest_shutter(stops)}")
        if cen_clip > TH_EXPO_CLIP:
            print("    Centrul clipeaza -> pierzi info de vignetare exact unde semnalul e maxim.")
    elif cen_pct < 30:
        stops = np.log2(57.0 / cen_pct)
        print(f"    VERDICT: PREA JOS")
        print(f"    Creste expunerea cu ~{stops:.1f} stopuri.{suggest_shutter(stops)}")
        print("    Sub ~30% in centru masterul iese zgomotos (zgomot fixed-pattern in flat).")
    elif cen_pct < CEN_LO:
        stops = np.log2(57.0 / cen_pct)
        print(f"    VERDICT: ACCEPTABIL, dar cam jos")
        print(f"    Ai putea creste cu ~{stops:.1f} stopuri.{suggest_shutter(stops)}")
    elif cen_pct <= CEN_HI:
        print(f"    VERDICT: OK  (centrul in tinta {CEN_LO:.0f}-{CEN_HI:.0f}%)")
    else:
        stops = np.log2(57.0 / cen_pct)
        print(f"    VERDICT: ACCEPTABIL, dar cam sus")
        print(f"    Ai putea scadea cu ~{-stops:.1f} stopuri.{suggest_shutter(stops)}")


# ---------------------------------------------------------------- main
def main():
    p = argparse.ArgumentParser(
        description="Validare de master flats.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__warranty__)
    p.add_argument("image", help="master flat .fit; cu --light, un light calibrat "
                                 "sau un FOLDER, pentru rulare pe toata seria")
    p.add_argument("--ratio", metavar="B.fit", help="testul de rotatie 180: image / B")
    p.add_argument("--light", action="store_true",
                   help="test end-to-end pe un light calibrat fara BGE")
    p.add_argument("--expo", action="store_true",
                   help="verifica expunerea unui flat BRUT (NEF sau flat necalibrat), nu master")
    p.add_argument("--sky", action="store_true",
                   help="cu --expo: praguri pt flats pe cer (blue hour), nu pe panou")
    p.add_argument("--sat", type=float, default=None,
                   help="nivel de saturatie (white), daca auto-detectia greseste")
    p.add_argument("--black", type=float, default=None, help="nivel black, pt --expo pe FITS")
    # Tiparul implicit e conventia Siril pentru cadre CALIBRATE dar NEREGISTRATE.
    # "r_pp_light_*" nu se potriveste, si asta e intentionat: registrarea transforma
    # geometric cadrul, deci muta eroarea de flat - fixa pe senzor - fata de centrul in
    # jurul caruia se fiteaza modelul radial.
    p.add_argument("--pattern", default="pp_light_*.fit",
                   help="cu --light pe un folder: ce fisiere sa ia (implicit pp_light_*.fit)")
    p.add_argument("--fov", type=float, default=None,
                   help="diagonala campului in grade, daca headerul nu o da")
    p.add_argument("-o", "--out", default="flatcheck.png", help="fisier PNG de iesire")
    # Versiunea si copyright-ul, pe primele doua linii ale oricarei rulari. Trebuie sa
    # fie identice cu ce scrie C#-ul (VersionInfo.Banner / .Copyright) - crosscheck.py
    # compara iesirile linie cu linie.
    # Se tipareste INAINTE de parse_args ca sa apara si la --help, unde argparse iese
    # singur, si la argumente gresite - exact ca in C#.
    print(f"flatcheck {__version__}")
    print(__copyright__)

    a = p.parse_args()

    # Un folder inseamna "ruleaza pe serie", si asta are sens doar la --light.
    # Celelalte moduri lucreaza pe un master sau pe un cadru anume.
    if os.path.isdir(a.image) and not a.light:
        sys.exit("EROARE: un folder se poate da doar cu --light, pentru rularea pe serie. "
                 "Celelalte moduri asteapta un fisier.")

    if a.expo:
        mode_expo(a.image, a.sat, a.black, a.sky)
    elif a.ratio:
        mode_ratio(a.image, a.ratio, a.out)
    elif a.light:
        if os.path.isdir(a.image):
            mode_light_series(a.image, a.pattern, a.out, a.fov)
        else:
            mode_light(a.image, a.out, a.fov)
    else:
        mode_structure(a.image, a.out)
    print()


if __name__ == "__main__":
    main()
