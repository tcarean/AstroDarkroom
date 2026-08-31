#!/usr/bin/env python3
"""crosscheck.py - compara flatcheck.exe cu flatcheck.py pe aceleasi fisiere FITS.

flatcheck.py e oracolul numeric pentru portul C# (vezi CLAUDE.md). Fisierele golden din
FlatCheck.Tests/Golden sunt capturate din C# insusi, deci fixeaza comportamentul fara
sa-l confirme. Scriptul asta inchide gaura: ruleaza ambele implementari pe bytes
identici si compara.

Doua comparatii per scenariu:
  TEXT    - iesirea completa, dupa normalizarea caii PNG
  NUMBERS - fiecare token numeric, comparat ca float

O diferenta la NUMBERS e un defect real. Diferentele doar de TEXT sunt divergentele
documentate (fara NEF, formulari diferite).

Utilizare:

    # 1. scrie fixturile din suita de teste C#
    FLATCHECK_DUMP_FIXTURES=<dir> dotnet test FlatCheck.sln --filter DumpsFixtures

    # 2. compara
    python crosscheck.py <dir>

Dependinte: numpy, astropy, matplotlib (pentru flatcheck.py).
"""
import difflib
import os
import re
import subprocess
import sys

ROOT = os.path.dirname(os.path.abspath(__file__))
PY = os.path.join(ROOT, "flatcheck.py")
EXE = os.path.join(ROOT, "FlatCheck", "bin", "Release", "net8.0", "flatcheck.exe")

SCENARIOS = [
    ("structure clean",        ["clean.fit"]),
    ("structure gradient",     ["grad.fit"]),
    ("structure diagonal",     ["diag.fit"]),
    ("structure band BUN",     ["grad_bun.fit"]),
    ("structure band REFA",    ["grad_refa.fit"]),
    ("structure uniform",      ["uniform.fit"]),
    ("structure mild vign",    ["vign_mild.fit"]),
    ("structure heavy vign",   ["vign_heavy.fit"]),
    ("structure pedestal",     ["pedestal.fit"]),
    ("structure wide 768x512", ["wide.fit"]),
    ("structure tall 512x768", ["tall.fit"]),
    ("structure float32",      ["float32.fit"]),
    ("structure int32",        ["int32.fit"]),

    ("ratio clean/gradient",   ["clean.fit", "--ratio", "grad.fit"]),
    ("ratio clean/clean",      ["clean.fit", "--ratio", "clean.fit"]),
    ("ratio mild/heavy",       ["vign_mild.fit", "--ratio", "vign_heavy.fit"]),
    ("ratio wide/wide",        ["wide.fit", "--ratio", "wide.fit"]),

    ("light undercorrected",   ["clean.fit", "--light"]),
    ("light overcorrected",    ["overcorrected.fit", "--light"]),
    ("light pure sky plane",   ["skyplane.fit", "--light"]),
    ("light uniform",          ["uniform.fit", "--light"]),
    ("light nebulosity",       ["nebula.fit", "--light"]),
    ("light wide",             ["wide.fit", "--light"]),

    ("expo cfa",               ["raw_cfa.fit", "--expo"]),
    ("expo cfa --sky",         ["raw_cfa.fit", "--expo", "--sky"]),
    ("expo 12-bit manual",     ["raw12bit.fit", "--expo", "--sat", "4095", "--black", "150"]),
    ("expo debayered",         ["clean.fit", "--expo"]),
    ("expo 25%",               ["expo25.fit", "--expo", "--sat", "4095"]),
    ("expo 30%",               ["expo30.fit", "--expo", "--sat", "4095"]),
    ("expo 50%",               ["expo50.fit", "--expo", "--sat", "4095"]),
    ("expo 65%",               ["expo65.fit", "--expo", "--sat", "4095"]),
    ("expo 81%",               ["expo81.fit", "--expo", "--sat", "4095"]),
    ("expo 25% --sky",         ["expo25.fit", "--expo", "--sky", "--sat", "4095"]),
    ("expo 65% --sky",         ["expo65.fit", "--expo", "--sky", "--sat", "4095"]),
]

# Cunoscute si acceptate: portul C# se abate INTENTIONAT aici. Vezi CLAUDE.md.
KNOWN_NUMERIC_DIVERGENCE = {
    "structure uniform":
        "FitCenter pe un cadru perfect constant. Coeficientii patratici sunt zgomot de "
        "virgula mobila (~1e-15) si determinantul iese 1.3e-29. C# are garda "
        "|det| < 1e-15 si cade pe centrul senzorului (0.00%, OK); Python n-are garda, "
        "np.linalg.solve nu arunca, si raporteaza un varf fals la (136,136) -> 6.35%, "
        "SUSPECT. C#-ul e corect; Python da un fals pozitiv pe un cadru uniform.",
}

# numere de sine statatoare - nu "3"-ul din "python3"
NUM = re.compile(r"(?<![A-Za-z0-9.])[-+]?\d+\.?\d*(?![A-Za-z0-9])")


def norm(text):
    out = []
    for line in text.replace("\r\n", "\n").split("\n"):
        if "harta salvata" in line:
            line = "    -> harta salvata: <PNG>"
        out.append(line.rstrip())
    while out and not out[-1]:
        out.pop()
    while out and not out[0]:
        out.pop(0)
    return out


def numbers(lines):
    vals = []
    for line in lines:
        if "harta salvata" in line:
            continue
        for tok in NUM.findall(line):
            try:
                vals.append(float(tok))
            except ValueError:
                pass
    return vals


def main(fixture_dir):
    if not os.path.isdir(fixture_dir):
        sys.exit(f"nu exista directorul cu fixturi: {fixture_dir}")
    if not os.path.exists(EXE):
        sys.exit(f"lipseste binarul Release: {EXE}\n  dotnet build FlatCheck.sln -c Release")

    def run(cmd):
        p = subprocess.run(cmd, cwd=fixture_dir, capture_output=True, text=True)
        return (p.stdout or "") + (p.stderr or "")

    identical, text_only, numeric, expected = 0, [], [], []
    for name, args in SCENARIOS:
        a = norm(run([sys.executable, PY] + args + ["-o", "py.png"]))
        b = norm(run([EXE] + args + ["-o", "cs.png"]))
        na, nb = numbers(a), numbers(b)
        nums_ok = len(na) == len(nb) and all(
            abs(x - y) <= 1e-9 + 1e-9 * max(abs(x), abs(y)) for x, y in zip(na, nb))

        if a == b:
            identical += 1
            print(f"[IDENTICAL] {name}")
        elif nums_ok:
            text_only.append(name)
            print(f"[TEXT-ONLY] {name}  ({len(na)} numere egale)")
        elif name in KNOWN_NUMERIC_DIVERGENCE:
            expected.append(name)
            print(f"[KNOWN DIV] {name}")
            print(f"            {KNOWN_NUMERIC_DIVERGENCE[name]}")
        else:
            numeric.append(name)
            print(f"[NUMBERS!!] {name}")
            for d in difflib.unified_diff(a, b, "flatcheck.py", "flatcheck.exe",
                                          lineterm="", n=1):
                print("     " + d)

    print()
    print(f"scenarii              : {len(SCENARIOS)}")
    print(f"identice la byte      : {identical}")
    print(f"doar text (numere ok) : {len(text_only)}")
    print(f"divergente cunoscute  : {len(expected)}")
    print(f"DIFERENTE NUMERICE    : {len(numeric)} {numeric}")
    return 1 if numeric else 0


if __name__ == "__main__":
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    sys.exit(main(sys.argv[1]))
