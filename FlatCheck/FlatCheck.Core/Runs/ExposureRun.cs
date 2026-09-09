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

// ExposureRun.cs - modul 4 cap-coada: un flat brut -> nivel + verdict + text.

using System.IO;

namespace FlatCheck
{
    internal static class ExposureRun
    {
        /// <summary>
        /// NU se cheama Img.Prepare aici, si asta e tot ce deosebeste modul de celelalte
        /// trei: are nevoie de mozaicul CFA brut, la scala ADU originala. Un block-mean
        /// 2x2 ar amesteca cele patru pozitii Bayer si ar sterge exact ce se masoara.
        ///
        /// Nicio ramificatie dupa format: ImageLoader a decis deja. Un raw duce o scala
        /// exacta, un FITS duce null si atunci analizorul o ghiceste din maxim - dar
        /// calculul de dupa e acelasi, bit cu bit.
        ///
        /// Nu produce harta: modul masoara un nivel, n-are ce desena.
        /// </summary>
        internal static RunOutcome<ExposureResult> Execute(
            string path, double? sat, double? black, bool sky, TextWriter report)
        {
            LoadedFrame f = ImageLoader.Load(path);
            var r = ExposureAnalyzer.Analyze(f.Image, sat, black, sky, f.Scale);
            ExposureReport.Write(r, path, report);
            return new RunOutcome<ExposureResult>(r, null);
        }
    }
}
