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
                    PassepartoutCm = OptionalNumber(Passepartout, "passepartout-ul", 0, 100, allowZero: true) ?? D.PassepartoutCm,
                    FrameCm = OptionalNumber(Frame, "rama", 0, 100, allowZero: true) ?? D.FrameCm,
                };
                // Regulile intre parametri sunt ale nucleului; aici doar se arata mesajul.
                error = p.Validate();
                return error == null ? p : null;
            }
            catch (FormatException e)
            {
                error = e.Message;
                return null;
            }
        }

        static double? OptionalNumber(string text, string what, double lo, double hi, bool allowZero = false)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            if (!double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
                throw new FormatException($"{what}: '{text}' nu e un numar (foloseste punctul ca separator zecimal)");
            bool tooLow = allowZero ? v < lo : v <= lo;
            if (tooLow || v > hi) throw new FormatException($"{what}: {text} e in afara intervalului {(allowZero ? "[" : "(")}{Fmt.F1(lo)}, {Fmt.F1(hi)}]");
            return v;
        }

        /// <summary>
        /// Textele de ajutor ale campurilor - aceleasi in tooltip si pe eticheta. Paragrafe
        /// despartite de o linie goala: un TextBlock respecta '\n', iar un singur bloc de
        /// 900 de caractere nu se citeste. Un test cere cel putin doua paragrafe fiecaruia.
        /// </summary>
        public string DistanceHelp =>
            "De la cati metri se va privi printul. Testul de marime: steaua (FWHM x pasul pixelului pe hartie) "
            + "trebuie sa fie punct de la distanta asta - ochiul desface ~0.29 mm la 1 m. Cu o distanta data "
            + "(hol, birou, canapea) marimile se despart si tabelul recomanda un rand."
            + "\n\n"
            + "Fara stele, distanta da pragul grilei de pixeli, 25.4 / (0.29 x d) dpi, in locul conventiei de 180."
            + "\n\n"
            + "Gol = 1.5 x diagonala fiecarei marimi, distanta naturala de la care se priveste un tablou; atunci "
            + "\"punct de la\" si distanta cresc amandoua cu latimea, verdictul e acelasi pe fiecare rand si nu se "
            + "recomanda nicio marime - o alege peretele."
            + "\n\n"
            + "1.5 vine din regula de print si inramare \"1-2 x diagonala\": 1 x e distanta cu perspectiva "
            + "obiectivului (focala normala = diagonala cadrului), 2 x e practica de galerie; la 1.5 x tabloul "
            + "subintinde ~37 de grade si se cuprinde intreg fara sa misti capul.";

        public string AspectHelp =>
            "Raportul latime:inaltime al printului, ex. 50:35 sau 50x35. Gol = raportul nativ al imaginii, fara decupaj - "
            + "asa se trimite la un laborator fine-art, care printeaza la centimetru, orice dimensiune; rama gata facuta "
            + "nu constrange nici ea, passepartout-ul absoarbe diferenta."
            + "\n\n"
            + "Completeaza-l doar pentru un laborator cu formate "
            + "fixe (20x30, 35x50, 50x70...): raportul spune cat se taie si pe ce latura, ca sa alegi tu cadrul, nu ei.";

        public string PassepartoutHelp =>
            "Latimea benzii de carton dintre imagine si rama, pe fiecare latura. Regula inramatorilor: "
            + "10-15 % din latimea imaginii si cel putin dublul latimii ramei - sub asta rama strange imaginea."
            + "\n\n"
            + "0 = fara passepartout. Galeriile fac latura de jos cu ~1 cm mai lata.";

        public string FrameHelp =>
            "Latimea profilului ramei vazuta din fata, pe fiecare latura. Pentru un print de 60-80 cm: 3-4 cm, "
            + "neagra mata; o rama mai lata cere un passepartout pe masura (cel putin dublul ei)."
            + "\n\n"
            + "Intra doar in marimea exterioara a tabloului.";

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
