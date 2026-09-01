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

// RatioReport.cs - RatioResult -> textul din consola. Doar formatare.

using System.IO;

namespace FlatCheck
{
    internal static class RatioReport
    {
        internal static void Write(RatioResult r, string pathA, string pathB, TextWriter w)
        {
            w.WriteLine($"\n=== TEST ROTATIE 180: {pathA}  /  {pathB} ===");
            w.WriteLine("    Vignetarea si praful sunt fixe pe senzor -> se anuleaza in raport.");
            w.WriteLine("    Orice gradient extern se inverseaza -> apare aici, la ~2x amplitudine.");

            w.WriteLine($"\n    structura in raport A/B : {r.PtpPct,5:F2} %   [{r.PtpVerdict}]");
            w.WriteLine($"    imprastiere 4 colturi   : {r.CornerSpreadPct,5:F2} %   [{r.CornerVerdict}]");
            w.WriteLine("    (eroarea reala in fiecare master e ~jumatate din cifrele astea)");

            w.WriteLine($"\n  VERDICT: {r.Verdict}");
            w.WriteLine(r.Verdict == "OK"
                ? "    Setup-ul de flats e curat. Poti construi biblioteca pe el."
                : "    Setup-ul contamineaza flat-ul. Repara sursa inainte de biblioteca.");
        }
    }
}
