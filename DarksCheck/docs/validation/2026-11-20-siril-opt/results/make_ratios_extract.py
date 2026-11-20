"""Take from the measurement's three V*_ratios.json the three fields the record uses, per light
and per master: the light's own hot-pixel ratio (`ratio`, the "right factor"), the factor Siril's
measure asks for (`k_siril_criterion`: covariance of light and dark over the dark's variance in
the central 512 x 512, all colours together) and the same with each colour's average taken out
first (`k_sites_removed`). The source files stay as they are.

python make_ratios_extract.py <the measurement's results folder> <ratios_extract.json>
"""
import json
import sys
from pathlib import Path

FIELDS = ("ratio", "k_siril_criterion", "k_sites_removed")
MASTERS = ("D13", "D18", "D20", "D33", "D47", "D67")


def main():
    src, dst = Path(sys.argv[1]), Path(sys.argv[2])
    out = {"fields": list(FIELDS), "masters": list(MASTERS), "sessions": {}}
    for session in ("V1006", "V1106", "V1108"):
        data = json.loads((src / f"{session}_ratios.json").read_text(encoding="utf-8"))
        rows = []
        for row in data["rows"]:
            rec = {"light": row["light"], "file": row["file"]}
            for master in MASTERS:
                rec[master] = [row[master][f] for f in FIELDS]
            rows.append(rec)
        out["sessions"][session] = rows
    dst.write_text(json.dumps(out, indent=None, separators=(",", ":")) + "\n", encoding="utf-8")
    print({s: len(r) for s, r in out["sessions"].items()})


if __name__ == "__main__":
    main()
