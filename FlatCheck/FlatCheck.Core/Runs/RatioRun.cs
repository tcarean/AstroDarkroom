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

// RatioRun.cs - modul 2 cap-coada: doua fisiere -> cifre + text + harta.

using System.IO;

namespace FlatCheck
{
    internal static class RatioRun
    {
        internal static RunOutcome<RatioResult> Execute(string pathA, string pathB, TextWriter report)
        {
            var r = RatioAnalyzer.Analyze(ImageLoader.LoadImage(pathA), ImageLoader.LoadImage(pathB));
            RatioReport.Write(r, pathA, pathB, report);
            return new RunOutcome<RatioResult>(r, PngWriter.EncodeResidualMap(r.Ratio));
        }
    }
}
