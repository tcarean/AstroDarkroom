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

// RatioRun.cs - modul 2 cap-coada: doua mastere -> atribuire + text + doua harti.
//
// Incarca prin ImageLoader.Load, nu LoadImage, fiindca modul are nevoie si de headere:
// STACKCNT si DATE-OBS sunt singurul loc din raport in care se vede daca cele doua stive
// chiar pot fi din doua orientari diferite. Lipsa lor nu e eroare - un raw n-are header
// FITS - si atunci raportul scrie "necunoscut".

using System.IO;

namespace FlatCheck
{
    internal static class RatioRun
    {
        internal static RunOutcome<RatioResult> Execute(string pathA, string pathB, TextWriter report)
        {
            var fa = ImageLoader.Load(pathA);
            var fb = ImageLoader.Load(pathB);

            var r = RatioAnalyzer.Analyze(fa.Image, fb.Image);
            r.StackCountA = FitsReader.HInt(fa.Header, "STACKCNT", 0);
            r.StackCountB = FitsReader.HInt(fb.Header, "STACKCNT", 0);
            r.DateObsA = FitsReader.HStr(fa.Header, "DATE-OBS", null);
            r.DateObsB = FitsReader.HStr(fb.Header, "DATE-OBS", null);

            RatioReport.Write(r, pathA, pathB, report);

            // Harta principala e reziduul partii fixe pe aparat, fiindca ea e cea care
            // se potriveste cu verdictul. A doua e A/B, pentru forma gradientului extern.
            return new RunOutcome<RatioResult>(
                r,
                PngWriter.EncodeResidualMap(r.Sensor.Residual),
                PngWriter.EncodeResidualMap(r.World));
        }
    }
}
