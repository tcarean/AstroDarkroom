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

// ParameterFields.cs - the text fields of the parameter panel and their translation into
// Parameters. The equivalent of the console's Options: syntax checks only; what the values
// mean is the core's to decide.
//
// The fields are strings, not numbers, for a reason of interface: a TextBox bound to a double
// refuses to let the user clear it to type something else, and colours it red halfway through
// typing. They are parsed once, at "Analizeaza".

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AstroPhotoAnalyzer.Wpf
{
    /// <summary>
    /// The parameter card's fields as text (R-024), the help text of each, and
    /// <see cref="TryBuild"/>, which turns them into a <see cref="Parameters"/> or a message.
    /// </summary>
    public sealed class ParameterFields : ObservableObject
    {
        static readonly Parameters D = new();

        string _medium = Parameters.NameOf(D.Medium);
        string _dmax = Fmt.Dmax(D.Dmax);
        string _distance = "";
        string _sizes = string.Join(", ", D.CandidateWidthsCm.Select(Fmt.F0));
        string _aspect = "";
        string _targetBg = "";
        string _passepartout = Fmt.F0(D.PassepartoutCm);
        string _frame = Fmt.F0(D.FrameCm);
        string _wcsPath = "";

        // The ICC profile the Dmax was taken from, and the text put in the field then: the name
        // reaches the report only while the field still shows that text (R-132).
        string _dmaxProfile, _dmaxProfileText, _dmaxProfileNote, _dmaxProfilePath;

        public IReadOnlyList<string> Media { get; } = Parameters.MediumNames;

        /// <summary>
        /// Choosing a medium writes its Dmax into the Dmax field, over whatever was there, so
        /// that the user sees which Dmax the analysis will use. The value is the core's (R-020).
        /// The order matters: code that sets both sets the medium first, then the Dmax.
        /// </summary>
        public string Medium
        {
            get => _medium;
            set
            {
                if (Set(ref _medium, value) && Media.Contains(value, StringComparer.OrdinalIgnoreCase))
                {
                    Dmax = DmaxTextOf(value);
                    _dmaxProfile = _dmaxProfileText = _dmaxProfileNote = _dmaxProfilePath = null;
                }
            }
        }

        /// <summary>
        /// Filled with the chosen medium's Dmax; the user may change it. Empty = the medium's.
        /// </summary>
        public string Dmax { get => _dmax; set => Set(ref _dmax, value); }

        /// <summary>
        /// The Dmax of a profile chosen in the "Din profil ICC..." window: it goes into the
        /// field, and the profile's name accompanies it in the report until the user types
        /// something else or chooses a medium.
        /// </summary>
        /// <param name="profile">
        /// The row chosen; null, or one that could not be read, changes nothing.
        /// </param>
        public void UseProfile(IccProfileRow profile)
        {
            if (profile == null || !profile.Readable) return;
            Dmax = profile.DmaxText;
            _dmaxProfile = profile.Name;
            _dmaxProfileText = profile.DmaxText;
            _dmaxProfileNote = profile.Note;
            _dmaxProfilePath = profile.File;
        }

        /// <summary>
        /// The text the Dmax field shows for a medium: the core's Dmax, as Fmt.Dmax writes it.
        /// </summary>
        /// <param name="medium">
        /// The medium's name, as in <see cref="Media"/>, any case; null or an unknown name throws
        /// ArgumentException (Parameters.MediumFromName), which <see cref="TryBuild"/> does not
        /// catch.
        /// </param>
        /// <returns>"2.4" for baryta, and so on.</returns>
        static string DmaxTextOf(string medium) => Fmt.Dmax(Parameters.DmaxOf(Parameters.MediumFromName(medium)));
        public string Distance { get => _distance; set => Set(ref _distance, value); }
        public string Sizes { get => _sizes; set => Set(ref _sizes, value); }
        public string Aspect { get => _aspect; set => Set(ref _aspect, value); }
        public string TargetBg { get => _targetBg; set => Set(ref _targetBg, value); }
        public string Passepartout { get => _passepartout; set => Set(ref _passepartout, value); }
        public string Frame { get => _frame; set => Set(ref _frame, value); }

        /// <summary>
        /// The FITS whose WCS is lent to the image (R-027); empty = the file's own WCS, if it has
        /// one.
        /// </summary>
        public string WcsPath { get => _wcsPath; set => Set(ref _wcsPath, value); }

        /// <summary>
        /// The parameters, or null with a message in <paramref name="error"/>: the first field
        /// that is not a number or is out of its range, or the core's own checks
        /// (Parameters.Validate: NaN and infinity, the rule between parameters).
        /// </summary>
        /// <param name="error">
        /// The message for the user, in the interface language; null when the parameters are returned.
        /// </param>
        /// <returns>
        /// The parameters, an empty field left to the core's default, except the widths, which
        /// an empty field refuses; null on a message.
        /// </returns>
        internal Parameters TryBuild(out string error)
        {
            error = null;
            try
            {
                // A field that still shows the text the medium put there is not a given Dmax: it
                // stays the core's, with all its decimals, not a copy rounded for display (R-024).
                string dmax = (Dmax ?? "").Trim();
                bool fromProfile = _dmaxProfile != null && dmax == _dmaxProfileText;
                bool untouched = !fromProfile && dmax == DmaxTextOf(Medium);
                var p = new Parameters
                {
                    Medium = Parameters.MediumFromName(Medium),
                    DmaxOverride = untouched ? null : OptionalNumber(Dmax, "Dmax", 0, Thresholds.DmaxMax),
                    DmaxProfile = fromProfile ? _dmaxProfile : null,
                    DmaxProfileNote = fromProfile ? _dmaxProfileNote : null,
                    DmaxProfilePath = fromProfile ? _dmaxProfilePath : null,
                    ViewingDistanceM = OptionalNumber(Distance, Tr.Pick("distance", "distanta"), 0, 100),
                    CandidateWidthsCm = SizesOf(Sizes),
                    TargetAspect = AspectOf(Aspect),
                    TargetBackground = TargetBgOf(TargetBg),
                    PassepartoutCm = OptionalNumber(Passepartout, Tr.Pick("passepartout", "passepartout-ul"), 0, 100, allowZero: true) ?? D.PassepartoutCm,
                    FrameCm = OptionalNumber(Frame, Tr.Pick("frame", "rama"), 0, 100, allowZero: true) ?? D.FrameCm,
                    WcsPath = string.IsNullOrWhiteSpace(WcsPath) ? null : WcsPath.Trim(),
                };
                // The rules between parameters are the core's; here only the message is shown.
                error = p.Validate();
                return error == null ? p : null;
            }
            catch (FormatException e)
            {
                error = e.Message;
                return null;
            }
        }

        /// <summary>
        /// The target background from its field: empty = none; a text that is not a number gives
        /// the fields' message; the range and the rounding are judged by the core, with its
        /// message (Parameters.TargetBackgroundError, R-023), so that the window refuses what the
        /// console refuses, in the same words.
        /// </summary>
        /// <param name="text">The field's text; null or blank is allowed.</param>
        /// <returns>
        /// The level as typed, 0..1, or null for an empty field; a bad text throws FormatException.
        /// </returns>
        static double? TargetBgOf(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            double v = ParseNumber(text, Tr.Pick("target background", "fundalul-tinta"));
            string error = Parameters.TargetBackgroundError(v);
            if (error != null) throw new FormatException(error);
            return v;
        }

        /// <summary>
        /// The number in a field, with the invariant culture (R-024); a text that is not a number
        /// gives the fields' message, with the point as the separator. One place for every field.
        /// "NaN", "Infinity" and "-Infinity" read too, as those values; the range checks of the
        /// callers refuse the infinities they bound, the target background's refuses NaN, and
        /// what passes them is refused by the core's Parameters.Validate, which
        /// <see cref="TryBuild"/> calls (R-024).
        /// </summary>
        /// <param name="text">The field's text, trimmed here; not null.</param>
        /// <param name="what">
        /// The field's name for the message, in the interface language ("Dmax", "distance").
        /// </param>
        /// <returns>
        /// The number; a text that does not parse throws FormatException with the message.
        /// </returns>
        static double ParseNumber(string text, string what)
        {
            if (!double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
                throw new FormatException(Tr.Pick($"{what}: '{text}' is not a number (use the point as the decimal separator)", $"{what}: '{text}' nu e un numar (foloseste punctul ca separator zecimal)"));
            return v;
        }

        /// <summary>
        /// An optional number in a range: empty = none; outside the range, a FormatException
        /// with the range in the message. NaN passes both comparisons and is returned;
        /// Parameters.Validate refuses it (R-024).
        /// </summary>
        /// <param name="text">The field's text; null or blank gives null.</param>
        /// <param name="what">The field's name for the message, in the interface language.</param>
        /// <param name="lo">
        /// The lower bound; excluded, or included with <paramref name="allowZero"/>.
        /// </param>
        /// <param name="hi">The upper bound, included.</param>
        /// <param name="allowZero">
        /// True when <paramref name="lo"/> itself (0 means "none") is allowed.
        /// </param>
        /// <returns>The number, or null for an empty field.</returns>
        static double? OptionalNumber(string text, string what, double lo, double hi, bool allowZero = false)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            double v = ParseNumber(text, what);
            bool tooLow = allowZero ? v < lo : v <= lo;
            if (tooLow || v > hi) throw new FormatException(Tr.Pick($"{what}: {text} is outside the interval {(allowZero ? "[" : "(")}{Fmt.F1(lo)}, {Fmt.F1(hi)}]", $"{what}: {text} e in afara intervalului {(allowZero ? "[" : "(")}{Fmt.F1(lo)}, {Fmt.F1(hi)}]"));
            return v;
        }

        /// <summary>
        /// The fields' help texts - the same in the tooltip and on the label. Paragraphs
        /// separated by an empty line: a TextBlock honours '\n', and a single block of 900
        /// characters cannot be read. A test asks at least two paragraphs of each.
        /// </summary>
        public string DistanceHelp => Tr.Pick(
            "From how many metres the print will be seen. The size test: the star (FWHM x the pixel pitch on paper) "
            + "must be a point from this distance - the eye resolves ~0.29 mm at 1 m. With a given distance "
            + "(a hallway, an office, a sofa) the sizes separate and the table recommends a row."
            + "\n\n"
            + "Without stars, the distance gives the threshold of the pixel grid, 25.4 / (0.29 x d) dpi, instead of the convention of 180."
            + "\n\n"
            + "Empty = 1.5 x the diagonal of each size, the natural distance from which a picture is seen; then "
            + "\"point from\" and the distance both grow with the width, the verdict is the same on every row and no "
            + "size is recommended - the wall chooses it."
            + "\n\n"
            + "1.5 comes from the print and framing rule \"1-2 x the diagonal\": 1 x is the distance with the perspective "
            + "of the lens (a normal focal length = the frame's diagonal), 2 x is gallery practice; at 1.5 x the picture "
            + "subtends ~37 degrees and is taken in whole without moving the head.",
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
            + "subintinde ~37 de grade si se cuprinde intreg fara sa misti capul.");

        public string AspectHelp => Tr.Pick(
            "The width:height ratio of the print, e.g. 50:35 or 50x35. Empty = the image's native ratio, no crop - "
            + "that is how it goes to a fine-art lab, which prints to the centimetre, any size; a ready-made frame "
            + "does not constrain it either, the passepartout absorbs the difference."
            + "\n\n"
            + "Fill it in only for a lab with fixed "
            + "formats (20x30, 35x50, 50x70...): the ratio says how much is cut and on which side, so that you choose the framing, not they.",
            "Raportul latime:inaltime al printului, ex. 50:35 sau 50x35. Gol = raportul nativ al imaginii, fara decupaj - "
            + "asa se trimite la un laborator fine-art, care printeaza la centimetru, orice dimensiune; rama gata facuta "
            + "nu constrange nici ea, passepartout-ul absoarbe diferenta."
            + "\n\n"
            + "Completeaza-l doar pentru un laborator cu formate "
            + "fixe (20x30, 35x50, 50x70...): raportul spune cat se taie si pe ce latura, ca sa alegi tu cadrul, nu ei.");

        public string TargetBgHelp => Tr.Pick(
            $"One more level for the simulation to try, strictly between 0 and 1 and rounded to {Parameters.TargetBackgroundDecimals} decimals, besides its grid "
            + $"{Fmt.Level(D.SimulationTargets.Min())}..{Fmt.Level(D.SimulationTargets.Max())}: its row appears in the table marked "
            + "\"(given)\" and is the one the print tile of Preview shows. Empty = the grid only, and Preview takes the recommended row."
            + "\n\n"
            + "The background level = the median of the green channel over the whole image (the sky dominates the frame). The simulation applies "
            + "gamma = ln(target) / ln(background) on every channel and re-measures the dust and the sky. It is a simulation, not Siril's stretch, "
            + "but the background ends up in the same place."
            + "\n\n"
            + $"On screen a sky at {Fmt.F2(Thresholds.ScreenBackgroundLo)}-{Fmt.F2(Thresholds.ScreenBackgroundHi)} looks good; "
            + "on paper everything under its black prints as the same black, and a print wants the sky at "
            + $"{Fmt.F2(Thresholds.PrintBackgroundLo)}-{Fmt.F2(Thresholds.PrintBackgroundHi)}. The report's recommendation is the minimum level "
            + $"at which the dust still stays over paper black and the sky at L* {Fmt.F0(Thresholds.LowSeparationLstar)} or above; this field does not change it.",
            $"Un nivel in plus pe care sa-l incerce simularea, strict intre 0 si 1 si rotunjit la {Parameters.TargetBackgroundDecimals} zecimale, pe langa grila ei "
            + $"{Fmt.Level(D.SimulationTargets.Min())}..{Fmt.Level(D.SimulationTargets.Max())}: randul lui apare in tabel marcat "
            + "\"(dat)\" si e cel pe care il arata dala de print din Preview. Gol = doar grila, si Preview-ul ia randul recomandat."
            + "\n\n"
            + "Nivelul fundalului = mediana canalului verde pe toata imaginea (cerul domina cadrul). Simularea aplica "
            + "gamma = ln(tinta) / ln(fundal) pe toate canalele si remasoara praful si cerul. E o simulare, nu stretch-ul lui Siril, "
            + "dar fundalul ajunge in acelasi loc."
            + "\n\n"
            + $"Pe ecran un cer la {Fmt.F2(Thresholds.ScreenBackgroundLo)}-{Fmt.F2(Thresholds.ScreenBackgroundHi)} arata bine; "
            + "pe hartie tot ce e sub negrul ei se printeaza cu acelasi negru, si un print vrea cerul la "
            + $"{Fmt.F2(Thresholds.PrintBackgroundLo)}-{Fmt.F2(Thresholds.PrintBackgroundHi)}. Recomandarea din raport e nivelul minim "
            + $"la care praful mai ramane deasupra negrului hartiei si cerul la L* {Fmt.F0(Thresholds.LowSeparationLstar)} sau peste; campul asta nu o schimba.");

        public string WcsHelp => Tr.Pick(
            "The path of a plate-solved FITS (Siril: Image Information > Plate solving, or a solved stack) with the same "
            + "width and height as the analysed image. Its header says where on the sky each pixel is, and with the Lynds catalogue "
            + "the tool recognizes the dust clouds of the frame by name: the dust and the sky are measured on them, not on a fraction of the frame."
            + "\n\n"
            + "A TIFF or PNG exported from Siril does not carry the WCS; the stack it came from has it, and this is how it is lent. "
            + "Empty = the WCS of the analysed file, if it is a plate-solved FITS; otherwise the dust and the sky are measured without the catalogue, "
            + "and the Tones tab says so in red."
            + "\n\n"
            + "The size must be identical: a crop or a resize between the stack and the export moves the clouds.",
            "Calea unui FITS plate-solved (Siril: Image Information > Plate solving, sau un stack rezolvat) cu aceeasi "
            + "latime si inaltime ca imaginea analizata. Antetul lui spune unde pe cer e fiecare pixel, si cu catalogul Lynds "
            + "unealta recunoaste norii de praf din cadru pe nume: praful si cerul se masoara pe ei, nu pe o fractie din cadru."
            + "\n\n"
            + "Un TIFF sau PNG exportat din Siril nu poarta WCS-ul; stack-ul din care a iesit il are, si asa se imprumuta. "
            + "Gol = WCS-ul fisierului analizat, daca e un FITS plate-solved; altfel praful si cerul se masoara fara catalog, "
            + "si tabul Tonuri o spune cu rosu."
            + "\n\n"
            + "Marimea trebuie sa fie identica: un decupaj sau o redimensionare intre stack si export muta norii.");

        public string PassepartoutHelp => Tr.Pick(
            "The width of the board between the image and the frame, on each side. The framers' rule: "
            + "10-15 % of the image's width and at least twice the frame's width - under that the frame crowds the image."
            + "\n\n"
            + "0 = no passepartout. Galleries make the bottom side ~1 cm wider.",
            "Latimea benzii de carton dintre imagine si rama, pe fiecare latura. Regula inramatorilor: "
            + "10-15 % din latimea imaginii si cel putin dublul latimii ramei - sub asta rama strange imaginea."
            + "\n\n"
            + "0 = fara passepartout. Galeriile fac latura de jos cu ~1 cm mai lata.");

        public string FrameHelp => Tr.Pick(
            "The width of the frame's moulding seen from the front, on each side. For a print of 60-80 cm: 3-4 cm, "
            + "matt black; a wider frame needs a matching passepartout (at least twice its width)."
            + "\n\n"
            + "It enters only the outer size of the picture.",
            "Latimea profilului ramei vazuta din fata, pe fiecare latura. Pentru un print de 60-80 cm: 3-4 cm, "
            + "neagra mata; o rama mai lata cere un passepartout pe masura (cel putin dublul ei)."
            + "\n\n"
            + "Intra doar in marimea exterioara a tabloului.");

        /// <summary>
        /// The candidate widths from their field: numbers separated by commas, semicolons or
        /// spaces, each positive; a FormatException for a part that is not one, or for no part at
        /// all. NaN and Infinity pass as positive; Parameters.Validate refuses them (R-024).
        /// </summary>
        /// <param name="text">The field's text; null counts as empty.</param>
        /// <returns>The widths in cm, in the order typed, at least one.</returns>
        static double[] SizesOf(string text)
        {
            var list = new List<double>();
            foreach (string s in (text ?? "").Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) || v <= 0)
                    throw new FormatException(Tr.Pick($"widths: '{s}' is not a width in cm", $"latimile: '{s}' nu e o latime in cm"));
                list.Add(v);
            }
            if (list.Count == 0) throw new FormatException(Tr.Pick("widths: write at least one width, in cm, separated by commas", "latimile: scrie cel putin o latime, in cm, separate prin virgula"));
            return list.ToArray();
        }

        /// <summary>
        /// The target aspect from its field: two positive numbers separated by ':', 'x', 'X' or
        /// '/'; anything else is a FormatException, a side that reads as NaN or infinity
        /// included: refused here, where the typed text is at hand for the message, rather than
        /// as the 0, infinity or NaN ratio it would give (R-024, ADR 0045).
        /// </summary>
        /// <param name="text">The field's text; null or blank gives null.</param>
        /// <returns>Width over height, or null for an empty field.</returns>
        static double? AspectOf(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            string[] parts = text.Split(new[] { ':', 'x', 'X', '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2
                && double.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double w)
                && double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double h)
                && double.IsFinite(w) && double.IsFinite(h) && w > 0 && h > 0)
                return w / h;
            throw new FormatException(Tr.Pick($"ratio: '{text}' is not width:height (e.g. 50:35)", $"raportul: '{text}' nu e latime:inaltime (ex. 50:35)"));
        }
    }
}
