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

"""Minimal TIFF/EXIF reader for Nikon NEF: exposure, ISO, DateTimeOriginal, SubSecTimeOriginal.
No third-party dependency (exiftool/exifread are not installed on this machine)."""
import struct, sys, os, glob
from datetime import datetime, timedelta

TAGS = {0x829A: 'ExposureTime', 0x8827: 'ISO', 0x9003: 'DateTimeOriginal',
        0x9291: 'SubSecTimeOriginal', 0x0110: 'Model', 0x8769: 'ExifIFD',
        0x0132: 'DateTime', 0x9290: 'SubSecTime'}
TYPE_SIZE = {1: 1, 2: 1, 3: 2, 4: 4, 5: 8, 7: 1, 9: 4, 10: 8}


def _read_ifd(f, off, endian, out):
    f.seek(off)
    n, = struct.unpack(endian + 'H', f.read(2))
    for _ in range(n):
        tag, typ, cnt = struct.unpack(endian + 'HHI', f.read(8))
        val_bytes = f.read(4)
        size = TYPE_SIZE.get(typ, 1) * cnt
        if size > 4:
            voff, = struct.unpack(endian + 'I', val_bytes)
            here = f.tell()
            f.seek(voff)
            data = f.read(size)
            f.seek(here)
        else:
            data = val_bytes[:size]
        name = TAGS.get(tag)
        if name is None:
            continue
        if typ == 2:
            val = data.rstrip(b'\0').decode('ascii', 'replace')
        elif typ == 3:
            val = struct.unpack(endian + 'H' * cnt, data)[0]
        elif typ == 4:
            val = struct.unpack(endian + 'I' * cnt, data)[0]
        elif typ == 5:
            a, b = struct.unpack(endian + 'II', data[:8])
            val = a / b if b else float('nan')
        else:
            val = data
        if name == 'ExifIFD':
            _read_ifd(f, val, endian, out)
        else:
            out[name] = val


def read(path):
    out = {}
    with open(path, 'rb') as f:
        bom = f.read(2)
        endian = '<' if bom == b'II' else '>'
        magic, off0 = struct.unpack(endian + 'HI', f.read(6))
        _read_ifd(f, off0, endian, out)
    t = out.get('DateTimeOriginal')
    if t:
        dt = datetime.strptime(t, '%Y:%m:%d %H:%M:%S')
        ss = out.get('SubSecTimeOriginal')
        if ss and ss.strip().isdigit():
            dt += timedelta(seconds=int(ss.strip()) / 10 ** len(ss.strip()))
        out['dt'] = dt
    return out


if __name__ == '__main__':
    for folder in sys.argv[1:]:
        files = sorted(glob.glob(os.path.join(folder, '*.NEF')))
        print(f'== {folder}: {len(files)} NEF')
        prev = None
        for p in files:
            e = read(p)
            gap = (e['dt'] - prev).total_seconds() if prev and 'dt' in e else None
            prev = e.get('dt')
            print(f"{os.path.basename(p)} {e.get('Model','?')} exp={e.get('ExposureTime')} ISO={e.get('ISO')} "
                  f"{e.get('DateTimeOriginal')} gap={gap}")
