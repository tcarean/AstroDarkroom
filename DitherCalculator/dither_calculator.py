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

Presupunere: treapta 1 din aplicația SynScan = 1x viteza siderală, iar
butonul mișcă montura cu această viteză față de stele (pe RA, butonul
de est oprește practic urmărirea, cel de vest o dublează - în ambele
cazuri imaginea se deplasează cu 1x față de cer).
"""

import math

# O rotație completă (360°) într-o zi siderală (23 h 56 min 4 s)
VITEZA_SIDERALA = 360 * 3600 / 86164.0905  # ≈ 15,04 secunde de arc pe secundă

# Valori implicite: Nikon D3300 (senzor 23,5 mm, 6000 px lățime) + Samyang 135 mm
IMPLICIT_FOCALA_MM = 135.0
IMPLICIT_PIXEL_UM = 3.92      # 23,5 mm / 6000 px ≈ 3,92 µm
IMPLICIT_VITEZA_X = 1.0       # treapta 1 din SynScan = 1x sideral
IMPLICIT_DECLINATIE = 0.0     # grade
IMPLICIT_DITHER_PX = 15.0

# Regulă practică (nu o formulă exactă): 10-20 px pe camera principală
DITHER_MIN_PX = 10
DITHER_MAX_PX = 20

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
        text = input(f"{mesaj} [{fmt_scurt(implicit)}]: ").strip()
        if not text:
            return implicit
        try:
            valoare = float(text.replace(",", "."))
        except ValueError:
            print("  Nu e un număr valid. Exemplu: 135 sau 3,92")
            continue
        if not math.isfinite(valoare) or not valid(valoare):
            print(f"  {eroare}")
            continue
        return valoare


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


def text_secunde(secunde):
    if secunde is None:
        return "nu mișcă practic imaginea aici - folosește Dec"
    return f"{fmt(secunde)} s"


def calcul():
    print()
    focala = citeste_numar(
        "Focala (mm)", IMPLICIT_FOCALA_MM,
        lambda v: v > 0, "Focala trebuie să fie mai mare ca 0.")
    pixel = citeste_numar(
        "Dimensiunea pixelului (µm)", IMPLICIT_PIXEL_UM,
        lambda v: v > 0, "Pixelul trebuie să fie mai mare ca 0.")
    viteza = citeste_numar(
        "Viteza din SynScan, ca multiplu sideral (treapta 1 = 1x)", IMPLICIT_VITEZA_X,
        lambda v: v > 0, "Viteza trebuie să fie mai mare ca 0.")
    dec = citeste_numar(
        "Declinația țintei în grade (ex. M31 ≈ 41, M42 ≈ -5)", IMPLICIT_DECLINATIE,
        lambda v: -90 <= v <= 90, "Declinația trebuie să fie între -90 și 90.")
    dither = citeste_numar(
        f"Dither dorit în pixeli (recomandat {DITHER_MIN_PX}-{DITHER_MAX_PX})",
        IMPLICIT_DITHER_PX,
        lambda v: v > 0, "Ditherul trebuie să fie mai mare ca 0.")

    scala = scala_imagine(pixel, focala)
    px_dec = pixeli_pe_secunda(viteza, scala)
    px_ra = pixeli_pe_secunda(viteza, scala, dec)
    sec_dec = secunde_pentru(dither, px_dec)
    sec_ra = secunde_pentru(dither, px_ra)

    print("\n=== Rezultate ===")
    print(f'Scala imaginii: {fmt(scala, 2)}"/pixel')

    print(f"\nDeplasare la {fmt_scurt(viteza)}x sideral:")
    print(f"  Dec:                {fmt(px_dec, 2)} px/s")
    print(f"  RA (la dec {fmt_scurt(dec)}°):".ljust(22) + f"{fmt(px_ra, 2)} px/s")

    print(f"\nDither: {fmt_scurt(dither)} px "
          f"(regulă practică: {DITHER_MIN_PX}-{DITHER_MAX_PX} px)")
    if dither < DITHER_MIN_PX:
        print("  Atenție: sub 10 px, walking noise-ul și pixelii fierbinți "
              "sunt eliminați mai slab.")
    elif dither > DITHER_MAX_PX:
        print("  Merge și așa; pierzi doar mai mult din marginea cadrului "
              "și aștepți puțin mai mult să se stabilizeze stelele.")

    print(f"\nCât să ții apăsat pentru {fmt_scurt(dither)} px:")
    print(f"  Dec:  {text_secunde(sec_dec)}"
          + ("  (+ timpul pierdut în backlash dacă schimbi sensul)"
             if sec_dec is not None else ""))
    print(f"  RA:   {text_secunde(sec_ra)}")

    scurte = [s for s in (sec_dec, sec_ra) if s is not None and s < 1]
    if scurte:
        print("  Atenție: sub ~1 s, întârzierea aplicației prin Wi-Fi contează. "
              "Folosește o viteză mai mică.")

    print(f"\nPe RA, la alte declinații (secunde pentru {fmt_scurt(dither)} px):")
    for d in DECLINATII_TABEL:
        s = secunde_pentru(dither, pixeli_pe_secunda(viteza, scala, d))
        print(f"  dec {d:>2}°:  {text_secunde(s)}")
    print("  (la fel pentru declinațiile negative)")

    print("\nNotă: calculul presupune că treapta 1 din SynScan = 1x sideral.")
    print("Verifică o dată: ține apăsat 10 s și măsoară cât s-a mutat o stea în pixeli.")


def main():
    print("Calculator dithering manual - Star Adventurer GTi")
    print("Apasă Enter ca să păstrezi valoarea dintre paranteze.")
    try:
        while True:
            calcul()
            raspuns = input("\nAlt calcul? (d/N): ").strip().lower()
            if raspuns not in ("d", "da", "y", "yes"):
                break
    except (KeyboardInterrupt, EOFError):
        print()


if __name__ == "__main__":
    main()
