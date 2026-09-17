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
"""Reimprospateaza copia bazei lensfun din acest folder (unealta de dezvoltare, nu face
parte din aplicatii). Descarca data/db din repo-ul lensfun de pe GitHub, inlocuieste
fisierele XML si DTD de aici si rescrie LENSFUN-VERSION.txt cu commit-ul si data
upstream. Ruleaza-l din orice folder: python lensfun/update-lensfun.py"""
import datetime
import json
import os
import urllib.request

API = "https://api.github.com/repos/lensfun/lensfun"
HERE = os.path.dirname(os.path.abspath(__file__))


def main():
    files = json.load(urllib.request.urlopen(API + "/contents/data/db"))
    commits = json.load(urllib.request.urlopen(API + "/commits?path=data/db&per_page=1"))
    sha, date = commits[0]["sha"], commits[0]["commit"]["committer"]["date"]
    for old in os.listdir(HERE):
        if old.endswith(".xml") or old.endswith(".dtd"):
            os.remove(os.path.join(HERE, old))
    n = 0
    for f in files:
        if f["type"] != "file":
            continue
        with open(os.path.join(HERE, f["name"]), "wb") as out:
            out.write(urllib.request.urlopen(f["download_url"]).read())
        n += 1
    with open(os.path.join(HERE, "LENSFUN-VERSION.txt"), "w", encoding="utf-8", newline="\n") as v:
        v.write("lensfun database snapshot\n"
                "source: https://github.com/lensfun/lensfun/tree/master/data/db\n"
                f"upstream commit: {sha}\nupstream date: {date}\n"
                f"downloaded: {datetime.date.today().isoformat()}\nfiles: {n}\n"
                "licence: CC BY-SA 3.0, see LICENSE.txt in this folder / THIRD-PARTY-NOTICES.md\n")
    print(f"{n} fisiere, commit {sha[:10]} din {date}")


if __name__ == "__main__":
    main()
