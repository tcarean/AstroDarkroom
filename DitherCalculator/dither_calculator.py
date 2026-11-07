#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Calculator pentru dithering manual cu Star Adventurer GTi (aplicația SynScan).

Calculează:
  - scala imaginii (secunde de arc pe pixel)
  - câți pixeli se deplasează imaginea pe secundă cât ții apăsat un buton
  - câte secunde să ții apăsat pentru deplasarea de dither dorită

Valorile implicite sunt pentru Nikon D3300 + Samyang 135 mm.
Rulare:  python dither_calculator.py   (Enter = păstrezi valoarea implicită)

Treptele de viteză (0-8) sunt cele din manualul aplicației (SynScan App
User's Manual, 2023-05-25, p. 8), în multipli ai vitezei siderale:
0 = 0,5x, 1 = 1x, 2 = 8x, 3 = 16x, 4 = 32x, 5 = 64x, 6 = 128x, 7 = 400x, 8 = 600x.

Presupunere (manualul nu o spune): butonul mișcă montura cu această viteză
față de stele (pe RA, la 1x butonul de est oprește practic urmărirea, cel
de vest o dublează - în ambele cazuri imaginea se deplasează cu 1x față de
cer). Peste 1x, butonul de est inversează motorul RA, deci și RA pierde
timp în backlash.
"""

import math
import sys

# O rotație completă (360°) într-o zi siderală (23 h 56 min 4 s)
VITEZA_SIDERALA = 360 * 3600 / 86164.0905  # ≈ 15,04 secunde de arc pe secundă

# Treptele butoanelor din SynScan, în multipli siderali (manualul aplicației, p. 8);
# aplicația arată numărul treptei. Treapta 9 = Max nu are un multiplu fix.
TREPTE_SYNSCAN = (0.5, 1, 8, 16, 32, 64, 128, 400, 600)

# Săgețile din aplicația SynScan Pro pe Star Adventurer GTi, testate de proprietar
# (rezultat dat pe 2026-11-07). Stânga-dreapta mută RA, sus-jos mută Dec.
# Altă montură sau aplicație: schimbă-le aici.
SAGEATA_EST, SAGEATA_VEST = "→", "←"
SAGEATA_NORD, SAGEATA_SUD = "↓", "↑"

# Valori implicite: Nikon D3300 (senzor 23,5 mm, 6000 px lățime) + Samyang 135 mm
IMPLICIT_FOCALA_MM = 135.0
IMPLICIT_PIXEL_UM = 3.92      # 23,5 mm / 6000 px ≈ 3,92 µm
IMPLICIT_TREAPTA = 1          # 1x sideral
IMPLICIT_DECLINATIE = 0.0     # grade
IMPLICIT_DITHER_PX = 15.0

# Regulă practică (nu o formulă exactă): 10-20 px pe camera principală
DITHER_MIN_PX = 10
DITHER_MAX_PX = 20

# Peste atât, ținutul apăsat între cadre nu mai e practic
SECUNDE_MAX = 60

DECLINATII_TABEL = (0, 15, 30, 45, 60, 75)


def fmt(valoare, zecimale=1):
    """Număr cu un număr fix de zecimale și virgulă zecimală."""
    return f"{valoare:.{zecimale}f}".replace(".", ",")


def fmt_scurt(valoare):
    """Număr fără zecimale inutile (135, 3,92, 1)."""
    return format(valoare, "g").replace(".", ",")


def citeste_numar(mesaj, implicit, valid, eroare):
    """Cere un număr. Enter = valoarea implicită. Acceptă virgulă sau punct."""
    while True:
        # PowerShell pune un BOM (U+FEFF) în fața textului trimis prin pipe
        text = input(f"{mesaj} [{fmt_scurt(implicit)}]: ").replace(chr(0xFEFF), "").strip()
        if not text:
            return implicit
        try:
            valoare = float(text.replace(",", "."))
        except ValueError:
            valoare = math.nan
        if not math.isfinite(valoare):
            print("  Nu e un număr valid. Exemplu: 135 sau 3,92")
            continue
        if not valid(valoare):
            print(f"  {eroare}")
            continue
        return valoare + 0.0  # + 0.0 transformă -0 în 0


def scala_imagine(pixel_um, focala_mm):
    """Secunde de arc pe pixel."""
    return 206.265 * pixel_um / focala_mm


def pixeli_pe_secunda(viteza_x, scala, declinatie=None):
    """Deplasarea imaginii în px/s. Pentru RA se dă declinația (factorul cos)."""
    arcsec_pe_s = VITEZA_SIDERALA * viteza_x
    if declinatie is not None:
        arcsec_pe_s *= math.cos(math.radians(declinatie))
    return arcsec_pe_s / scala


def secunde_pentru(pixeli, px_pe_s):
    """Secunde de ținut apăsat; None dacă axa practic nu mișcă imaginea."""
    if px_pe_s < 1e-3:
        return None
    return pixeli / px_pe_s


def text_treapta(treapta):
    """Ex.: treapta 2 (8x)."""
    return f"treapta {treapta} ({fmt_scurt(TREPTE_SYNSCAN[treapta])}x)"


def text_secunde(secunde, sfat):
    """Secundele de ținut apăsat; sfatul apare când axa nu ajunge."""
    if secunde is None:
        return f"nu mișcă practic imaginea - {sfat}"
    if secunde > SECUNDE_MAX:
        return f"{fmt(secunde)} s - prea mult, {sfat}"
    return f"{fmt(secunde)} s"


def calcul(implicit):
    """Un calcul; întoarce valorile introduse, implicite pentru următorul."""
    print()
    focala = citeste_numar(
        "Focala (mm)", implicit["focala"],
        lambda v: v > 0, "Focala trebuie să fie mai mare ca 0.")
    pixel = citeste_numar(
        "Dimensiunea pixelului (µm)", implicit["pixel"],
        lambda v: v > 0, "Pixelul trebuie să fie mai mare ca 0.")
    ultima = len(TREPTE_SYNSCAN) - 1
    treapta = int(citeste_numar(
        f"Treapta de viteză din SynScan (numărul dintre butoane, 0-{ultima})",
        implicit["treapta"],
        lambda v: v == int(v) and 0 <= v <= ultima,
        f"Treapta trebuie să fie un număr întreg de la 0 la {ultima} "
        f"({ultima + 1} = Max nu are o viteză fixă)."))
    viteza = TREPTE_SYNSCAN[treapta]
    dec = citeste_numar(
        "Declinația țintei în grade (ex. M31 ≈ 41, M42 ≈ -5)", implicit["dec"],
        lambda v: -90 <= v <= 90, "Declinația trebuie să fie între -90 și 90.")
    dither = citeste_numar(
        f"Dither dorit în pixeli (recomandat {DITHER_MIN_PX}-{DITHER_MAX_PX})",
        implicit["dither"],
        lambda v: v > 0, "Ditherul trebuie să fie mai mare ca 0.")

    scala = scala_imagine(pixel, focala)
    px_dec = pixeli_pe_secunda(viteza, scala)
    px_ra = pixeli_pe_secunda(viteza, scala, dec)
    sec_dec = secunde_pentru(dither, px_dec)
    sec_ra = secunde_pentru(dither, px_ra)
    if treapta < ultima:
        urca = f"urcă la {text_treapta(treapta + 1)}"
        sfat_dec, sfat_ra = urca, f"{urca} sau folosește Dec"
    else:
        sfat_dec, sfat_ra = "alege un dither mai mic", "folosește Dec"

    print("\n=== Rezultate ===")
    print(f'Scala imaginii: {fmt(scala, 2)}"/pixel')

    print(f"\nDeplasare la treapta {treapta} ({fmt_scurt(viteza)}x sideral):")
    print(f"  Dec:                {fmt(px_dec, 2)} px/s")
    eticheta_ra = f"  RA (la dec {fmt_scurt(dec)}°):"
    print(f"{eticheta_ra:<21} {fmt(px_ra, 2)} px/s")  # minim un spațiu după etichetă

    print(f"\nDither: {fmt_scurt(dither)} px "
          f"(regulă practică: {DITHER_MIN_PX}-{DITHER_MAX_PX} px)")
    if dither < DITHER_MIN_PX:
        print(f"  Atenție: sub {DITHER_MIN_PX} px, walking noise-ul și pixelii "
              "fierbinți sunt eliminați mai slab.")
    elif dither > DITHER_MAX_PX:
        print("  Merge și așa; pierzi doar mai mult din marginea cadrului "
              "și aștepți puțin mai mult să se stabilizeze stelele.")

    print(f"\nCât să ții apăsat pentru {fmt_scurt(dither)} px:")
    print(f"  Dec:  {text_secunde(sec_dec, sfat_dec)}"
          + ("  (+ backlash dacă schimbi sensul; "
             "îl poate compensa Settings > Backlash din SynScan)"
             if sec_dec is not None else ""))
    print(f"  RA:   {text_secunde(sec_ra, sfat_ra)}"
          + (f"  (cu {SAGEATA_EST} spre est, peste 1x motorul se inversează: + backlash)"
             if sec_ra is not None and viteza > 1 else ""))

    scurte = [s for s in (sec_dec, sec_ra) if s is not None and s < 1]
    if scurte:
        coboara = (f"Coboară la {text_treapta(treapta - 1)}." if treapta > 0
                   else "Treapta 0 e cea mai mică; alege un dither mai mare.")
        print("  Atenție: sub ~1 s, întârzierea aplicației prin Wi-Fi contează. " + coboara)

    print(f"\nPe RA, la alte declinații (secunde pentru {fmt_scurt(dither)} px):")
    for d in DECLINATII_TABEL:
        s = secunde_pentru(dither, pixeli_pe_secunda(viteza, scala, d))
        print(f"  dec {d:>2}°:  {text_secunde(s, sfat_ra)}")
    print("  (la fel pentru declinațiile negative)")

    print("\nNotă: treptele sunt cele din manualul aplicației SynScan (treapta 1 = 1x sideral).")
    print("Manualul nu spune dacă viteza butonului e față de cer cât timp montura urmărește.")
    # Testul e la treapta 1: acolo niciun buton nu inversează motorul RA (fără backlash)
    test_px = 10 * pixeli_pe_secunda(TREPTE_SYNSCAN[1], scala, dec)
    if test_px >= 1:
        print(f"Verifică o dată pe RA, la treapta 1: 10 s cu {SAGEATA_EST} (est) și 10 s cu "
              f"{SAGEATA_VEST} (vest) trebuie să mute steaua cu câte ~{fmt(test_px, 0)} px.")
    else:
        print("Verifică o dată pe RA, la treapta 1, pe o țintă mai departe de pol "
              "(aici RA abia mișcă imaginea).")

    return {"focala": focala, "pixel": pixel, "treapta": treapta,
            "dec": dec, "dither": dither}


def main():
    # Fără consolă (pipe, fișier), Windows scrie în cp1252 și diacriticele ar opri programul
    sys.stdout.reconfigure(encoding="utf-8")
    print("Calculator dithering manual - Star Adventurer GTi")
    print(f"Săgețile din SynScan Pro: {SAGEATA_EST} est, {SAGEATA_VEST} vest (RA); "
          f"{SAGEATA_NORD} nord, {SAGEATA_SUD} sud (Dec).")
    print("Apasă Enter ca să păstrezi valoarea dintre paranteze.")
    valori = {"focala": IMPLICIT_FOCALA_MM, "pixel": IMPLICIT_PIXEL_UM,
              "treapta": IMPLICIT_TREAPTA, "dec": IMPLICIT_DECLINATIE,
              "dither": IMPLICIT_DITHER_PX}
    try:
        while True:
            valori = calcul(valori)
            raspuns = input("\nAlt calcul? (d/N): ").strip().lower()
            if raspuns not in ("d", "da", "y", "yes"):
                break
    except (KeyboardInterrupt, EOFError):
        print()


if __name__ == "__main__":
    main()
