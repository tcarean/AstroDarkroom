#!/usr/bin/env python3
"""
Copiaza metadatele WCS (plate solving) si datele optice dintr-un FITS in altul.

Uz tipic: ai un stack liniar plate-solved si o imagine finala procesata care
a pierdut solutia astrometrica. Geometria trebuie sa fie identica.

Exemple:
    python copy_wcs.py stack_solved.fit final.fit
    python copy_wcs.py stack_solved.fit final.fit -o final_wcs.fit
    python copy_wcs.py stack_solved.fit final.fit --dry-run
"""

import argparse
import shutil
import sys

try:
    from astropy.io import fits
except ImportError:
    sys.exit("Lipseste astropy. Instaleaza cu: pip install astropy")


# Cheile care descriu solutia astrometrica propriu-zisa.
WCS_KEYS = [
    "WCSAXES", "CTYPE1", "CTYPE2", "CUNIT1", "CUNIT2",
    "CRPIX1", "CRPIX2", "CRVAL1", "CRVAL2",
    "CDELT1", "CDELT2", "CROTA1", "CROTA2",
    "CD1_1", "CD1_2", "CD2_1", "CD2_2",
    "PC1_1", "PC1_2", "PC2_1", "PC2_2",
    "EQUINOX", "RADESYS", "LONPOLE", "LATPOLE",
    "A_ORDER", "B_ORDER", "AP_ORDER", "BP_ORDER",  # distorsiune SIP
    "PLTSOLVD",
]

# Metadate optice / de observatie. Fara FOCALLEN si XPIXSZ, un re-solve
# ulterior nu are scara de pornire, deci merita copiate si ele.
OPTICAL_KEYS = [
    "FOCALLEN", "FOCAL", "XPIXSZ", "YPIXSZ", "XBINNING", "YBINNING",
    "OBJCTRA", "OBJCTDEC", "RA", "DEC", "OBJECT",
    "SITELAT", "SITELONG", "SITEELEV", "DATE-OBS", "INSTRUME", "TELESCOP",
]


def sip_coefficient_keys(header):
    """Coeficientii SIP (A_0_2, B_1_1, AP_..., BP_...) au nume variabile."""
    prefixes = ("A_", "B_", "AP_", "BP_")
    found = []
    for key in header:
        if not key or key in WCS_KEYS:
            continue
        if key.startswith(prefixes) and key[-1].isdigit():
            found.append(key)
    return found


def main():
    p = argparse.ArgumentParser(
        description="Copiaza WCS + metadate optice dintr-un FITS sursa in unul destinatie.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
    )
    p.add_argument("source", help="FITS-ul plate-solved (de unde se citesc metadatele)")
    p.add_argument("dest", help="FITS-ul procesat (unde se scriu metadatele)")
    p.add_argument("-o", "--output",
                   help="Scrie intr-un fisier nou in loc sa modifice destinatia in loc")
    p.add_argument("--dry-run", action="store_true",
                   help="Arata ce s-ar copia, fara sa scrie nimic")
    p.add_argument("--force", action="store_true",
                   help="Continua chiar daca dimensiunile imaginilor difera")
    p.add_argument("--no-optical", action="store_true",
                   help="Copiaza doar cheile WCS, fara metadatele optice")
    args = p.parse_args()

    try:
        src_hdr = fits.getheader(args.source)
    except Exception as e:
        sys.exit(f"Nu pot citi sursa '{args.source}': {e}")

    try:
        dst_hdr_probe = fits.getheader(args.dest)
    except Exception as e:
        sys.exit(f"Nu pot citi destinatia '{args.dest}': {e}")

    # Verificare geometrie: WCS-ul e valabil doar daca grila de pixeli e aceeasi.
    src_dim = (src_hdr.get("NAXIS1"), src_hdr.get("NAXIS2"))
    dst_dim = (dst_hdr_probe.get("NAXIS1"), dst_hdr_probe.get("NAXIS2"))
    if src_dim != dst_dim:
        msg = f"Dimensiuni diferite: sursa {src_dim} vs destinatie {dst_dim}."
        if not args.force:
            sys.exit(msg + " WCS-ul ar fi gresit. Foloseste --force daca stii ce faci.")
        print(f"ATENTIE: {msg} Continui oricum (--force).")

    if not src_hdr.get("CTYPE1"):
        print("ATENTIE: sursa nu pare plate-solved (lipseste CTYPE1).")

    keys = list(WCS_KEYS)
    keys += sip_coefficient_keys(src_hdr)
    if not args.no_optical:
        keys += OPTICAL_KEYS

    present = [k for k in keys if k in src_hdr]
    if not present:
        sys.exit("Nicio cheie relevanta gasita in sursa. Nimic de copiat.")

    if args.dry_run:
        print(f"Chei care ar fi copiate ({len(present)}):")
        for k in present:
            print(f"  {k:10s} = {src_hdr[k]}")
        return

    target = args.output or args.dest
    if args.output:
        shutil.copyfile(args.dest, args.output)

    with fits.open(target, mode="update") as hdul:
        hdr = hdul[0].header
        for k in present:
            hdr[k] = (src_hdr[k], src_hdr.comments[k])
        hdr.add_history("WCS copiat din " + args.source)
        hdul.flush()

    print(f"Copiate {len(present)} chei in {target}.")
    print("Verifica vizual adnotarea: daca imaginea a fost oglindita/flip-uita "
          "pe parcursul procesarii, WCS-ul copiat va fi gresit fara sa dea eroare.")


if __name__ == "__main__":
    main()
