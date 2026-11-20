"""Siril's -opt criterion re-done in Python for some lights against one master, compared with
the k0 that Siril logged.

python -I repro.py <out.json> <light dir> <master.fit> <first,second,...light numbers (1-based)> [<siril log>]
"""
import json
import re
import sys
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
import crit  # noqa: E402


def logged_k0(log):
    out = {}
    if log and Path(log).exists():
        for m in re.finditer(r"Dark optimization of image (\d+): k0=([0-9.]+)", Path(log).read_text(errors="replace")):
            out[int(m.group(1))] = float(m.group(2))
    return out


def main():
    out, ldir, master, nums = Path(sys.argv[1]), Path(sys.argv[2]), Path(sys.argv[3]), sys.argv[4]
    log = sys.argv[5] if len(sys.argv) > 5 else None
    D = crit.box(master)
    siril = logged_k0(log)
    rows = []
    for n in [int(x) for x in nums.split(",")]:
        L = crit.box(ldir / f"light_{n:05d}.fit")
        k0, steps = crit.siril_k0(L, D)
        dec = crit.decompose(L, D)
        ks = np.linspace(0, 2, 11)
        curve = [round(float(crit.noise(L, D, k)) * 65535.0, 4) for k in ks]
        row = {"light": n, "index": n - 1, "python_k0": round(k0, 3), "python_k0_raw": k0,
               "siril_k0": siril.get(n - 1), "k_star_cov_var": round(crit.k_star(L, D), 3),
               "noise_ADU_at_k_0_0.2_..._2": curve,
               "search_steps": [(round(c, 5), round(f * 65535.0, 6)) for c, f in steps]}
        row.update({k: (round(v, 4) if isinstance(v, float) else v) for k, v in dec.items()})
        rows.append(row)
        print(json.dumps({k: row[k] for k in ("light", "python_k0", "siril_k0", "k_star_cov_var", "cov_site", "cov_within", "var_site", "var_within", "k_within")}), flush=True)
    out.write_text(json.dumps({"lights": str(ldir), "master": str(master), "log": log, "rows": rows}, indent=1))


if __name__ == "__main__":
    main()
