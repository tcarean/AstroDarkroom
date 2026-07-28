#!/usr/bin/env python3
"""
flatcheck.py - diagnostic cantitativ pentru master flats
=========================================================

CITESTE ASTA INTAI: CE FISIER II DAI
------------------------------------
Cea mai probabila cauza de rezultat gresit nu e in cod, e in fisierul ales.

Scriptul are nevoie de flat-uri din care s-a SCAZUT DEJA flat-dark-ul
(offset-ul). Dupa ce ruleaza scriptul Siril (OSC_Preprocessing sau
OSC_Preprocessing_WithoutDark), fisierul corect este:

    process/pp_flat_stacked.fit          <- prefixul "pp_" = calibrat. ASTA.

NU folosi un .NEF brut si NU folosi un stack necalibrat. Offset-ul (~600 ADU
dintr-un flat de ~30000) aplatizeaza profilul si subraporteaza vignetarea cu
~1%. Iar toate testele de mai jos vor spune "OK" - profilul ramane perfect
simetric radial, doar amplitudinea e gresita. Nu e detectabil, doar evitabil.
Greseala creste cu ISO, pentru ca offset-ul in ADU creste cu gain-ul.

ISO-ul NU CONTEAZA
------------------
Nu modifica nimic pentru ISO. Fiecare metrica e un raport - log2(centru/colt),
(max-min)/medie, img/model - iar ISO e un scalar, deci se simplifica din toate.
Verificat pe date sintetice: master la ISO 1600 (zgomot 0.64%/px) si la ISO 100
(0.16%/px) dau cifre identice (0.07% vs 0.06%), la un prag de 1.5%.
Motivul: scriptul mediaza 4x4 pixeli, apoi ia mediane peste mii de pixeli.

IDEEA DE BAZA
-------------
Pentru un obiectiv fotografic fara tren optic (Samyang 135 f/2 pe D3300),
un flat CORECT este, prin constructie, o functie *pur radiala* centrata pe
axa optica. Vignetarea e simetrica rotational. Praful la f/2 e sub prag.

Deci orice abatere de la simetria radiala NU e vignetare. E eroare:
    - sursa de lumina neuniforma (backlight de monitor)
    - light leak (vizor, ambient)
    - camera inclinata fata de sursa
    - hartie difuzoare prea departe de obiectiv

"Flat bun" nu inseamna "arata plat". Inseamna "abaterea de la simetria
radiala e sub zgomot". Aia e o cifra, si o poti masura.

Scriptul modeleaza profilul radial, il imparte la loc, si se uita la ce
ramane. Ce ramane trebuie sa fie zgomot.

TREI MODURI
-----------
1. STRUCTURA (implicit) - e master flat-ul simetric radial?

       python3 flatcheck.py process/pp_flat_stacked.fit

   Ruleaza-l pe masterul final, o data, ca sa confirmi ca e curat.

2. TESTUL DE ROTATIE 180 GRADE - valideaza SETUP-ul, nu masterul.

       python3 flatcheck.py pp_flat_A.fit --ratio pp_flat_B.fit

   Tragi 15 flats, rotesti camera 180 grade in jurul axei optice, mai tragi 15,
   calibrezi si stackuiesti separat cele doua seturi.
   Vignetarea si praful sunt fixe pe senzor -> identice in ambele.
   Orice gradient extern e fix in lume -> se inverseaza.
   Deci A/B trebuie sa fie 1 peste tot. Structura in A/B = contaminare,
   la ~2x amplitudinea erorii reale din fiecare master.

   PUNCT ORB: testul e invariant la contaminare simetrica radial in jurul axei
   optice. Daca centrezi obiectivul pe mijlocul monitorului, caderea de
   backlight a ecranului e cvasi-radiala in jurul axei tale, se aduna la
   vignetare, si testul raporteaza "curat" cu seninatate. Pentru asta ai
   testul de distanta: un set la 30cm si unul la 60cm, apoi --ratio intre ele.
   Vignetarea obiectivului e constanta cu distanta; contaminarea nu e.

3. TESTUL END-TO-END - singurul care conteaza cu adevarat.

       python3 flatcheck.py process/pp_light_00001.fit --light

   Un light (sau un stack) CALIBRAT cu flat, dar FARA background extraction.

4. VERIFICAREA EXPUNERII - pe un flat BRUT, inainte de a construi biblioteca.

       python3 flatcheck.py flat_brut.NEF --expo
       python3 flatcheck.py flat_necalibrat.fit --expo --sat 4095 --black 150

   Ruleaza pe un SINGUR flat brut (NEF direct, sau un flat convertit in Siril dar
   NECALIBRAT) - NU pe master, pentru ca stacking-ul normalizeaza si sterge nivelul.
   Spune cat de sus e CENTRUL cadrului fata de saturatie si in ce directie sa modifici.
   IMPORTANT: masoara CENTRUL (fereastra 20%), nu mediana globala. Un flat are prin
   definitie o distributie larga (asta E vignetarea): centru luminos, colturi
   intunecate. Mediana globala e trasa in jos de colturi si te-ar pacali sa supraexpui
   pana clipezi centrul. Ce conteaza pt clipping e centrul, unde satureaza intai.
   Tinta: centrul la 50-65%. (Histograma camerei/ViewNX arata imaginea PROCESATA -
   debayer+WB+gamma - si trage varfurile spre dreapta, de aia pare "la limita" cand
   datele brute sunt la ~40%. Pentru histograma pe date brute reale: Siril, nu ViewNX.)
   NEF -> rawpy da white/black exacte. FITS -> scala auto-detectata; --sat/--black daca
   greseste. Ca sa afli white-ul exact: un cadru supraexpus, maxul de acolo E saturatia.
   Fondul nu trebuie sa aiba componenta radiala centrata pe cadru.
   Gradientul de poluare luminoasa e liniar/inclinat, nu radial -> scriptul le
   separa printr-un fit simultan (plan + termeni radiali). De asta testul
   functioneaza si pe cer poluat.

   Semnul iti da diagnosticul:
     fond care CADE spre margini  -> sub-corectie. Alta diafragma la flats
                                     decat la lights, sau flat contaminat.
     fond care CRESTE spre margini -> supra-corectie. Aproape sigur offset:
                                     flat-darks lipsa/gresite, sau lights
                                     necalibrate cu bias (vezi mai jos).

   ATENTIE FARA DARKS: daca nu tragi darks, lights-urile TREBUIE sa primeasca
   un bias, altfel offset-ul se imparte la flat si se amplifica radial in
   colturi. In Siril: -bias=... la calibrate. Scriptul OSC_Preprocessing_
   WithoutDark face asta automat cu continutul folderului biases/ - motiv
   pentru care flat-darks-urile din biases/ trebuie sa fie la ACELASI ISO ca
   lights-urile.

   Modul 1 si 2 sunt proxy. Doar modul 3 inchide bucla. Ruleaza-l o data, la
   prima tinta reala. Daca da OK, tot lantul e validat pentru ani.

CE INVALIDEAZA MASTERUL
-----------------------
Curatare de senzor. Alta diafragma. Filtru/inel schimbat. Lovitura serioasa la
obiectiv. NU invalideaza: timpul, rotatia camerei pe cer, sesiuni diferite,
temperatura, ISO-ul lights-urilor.

DEPENDINTE
----------
    pip install numpy astropy matplotlib
    pip install rawpy        # optional, doar daca dai .NEF direct

NOTE TEHNICE
------------
- Face automat block-mean 2x2, ceea ce elimina modulatia Bayer (fiecare bloc
  2x2 RGGB contine exact 1 R, 2 G, 1 B -> combinatie fixa, profilul radial se
  pastreaza intact). Deci merge direct pe master flat CFA nedebayerizat din
  Siril, care e exact ce produce pp_flat_stacked.fit.
- Apoi downsampleaza la ~1200px pe latura lunga. Vignetarea e neteda, nu
  pierzi nimic, si scapi de zgomot ca sa vezi structura.
- Modelul radial si asimetria se calculeaza in jurul centrului SENZORULUI, nu
  al varfului de iluminare fitat. Vezi comentariul din mode_structure: fitarea
  centrului absoarbe exact gradientul liniar pe care il cauti.
- Cifra de vignetare e informativa, nu un test. Pentru referinta, Samyang 135
  f/2 pe APS-C ar trebui sa dea ~0.5-0.9 stopuri centru->colt.
- Pragurile de mai jos sunt reguli empirice, nu constante fizice. Sunt un
  punct de plecare. Ruleaza scriptul o data pe un setup despre care esti sigur
  si calibreaza-le pe cifrele tale.
"""

import argparse
import sys
import numpy as np

# ---------------------------------------------------------------- praguri
# (procente; ajusteaza dupa ce vezi cifre reale pe setup-ul tau)
TH_CORNER_OK, TH_CORNER_BAD = 1.5, 4.0     # imprastiere intre cele 4 colturi
TH_AZIM_OK, TH_AZIM_BAD = 1.5, 4.0         # asimetrie azimutala per inel
TH_CENTROID_OK, TH_CENTROID_BAD = 4.0, 10.0  # offset centru, % din semidiagonala
TH_RATIO_OK, TH_RATIO_BAD = 1.0, 3.0       # structura in A/B (testul 180)


# ---------------------------------------------------------------- I/O
def load_image(path):
    p = path.lower()
    if p.endswith(('.fit', '.fits', '.fts')):
        from astropy.io import fits
        data = None
        with fits.open(path) as hdul:
            for h in hdul:
                if h.data is not None and h.data.size > 1:
                    data = np.asarray(h.data, dtype=np.float64)
                    break
        if data is None:
            raise ValueError(f"Niciun array de imagine in {path}")
    elif p.endswith(('.nef', '.cr2', '.cr3', '.arw', '.dng', '.raf')):
        import rawpy
        with rawpy.imread(path) as raw:
            data = raw.raw_image_visible.astype(np.float64)
    else:
        raise ValueError(f"Format nerecunoscut: {path} (accept .fit/.fits, raw)")

    if data.ndim == 3:
        # Siril color = (3, H, W); alte tool-uri = (H, W, 3)
        data = data.mean(axis=0) if data.shape[0] in (3, 4) else data.mean(axis=2)
    if data.ndim != 2:
        raise ValueError(f"Astept 2D, am primit {data.shape}")
    return data


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
    """Vertexul unui paraboloid fitat pe log(I) = centrul de iluminare."""
    h, w = img.shape
    y, x = np.mgrid[0:h, 0:w].astype(np.float64)
    xn, yn = (x - w / 2) / (w / 2), (y - h / 2) / (h / 2)
    v = np.log(np.maximum(img, 1e-12))
    s = max(1, min(h, w) // 200)
    xs, ys, vs = xn[::s, ::s].ravel(), yn[::s, ::s].ravel(), v[::s, ::s].ravel()
    A = np.column_stack([np.ones_like(xs), xs, ys, xs**2, ys**2, xs * ys])
    coef, *_ = np.linalg.lstsq(A, vs, rcond=None)
    _, b, c, d, e, f = coef
    M = np.array([[2 * d, f], [f, 2 * e]])
    try:
        sol = np.linalg.solve(M, np.array([-b, -c]))
    except np.linalg.LinAlgError:
        return w / 2.0, h / 2.0
    cx, cy = sol[0] * (w / 2) + w / 2, sol[1] * (h / 2) + h / 2
    if not (0 < cx < w and 0 < cy < h):   # fit degenerat -> fallback
        return w / 2.0, h / 2.0
    return cx, cy


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
    fx, fy = fit_center(img)
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

    print(f"\n  VIGNETARE (informativ, nu e un test)")
    print(f"    centru -> colt          : {vign_stops:.2f} stopuri "
          f"(colt = {cvals.mean()/ctr*100:.0f}% din centru)")

    print(f"\n  TESTE")
    print(f"    imprastiere 4 colturi   : {spread:5.2f} %   [{verdict(spread, TH_CORNER_OK, TH_CORNER_BAD)}]")
    for k, v in cor.items():
        print(f"        {k:<14}: {v/cvals.mean()*100:6.2f} % din media colturilor")
    print(f"    asimetrie azimutala max : {az_max:5.2f} %   [{verdict(az_max, TH_AZIM_OK, TH_AZIM_BAD)}]")
    print(f"    reziduu dupa model rad. : {res_ptp:5.2f} %   [{verdict(res_ptp, TH_AZIM_OK, TH_AZIM_BAD)}]")
    print(f"    offset centru iluminare : {off:5.2f} %   [{verdict(off, TH_CENTROID_OK, TH_CENTROID_BAD)}]")
    print(f"        varf iluminare = ({fx:.0f}, {fy:.0f})  vs  centru senzor = ({cx:.0f}, {cy:.0f})")

    worst = max(
        verdict(spread, TH_CORNER_OK, TH_CORNER_BAD),
        verdict(az_max, TH_AZIM_OK, TH_AZIM_BAD),
        verdict(res_ptp, TH_AZIM_OK, TH_AZIM_BAD),
        verdict(off, TH_CENTROID_OK, TH_CENTROID_BAD),
        key=lambda s: {"OK": 0, "SUSPECT": 1, "PROBLEMA": 2}[s])
    print(f"\n  VERDICT: {worst}")
    if worst != "OK":
        print("    Abatere de la simetria radiala => sursa neuniforma, light leak,")
        print("    camera inclinata, sau ambient. NU e vignetare. Vezi harta reziduala.")

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


def mode_light(path, plot_path):
    raw = load_image(path)
    img = prepare(raw)
    h, w = img.shape
    cx, cy = w / 2.0, h / 2.0
    r_corner = np.hypot(cx, cy)

    print(f"\n=== TEST END-TO-END: {path} ===")
    print("    Astept un stack CALIBRAT cu flat, dar FARA background extraction.")

    bg, bx, by = tile_background(img)

    # Fit simultan: fond = plan + componenta radiala.
    #   plan (a + b*x + c*y)  absoarbe gradientul de poluare luminoasa
    #   r^2 si r^4            capteaza vignetare reziduala (eroare de flat)
    # De asta testul merge si pe cer poluat: cele doua au forme diferite si
    # se separa. ATENTIE: NU folosi percentile pe inele circulare - p20 pe un
    # inel prinde partea intunecata a gradientului de cer si o raporteaza ca
    # falsa vignetare. (Am facut fix greseala asta prima data.)
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

    print(f"\n    tile-uri folosite       : {keep.sum()} / {keep.size} "
          f"(restul = nebulozitate/stele, respinse)")
    print(f"    gradient de cer (plan)  : {plane_pct:5.2f} %  <- poluare luminoasa, IGNORA")
    print(f"    componenta radiala      : {rad_pct:+6.2f} %   "
          f"[{verdict(abs(rad_pct), 2.0, 5.0)}]  <- ASTA e flat-ul")

    print("\n  INTERPRETARE")
    if abs(rad_pct) < 2.0:
        print("    Fondul n-are componenta radiala. Flat-ul face treaba.")
        print("    Asta e testul care conteaza - toate celelalte sunt proxy pentru el.")
    elif rad_pct > 0:
        print("    Fondul cade spre margini => SUB-corectie. Flat-ul nu prinde toata")
        print("    vignetarea: alta diafragma decat la lights, sau flat contaminat.")
    else:
        print("    Fondul creste spre margini => SUPRA-corectie. Cel mai probabil")
        print("    flat-darks lipsa/gresite: imparti la un flat care contine offset,")
        print("    iar eroarea se amplifica exact in colturi.")

    model = (a + b * X + c * Y + d * R2 + e * R2**2).reshape(bg.shape)
    radial_only = (d * R2 + e * R2**2).reshape(bg.shape) / a
    prof_r = np.linspace(0, 1, 40)
    make_plot(plot_path, bg, radial_only + 1.0,
              prof_r * r_corner, 1 + d * prof_r**2 + e * prof_r**4,
              [], cx / (w / bg.shape[1]), cy / (h / bg.shape[0]),
              min(bg.shape) / 2, path, "componenta radiala extrasa (fara plan)")


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
    ax[1, 1].axhline(TH_AZIM_OK, color='g', ls='--', lw=0.8)
    ax[1, 1].axhline(TH_AZIM_BAD, color='r', ls='--', lw=0.8)
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


def mode_expo(path, sat_override=None, black_override=None):
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

    CEN_LO, CEN_HI = 50.0, 65.0   # tinta pt CENTRU (nu 50-60 ca la mediana globala)
    if cen_clip > TH_EXPO_CLIP or cen_pct > 80:
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
    p = argparse.ArgumentParser(description="Validare de master flats.")
    p.add_argument("image", help="master flat .fit (sau light calibrat cu --light)")
    p.add_argument("--ratio", metavar="B.fit", help="testul de rotatie 180: image / B")
    p.add_argument("--light", action="store_true",
                   help="test end-to-end pe un light calibrat fara BGE")
    p.add_argument("--expo", action="store_true",
                   help="verifica expunerea unui flat BRUT (NEF sau flat necalibrat), nu master")
    p.add_argument("--sat", type=float, default=None,
                   help="nivel de saturatie (white), daca auto-detectia greseste")
    p.add_argument("--black", type=float, default=None, help="nivel black, pt --expo pe FITS")
    p.add_argument("-o", "--out", default="flatcheck.png", help="fisier PNG de iesire")
    a = p.parse_args()

    if a.expo:
        mode_expo(a.image, a.sat, a.black)
    elif a.ratio:
        mode_ratio(a.image, a.ratio, a.out)
    elif a.light:
        mode_light(a.image, a.out)
    else:
        mode_structure(a.image, a.out)
    print()


if __name__ == "__main__":
    main()