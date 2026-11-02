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

        // The ICC profile the Dmax was taken from, the one the top bar shows (R-155): it is
        // dropped the moment the field shows another text than the one it wrote, or a medium is
        // chosen, so the bar never names a profile the next run would not use.
        IccProfileRow _profile;

        public IReadOnlyList<string> Media { get; } = Parameters.MediumNames;

        /// <summary>
        /// Choosing a medium writes its Dmax into the Dmax field, over whatever was there, so
        /// that the user sees which Dmax the analysis will use. The value is the core's (R-020).
        /// The order matters: code that sets both sets the medium first, then the Dmax. A
        /// profile in use, readable or not, is dropped (R-155).
        /// </summary>
        public string Medium
        {
            get => _medium;
            set
            {
                if (Set(ref _medium, value) && Media.Contains(value, StringComparer.OrdinalIgnoreCase))
                {
                    Profile = null;
                    Dmax = DmaxTextOf(value);
                }
            }
        }

        /// <summary>
        /// Filled with the chosen medium's Dmax; the user may change it. Empty = the medium's.
        /// A text other than the one the profile in use wrote drops that profile (R-155); one
        /// that could not be read wrote none and stays until a medium or "Scoate".
        /// </summary>
        public string Dmax
        {
            get => _dmax;
            set
            {
                if (Set(ref _dmax, value) && _profile != null && _profile.Readable && (value ?? "").Trim() != _profile.DmaxText)
                    Profile = null;
            }
        }

        /// <summary>
        /// The ICC profile in use, as the top bar shows it (R-155): the one "Foloseste" chose or
        /// the settings restored (R-156), readable or not; null when there is none. Set only
        /// here: through <see cref="UseProfile"/>, <see cref="RestoreProfile"/>,
        /// <see cref="ClearProfile"/>, a typed Dmax or a chosen medium.
        /// </summary>
        public IccProfileRow Profile
        {
            get => _profile;
            private set
            {
                if (!Set(ref _profile, value)) return;
                Raise(nameof(HasProfile));
                Raise(nameof(ProfileText));
                Raise(nameof(ProfileIsProblem));
            }
        }

        public bool HasProfile => _profile != null;

        /// <summary>
        /// A profile that is set but could not be read (its file gone or broken since it was
        /// remembered, R-156): the bar shows it in red and the run does not use it.
        /// </summary>
        public bool ProfileIsProblem => _profile != null && !_profile.Readable;

        /// <summary>
        /// What the bar shows: the profile's name and the core's sentence about it, or that
        /// there is none and the medium's Dmax is used. Explanation, not diagnosis (ADR 0020):
        /// the name and the sentence are the core's.
        /// </summary>
        public string ProfileText
            => _profile == null ? Tr.Pick("(none: the Dmax of what you print on)", "(niciunul: Dmax-ul suportului ales)") : _profile.Name + " - " + _profile.Detail;

        /// <summary>
        /// The Dmax of a profile chosen in the "Din profil ICC..." window: it goes into the
        /// field, and the profile is shown in the bar and named in the report until the user
        /// types something else, chooses a medium or presses "Scoate" (R-155).
        /// </summary>
        /// <param name="profile">
        /// The row chosen; null, or one that could not be read, changes nothing.
        /// </param>
        /// <returns>
        /// True when something changed: another file than the profile in use, a profile that
        /// was not in use, or the field no longer showing the profile's Dmax; false when the
        /// profile in use was chosen again with its Dmax still in the field, or nothing was
        /// chosen, so that the caller runs nothing.
        /// </returns>
        public bool UseProfile(IccProfileRow profile)
        {
            if (profile == null || !profile.Readable) return false;
            bool same = _profile != null && _profile.Readable
                && string.Equals(_profile.File, profile.File, StringComparison.OrdinalIgnoreCase)
                && (Dmax ?? "").Trim() == profile.DmaxText;
            if (same) return false;
            // The profile first: the Dmax setter compares the text with the profile in use, and
            // the new profile's own text must not drop it.
            Profile = profile;
            Dmax = profile.DmaxText;
            return true;
        }

        /// <summary>
        /// The profile the settings remembered (R-156), at the start: readable, it is applied as
        /// if chosen; not readable (its file gone or broken), it is shown with its reason and
        /// not used, the field keeping the medium's Dmax.
        /// </summary>
        /// <param name="profile">The row as the core read the remembered file; null changes nothing.</param>
        internal void RestoreProfile(IccProfileRow profile)
        {
            if (profile == null) return;
            if (profile.Readable) UseProfile(profile);
            else Profile = profile;
        }

        /// <summary>
        /// "Scoate" (R-155): the profile is dropped and the chosen medium's Dmax goes back into
        /// the field. Nothing happens without a profile.
        /// </summary>
        public void ClearProfile()
        {
            if (_profile == null) return;
            Profile = null;
            Dmax = DmaxTextOf(Medium);
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
                // A readable profile in use always has its Dmax in the field (the setter drops
                // it otherwise); one that could not be read is shown, not used.
                string dmax = (Dmax ?? "").Trim();
                bool fromProfile = _profile != null && _profile.Readable;
                bool untouched = !fromProfile && dmax == DmaxTextOf(Medium);
                var p = new Parameters
                {
                    Medium = Parameters.MediumFromName(Medium),
                    DmaxOverride = untouched ? null : OptionalNumber(Dmax, "Dmax", 0, Thresholds.DmaxMax),
                    DmaxProfile = fromProfile ? _profile.Name : null,
                    DmaxProfileNote = fromProfile ? _profile.Note : null,
                    DmaxProfilePath = fromProfile ? _profile.File : null,
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
            if (tooLow || v > hi)
                throw new FormatException(allowZero
                    ? Tr.Pick($"{what}: {text} must be between {Fmt.F1(lo)} and {Fmt.F1(hi)}", $"{what}: {text} trebuie sa fie intre {Fmt.F1(lo)} si {Fmt.F1(hi)}")
                    : Tr.Pick($"{what}: {text} must be over {Fmt.F1(lo)} and at most {Fmt.F1(hi)}", $"{what}: {text} trebuie sa fie mai mare decat {Fmt.F1(lo)} si cel mult {Fmt.F1(hi)}"));
            return v;
        }

        /// <summary>
        /// The fields' help texts - the same in the tooltip and on the label. Paragraphs
        /// separated by an empty line: a TextBlock honours '\n', and a single block of 900
        /// characters cannot be read. A test asks at least two paragraphs of each.
        /// </summary>
        public string DistanceHelp => Tr.Pick(
            "How many metres away the print will be seen from. The program checks whether the stars look like points from this distance, "
            + $"by their width on paper (FWHM): from 1 m, the eye tells apart about {Fmt.F2(Thresholds.AcuityMmAt1m)} mm. If you give a distance "
            + "(a hallway, an office, a sofa), the sizes separate and the table recommends one."
            + "\n\n"
            + $"If the image has no stars, the distance says how many pixels per inch (dpi) are needed for the eye not to see the pixels, instead of the {Fmt.F0(Thresholds.FallbackMinDpi)} labs usually ask for."
            + "\n\n"
            + $"Empty = {Fmt.F1(Thresholds.ViewingDistanceFactor)} x the diagonal of each size, the distance a picture is usually seen from. Then, the larger "
            + "the print, the farther you look at it from, so the answer is the same for every size and none is recommended: you choose the size by the wall."
            + "\n\n"
            + $"Why {Fmt.F1(Thresholds.ViewingDistanceFactor)}: from there you take in the whole picture without moving your head, and still see the detail.",
            "De la cati metri va fi privit printul. Programul verifica daca stelele se vad ca puncte de la distanta asta, "
            + $"dupa latimea lor pe hartie (FWHM): de la 1 m, ochiul deosebeste cam {Fmt.F2(Thresholds.AcuityMmAt1m)} mm. Daca dai o distanta "
            + "(hol, birou, canapea), marimile se despart si tabelul recomanda una."
            + "\n\n"
            + $"Daca imaginea nu are stele, distanta spune de cati pixeli pe inch (dpi) e nevoie ca ochiul sa nu vada pixelii, in loc de cei {Fmt.F0(Thresholds.FallbackMinDpi)} pe care ii cer de obicei laboratoarele."
            + "\n\n"
            + $"Gol = {Fmt.F1(Thresholds.ViewingDistanceFactor)} x diagonala fiecarei marimi, distanta de la care se priveste de obicei un tablou. Atunci, cu cat "
            + "printul e mai mare, cu atat il privesti de mai departe, asa ca raspunsul e acelasi la toate marimile si nu se recomanda niciuna: marimea o alegi dupa perete."
            + "\n\n"
            + $"De ce {Fmt.F1(Thresholds.ViewingDistanceFactor)}: de la distanta asta cuprinzi tabloul intreg fara sa misti capul, dar vezi inca detaliile.");

        public string AspectHelp => Tr.Pick(
            "The print's proportions, width:height, for example 50:35 or 50x35. Empty = the image's proportions, nothing cut. That is how you send it "
            + "to a fine-art lab, which prints any size, to the centimetre; no ready-made frame gets in the way either, because the passepartout covers the difference."
            + "\n\n"
            + "Fill it in only for a lab with fixed formats (20x30, 35x50, 50x70...): you see how much is cut and from which side, so that you choose what stays, not the lab.",
            "Proportia printului, latime:inaltime, de exemplu 50:35 sau 50x35. Gol = proportia imaginii, fara nimic taiat. Asa trimiti "
            + "la un laborator fine-art, care printeaza la orice marime, la centimetru; nici o rama gata facuta nu te incurca, pentru ca passepartout-ul acopera diferenta."
            + "\n\n"
            + "Completeaza campul doar pentru un laborator cu formate fixe (20x30, 35x50, 50x70...): vezi cat se taie si din ce parte, ca sa alegi tu ce ramane, nu laboratorul.");

        public string TargetBgHelp => Tr.Pick(
            $"One more target background for the simulation of the Tones tab to try, besides its usual ones, from {Fmt.Level(D.SimulationTargets.Min())} to {Fmt.Level(D.SimulationTargets.Max())}. "
            + $"Write a number over 0 and under 1; it is rounded to {Parameters.TargetBackgroundDecimals} decimals, and its row appears in the table marked \"(given)\". Empty = the usual ones only."
            + "\n\n"
            + "The background level is the middle value of the green channel over the whole image (the sky fills most of the frame). The simulation lifts the tones "
            + "with a simple curve (gamma) until the background reaches the target, then measures the dust and the sky again. It is not Siril's stretch, "
            + "but the background ends up in the same place."
            + "\n\n"
            + $"On screen, a sky at {Fmt.F2(Thresholds.ScreenBackgroundLo)}-{Fmt.F2(Thresholds.ScreenBackgroundHi)} looks good; "
            + "on paper, everything darker than its black prints as the same black, so a print wants the sky at "
            + $"{Fmt.F2(Thresholds.PrintBackgroundLo)}-{Fmt.F2(Thresholds.PrintBackgroundHi)}. The report recommends the lowest level "
            + $"at which the dust stays over paper black and the sky reaches L* {Fmt.F0(Thresholds.LowSeparationLstar)} or above; this field does not change the recommendation.",
            $"Un fundal-tinta in plus, pe care sa-l incerce simularea din tabul Tonuri, pe langa cele obisnuite, de la {Fmt.Level(D.SimulationTargets.Min())} la {Fmt.Level(D.SimulationTargets.Max())}. "
            + $"Scrie un numar mai mare decat 0 si mai mic decat 1; se rotunjeste la {Parameters.TargetBackgroundDecimals} zecimale, iar randul lui apare in tabel cu semnul \"(dat)\". Gol = doar cele obisnuite."
            + "\n\n"
            + "Nivelul fundalului e valoarea din mijloc a canalului verde pe toata imaginea (cerul ocupa cea mai mare parte din cadru). Simularea ridica tonurile "
            + "cu o curba simpla (gamma) pana cand fundalul ajunge la tinta, apoi masoara din nou praful si cerul. Nu e stretch-ul din Siril, "
            + "dar fundalul ajunge in acelasi loc."
            + "\n\n"
            + $"Pe ecran, un cer la {Fmt.F2(Thresholds.ScreenBackgroundLo)}-{Fmt.F2(Thresholds.ScreenBackgroundHi)} arata bine; "
            + "pe hartie, tot ce e mai intunecat decat negrul ei iese la fel de negru, asa ca un print vrea cerul la "
            + $"{Fmt.F2(Thresholds.PrintBackgroundLo)}-{Fmt.F2(Thresholds.PrintBackgroundHi)}. Raportul recomanda cel mai mic nivel "
            + $"la care praful ramane deasupra negrului hartiei si cerul ajunge la L* {Fmt.F0(Thresholds.LowSeparationLstar)} sau mai sus; campul acesta nu schimba recomandarea.");

        public string WcsHelp => Tr.Pick(
            "The path of a FITS with sky coordinates (plate-solved; in Siril: Image Information > Plate solving), of the same width and height as the analysed image. "
            + "From it the program learns where on the sky each pixel is and, with the Lynds catalogue, recognizes the dust clouds of the frame by name; "
            + "the dust and the sky are then measured on those clouds."
            + "\n\n"
            + "A TIFF or a PNG exported from Siril has no coordinates; the stack it came from has them, so you can give that here. "
            + "Empty = the coordinates of the analysed file, if it is a plate-solved FITS; otherwise the dust and the sky are found without the catalogue, "
            + "and the Tones tab says so in red."
            + "\n\n"
            + "The size must be the same: if you cropped or resized the image after the stack, the clouds no longer fall where they should.",
            "Calea unui FITS cu coordonate pe cer (plate-solved; in Siril: Image Information > Plate solving), de aceeasi latime si inaltime ca imaginea analizata. "
            + "Din el programul afla unde pe cer e fiecare pixel si, cu catalogul Lynds, recunoaste pe nume norii de praf din cadru; "
            + "praful si cerul se masoara apoi pe acesti nori."
            + "\n\n"
            + "Un TIFF sau un PNG exportat din Siril nu are coordonatele; le are stack-ul din care a iesit, asa ca il poti da aici. "
            + "Gol = coordonatele fisierului analizat, daca e un FITS plate-solved; altfel praful si cerul se cauta fara catalog, "
            + "iar tabul Tonuri o spune cu rosu."
            + "\n\n"
            + "Marimea trebuie sa fie aceeasi: daca ai decupat sau redimensionat imaginea dupa stack, norii nu mai cad unde trebuie.");

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
            throw new FormatException(Tr.Pick($"proportions: '{text}' is not of the form width:height (e.g. 50:35)", $"proportia: '{text}' nu e de forma latime:inaltime (ex. 50:35)"));
        }
    }
}
