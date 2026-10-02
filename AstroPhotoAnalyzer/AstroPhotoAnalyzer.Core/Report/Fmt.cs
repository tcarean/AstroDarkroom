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

// Fmt.cs - formatarea numerelor din raport, cu cultura invarianta EXPLICIT.
//
// Consola are InvariantGlobalization; fereastra nu poate (WPF crapa fara date de
// cultura) si fixeaza cultura la pornire. Daca vreun numar ar trece prin cultura
// curenta, "0.24" ar putea deveni "0,24" pe un Windows romanesc si Raport.txt n-ar mai
// fi identic cu iesirea consolei. Toate numerele din raport trec pe aici.

using System.Globalization;

namespace AstroPhotoAnalyzer
{
    internal static class Fmt
    {
        static readonly CultureInfo I = CultureInfo.InvariantCulture;

        internal static string F0(double v) => double.IsNaN(v) ? "n/a" : v.ToString("0", I);
        internal static string F1(double v) => double.IsNaN(v) ? "n/a" : v.ToString("0.0", I);
        internal static string F2(double v) => double.IsNaN(v) ? "n/a" : v.ToString("0.00", I);
        internal static string F3(double v) => double.IsNaN(v) ? "n/a" : v.ToString("0.000", I);
        internal static string F4(double v) => double.IsNaN(v) ? "n/a" : v.ToString("0.0000", I);

        /// <summary>Cu semn: "+1.6", "-9.9", "0.0".</summary>
        internal static string S1(double v) => double.IsNaN(v) ? "n/a" : v.ToString("+0.0;-0.0;0.0", I);

        /// <summary>
        /// Un nivel de fundal (0..1): doua zecimale pe grila ("0.17"), trei cand le are
        /// ("0.162", tinta exacta; "0.172", nivelul masurat). Cu F2 tinta exacta si randul
        /// grilei ar fi parut acelasi numar.
        /// </summary>
        internal static string Level(double v) => double.IsNaN(v) ? "n/a" : v.ToString("0.00#", I);

        /// <summary>
        /// Un Dmax: doua zecimale cand le are, cel putin una - "2.0", "1.5", "2.35". Cu F1,
        /// --dmax 2.35 aparea "Dmax 2.4" si raportul parea sa fi masurat alta hartie.
        /// </summary>
        internal static string Dmax(double v) => double.IsNaN(v) ? "n/a" : v.ToString("0.0#", I);

        internal static string Pct(double v) => F1(v) + " %";
        internal static string Mb(double v) => F1(v) + " MB";
        internal static string Int(long v) => v.ToString(I);

        /// <summary>"5938 x 3888".</summary>
        internal static string Dim(int w, int h) => Int(w) + " x " + Int(h);
    }
}
