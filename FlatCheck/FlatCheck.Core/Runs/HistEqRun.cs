// SPDX-License-Identifier: GPL-3.0-or-later
//
// Copyright (C) 2026 Tudor Carean
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

// HistEqRun.cs - modul 5 cap-coada: fisier -> rastere egalizate + text + PNG.
//
// Png e compozitul, singura vedere pe care o scrie consola. Celelalte - cate un gri pe
// pozitie si mozaicul - le encodeaza fereastra la cerere, prin cele doua functii de mai
// jos, si le tine in cache pe chip: sunt pana la cinci imagini de 1500x1000, si a le
// encoda pe toate dinainte e exact problema pentru care exista cache-ul hartii.
// RunOutcome.SecondaryPng ramane null aici.

using System.IO;

namespace FlatCheck
{
    internal static class HistEqRun
    {
        internal static RunOutcome<HistEqResult> Execute(string path, TextWriter report)
        {
            LoadedFrame f = ImageLoader.Load(path);
            var r = HistEqAnalyzer.Analyze(f.Image, f.Cfa);
            HistEqReport.Write(r, path, report);
            return new RunOutcome<HistEqResult>(r, EncodeComposite(r));
        }

        internal static byte[] EncodeComposite(HistEqResult r)
            => PngWriter.Encode(r.CompositeRgb, r.W, r.H);

        internal static byte[] EncodeChannel(HistEqResult r, int i)
            => PngWriter.EncodeGrey(r.Channels[i].Equalised, r.W, r.H);

        /// <summary>Null pe mono: acolo nu exista un mozaic separat de canal.</summary>
        internal static byte[] EncodeMosaic(HistEqResult r)
            => r.Mosaic == null ? null : PngWriter.EncodeGrey(r.Mosaic, r.W, r.H);
    }
}
