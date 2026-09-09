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

// LightRun.cs - modul 3 pe un singur cadru, cap-coada.

using System.IO;

namespace FlatCheck
{
    /// <summary>
    /// Rezultatul unei rulari --light. Spre deosebire de celelalte moduri, asta poate
    /// REFUZA sa raspunda: peste Th.LightFovMax grade diagonala cerul si vignetarea au
    /// aceeasi forma, deci orice cifra ar iesi ar fi cerul. Refuzul nu e un verdict prost,
    /// e absenta unui verdict - de aceea Result ramane null si nu se produce nicio cifra.
    /// </summary>
    internal sealed class LightRunOutcome
    {
        /// <summary>Campul acoperit si de unde s-a aflat - se raporteaza si la refuz.</summary>
        internal readonly SuitabilityResult Suitability;

        /// <summary>Cifrele, sau null cand testul nu se aplica.</summary>
        internal readonly LightResult Result;

        /// <summary>Harta radiala PNG, sau null cand testul nu se aplica.</summary>
        internal readonly byte[] Png;

        internal bool Refused => Result == null;

        internal LightRunOutcome(SuitabilityResult s, LightResult r, byte[] png)
        {
            Suitability = s;
            Result = r;
            Png = png;
        }
    }

    internal static class LightRun
    {
        internal static LightRunOutcome Execute(string path, double? fov, TextWriter report)
        {
            LoadedFrame f = ImageLoader.Load(path);

            // Garda INAINTE de orice calcul. Pe un camp prea larg cifra n-ar insemna
            // nimic, deci nu se produce deloc, in loc sa se produca si sa se explice
            // dedesubt - un numar tiparit e crezut, oricat de multe paranteze ai pune.
            var s = LightSuitability.Check(f.Header, fov);
            if (!s.CanRun)
            {
                LightReport.WriteTooWide(s, path, report);
                return new LightRunOutcome(s, null, null);
            }

            var r = LightAnalyzer.Analyze(f.Image);
            LightReport.Write(r, s, path, report);
            return new LightRunOutcome(s, r, PngWriter.EncodeResidualMap(r.RadialMap));
        }
    }
}
