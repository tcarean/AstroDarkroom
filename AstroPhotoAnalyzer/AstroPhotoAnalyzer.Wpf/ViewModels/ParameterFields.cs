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

// ParameterFields.cs - campurile de text ale panoului de parametri si traducerea lor in
// Parameters. Echivalentul lui Options din consola: doar verificare sintactica; ce
// inseamna valorile decide nucleul.
//
// Campurile sunt siruri, nu numere, dintr-un motiv de interfata: un TextBox legat la un
// double refuza sa lase omul sa stearga ca sa scrie altceva, si il coloreaza rosu la
// jumatatea tastarii. Se parseaza o data, la "Analizeaza".

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AstroPhotoAnalyzer.Wpf
{
    public sealed class ParameterFields : ObservableObject
    {
        static readonly Parameters D = new();

        string _medium = Parameters.NameOf(D.Medium);
        string _dmax = "";
        string _distance = "";
        string _sizes = string.Join(", ", D.CandidateWidthsCm.Select(Fmt.F0));
        string _aspect = "";
        string _targetBg = Fmt.F2(D.TargetBackground);
        string _passepartout = Fmt.F0(D.PassepartoutCm);
        string _frame = Fmt.F0(D.FrameCm);

        public IReadOnlyList<string> Media { get; } = new[]
        {
            Parameters.NameOf(PrintMedium.Baryta), Parameters.NameOf(PrintMedium.Canvas), Parameters.NameOf(PrintMedium.Acrylic),
        };

        public string Medium { get => _medium; set => Set(ref _medium, value); }
        public string Dmax { get => _dmax; set => Set(ref _dmax, value); }
        public string Distance { get => _distance; set => Set(ref _distance, value); }
        public string Sizes { get => _sizes; set => Set(ref _sizes, value); }
        public string Aspect { get => _aspect; set => Set(ref _aspect, value); }
        public string TargetBg { get => _targetBg; set => Set(ref _targetBg, value); }
        public string Passepartout { get => _passepartout; set => Set(ref _passepartout, value); }
        public string Frame { get => _frame; set => Set(ref _frame, value); }

        /// <summary>Parametrii, sau null cu un mesaj in <paramref name="error"/>.</summary>
        internal Parameters TryBuild(out string error)
        {
            error = null;
            try
            {
                var p = new Parameters
                {
                    Medium = Parameters.MediumFromName(Medium),
                    DmaxOverride = OptionalNumber(Dmax, "Dmax", 0, 4),
                    ViewingDistanceM = OptionalNumber(Distance, "distanta", 0, 100),
                    CandidateWidthsCm = SizesOf(Sizes),
                    TargetAspect = AspectOf(Aspect),
                    TargetBackground = OptionalNumber(TargetBg, "fundalul-tinta", 0, 1) ?? D.TargetBackground,
                    PassepartoutCm = OptionalNumber(Passepartout, "passepartout-ul", 0, 100) ?? D.PassepartoutCm,
                    FrameCm = OptionalNumber(Frame, "rama", 0, 100) ?? D.FrameCm,
                };
                return p;
            }
            catch (FormatException e)
            {
                error = e.Message;
                return null;
            }
        }

        static double? OptionalNumber(string text, string what, double lo, double hi)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            if (!double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
                throw new FormatException($"{what}: '{text}' nu e un numar (foloseste punctul ca separator zecimal)");
            if (v <= lo || v > hi) throw new FormatException($"{what}: {text} e in afara intervalului ({Fmt.F1(lo)}, {Fmt.F1(hi)}]");
            return v;
        }

        static double[] SizesOf(string text)
        {
            var list = new List<double>();
            foreach (string s in (text ?? "").Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) || v <= 0)
                    throw new FormatException($"latimile: '{s}' nu e o latime in cm");
                list.Add(v);
            }
            if (list.Count == 0) throw new FormatException("latimile: scrie cel putin o latime, in cm, separate prin virgula");
            return list.ToArray();
        }

        static double? AspectOf(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            string[] parts = text.Split(new[] { ':', 'x', 'X', '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2
                && double.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double w)
                && double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double h)
                && w > 0 && h > 0)
                return w / h;
            throw new FormatException($"raportul: '{text}' nu e latime:inaltime (ex. 50:35)");
        }
    }
}
