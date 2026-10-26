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

// The synthetic flat's report: which profile was used (the source, the lens, the focal length
// and aperture REQUESTED and those FOUND - the choice is "the nearest", so it is said), how
// deep the vignetting written is, in stops, at the points where the real masters were compared
// with the databases, and what the file does NOT contain. Plus the list of lenses for --lenses
// and the texts of the window's panel (where the Adobe profiles are, how lensfun is updated), so
// that both applications say them the same way.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using static FlatCheck.Ui;

namespace FlatCheck
{
    /// <summary>
    /// The words of <c>--synthetic</c> and <c>--lenses</c>: the report of a synthetic flat, the
    /// format and mismatch sentences the structure comparison reuses, the refusals, the lens
    /// list, and the texts of the window's profile panel.
    /// </summary>
    internal static class SyntheticReport
    {
        /// <summary>
        /// Writes the report: the reference frame, the lens and the profile used against the one
        /// requested, the model, the crop and the format, the depths written, and the notes.
        /// </summary>
        /// <param name="r">The synthetic flat built.</param>
        /// <param name="req">What was requested, and where each value came from.</param>
        /// <param name="referencePath">The reference frame's path, as printed.</param>
        /// <param name="w">Where to write.</param>
        internal static void Write(SyntheticFlatResult r, SyntheticRequest req, string referencePath, TextWriter w)
        {
            LensProfile p = r.Profile;
            w.WriteLine(T("\n=== SYNTHETIC FLAT from the lens profile ===", "\n=== FLAT SINTETIC din profilul obiectivului ==="));
            w.WriteLine(T($"    reference frame    : {referencePath}  ({r.Width} x {r.Height})",
                          $"    cadru de referinta : {referencePath}  ({r.Width} x {r.Height})"));
            w.WriteLine(T($"    lens               : {p.Lens.Name}  [{p.Lens.Where}]  source: {p.Lens.SourceLabel}",
                          $"    obiectiv           : {p.Lens.Name}  [{p.Lens.Where}]  sursa: {p.Lens.SourceLabel}"));
            w.WriteLine(T($"    profile used       : {p.Settings}   (requested: {Requested(req)})",
                          $"    profil folosit     : {p.Settings}   (cerut: {Requested(req)})"));
            string mismatch = ProfileMismatch(p, req.FocalMm, req.FStop, r.LensProfiles);
            if (mismatch != null) Indent(T("WARNING: ", "ATENTIE: ") + mismatch, w);
            w.WriteLine($"    model              : {p.Model.Describe()}");
            w.WriteLine(T($"    profile file       : {Path.GetFileName(p.File)}",
                          $"    fisier profil      : {Path.GetFileName(p.File)}"));
            w.WriteLine(T("    camera crop        : ", "    crop aparat        : ") + CropLine(r, req));
            w.WriteLine("    format             : " + FormatMatch(p.Model, r.CameraCrop, r.Width, r.Height));

            w.WriteLine(T("\n  DEPTH OF THE VIGNETTING WRITTEN (against the axis)", "\n  ADANCIMEA VIGNETARII SCRISE (fata de axa)"));
            foreach (string line in DepthLines(r)) w.WriteLine("    " + line);

            w.WriteLine();
            Indent(T("INFORMATIVE: ", "INFORMATIV: ") + Caveat(), w);
            Indent(SirilNote(), w);
        }

        /// <summary>
        /// The crop used and where it comes from; with lensfun the extrapolation too, with Adobe
        /// when the profile was calibrated on another format than yours, and "necunoscut" when the
        /// SFF was taken.
        /// </summary>
        /// <param name="r">The synthetic flat built.</param>
        /// <param name="req">The request, with the crop's source.</param>
        /// <returns>The crop and its source, or "necunoscut" with the profile's SFF.</returns>
        internal static string CropLine(SyntheticFlatResult r, SyntheticRequest req)
            => r.CameraCrop <= 0
                ? T($"unknown  ({req.CropSource}; SFF {Fmt(r.Profile.Model.CropFactor, "0.###")})",
                    $"necunoscut  ({req.CropSource}; SFF {Fmt(r.Profile.Model.CropFactor, "0.###")})")
                : $"{Fmt(r.CameraCrop, "0.###")}  ({req.CropSource})";

        /// <summary>
        /// Over 3 % between the crop and the calibration format: another sensor format, not the
        /// nominal rounding (1.5 against 1.523).
        /// </summary>
        internal const double SameFormatTolerance = 0.03;

        /// <summary>
        /// The profile against your sensor, on one row: what format it was calibrated on, what
        /// format you have, and what follows from the difference - the radius recomputed through
        /// mm when the formats differ, and how far your corner falls from the calibration corner:
        /// inside, a small extrapolation (under 10 %: the D3300 on the lensfun profile calibrated
        /// at 1.61 gives 1.06) or a LARGE one (a full frame on a DX profile, 1.5: the sixth-degree
        /// polynomial says nothing there any more). The console prints it at "format", the window
        /// shows it at the choice of lens and under the depths written; the same sentence.
        /// </summary>
        /// <param name="m">The profile's model.</param>
        /// <param name="cameraCrop">Your camera's crop factor; 0 or less when unknown.</param>
        /// <param name="w">The frame's width, pixels.</param>
        /// <param name="h">The frame's height, pixels.</param>
        /// <returns>The calibration format, yours, and the extrapolation's tail.</returns>
        internal static string FormatMatch(VignettingModel m, double cameraCrop, int w, int h)
            => FormatMatch(m, cameraCrop, cameraCrop > 0 ? m.Extrapolation(w, h, cameraCrop) : double.NaN);

        /// <summary>
        /// The same sentence, with the extrapolation already computed
        /// (SyntheticFlatResult.Extrapolation).
        /// </summary>
        /// <param name="m">The profile's model.</param>
        /// <param name="cameraCrop">Your camera's crop factor; 0 or less when unknown.</param>
        /// <param name="extrapolation">Your corner against the calibration corner; NaN when
        /// unknown.</param>
        /// <returns>The calibration format, yours, and the extrapolation's tail.</returns>
        internal static string FormatMatch(VignettingModel m, double cameraCrop, double extrapolation)
        {
            string calib = $"{SensorFormats.LabelOf(m.CropFactor)} (crop {Fmt(m.CropFactor, "0.###")})";
            if (!(cameraCrop > 0))
                return T($"profile calibrated on {calib}; your crop is not known, the calibration sensor is taken",
                         $"profil calibrat pe {calib}; crop-ul tau nu se stie, se ia senzorul de calibrare");
            string yours = $"{SensorFormats.LabelOf(cameraCrop)} ({Fmt(cameraCrop, "0.###")})";
            bool same = Math.Abs(cameraCrop - m.CropFactor) / m.CropFactor <= SameFormatTolerance;
            string head = same
                ? T($"profile calibrated on {calib}; your sensor {yours}: the same format",
                    $"profil calibrat pe {calib}; senzorul tau {yours}: acelasi format")
                : T($"profile calibrated on {calib}; your sensor {yours}: another format, the radius is recomputed through mm",
                    $"profil calibrat pe {calib}; senzorul tau {yours}: alt format, raza se recalculeaza prin mm");
            return head + ExtrapolationTail(extrapolation);
        }

        /// <summary>
        /// True when the sentence above is a warning: your corner falls far beyond the calibration.
        /// </summary>
        /// <param name="m">The profile's model.</param>
        /// <param name="cameraCrop">Your camera's crop factor; 0 or less when unknown.</param>
        /// <param name="w">The frame's width, pixels.</param>
        /// <param name="h">The frame's height, pixels.</param>
        /// <returns>True above <see cref="SyntheticFlatResult.SmallExtrapolation"/> with a known
        /// crop.</returns>
        internal static bool FormatMatchIsWarning(VignettingModel m, double cameraCrop, int w, int h)
            => cameraCrop > 0 && m.Extrapolation(w, h, cameraCrop) > SyntheticFlatResult.SmallExtrapolation;

        /// <summary>
        /// What your corner means against the calibration corner; empty when they coincide.
        /// </summary>
        /// <param name="e">Your corner against the calibration corner; NaN when unknown.</param>
        /// <returns>The tail, starting with "; "; "" between 0.97 and 1 or for NaN.</returns>
        internal static string ExtrapolationTail(double e)
        {
            string es = Fmt(e, "0.00");
            if (!(e > 1.0))
                return e < 0.97 ? T($"; your corner falls at {es} of the calibration corner, inside the profile",
                                    $"; coltul tau cade la {es} din coltul de calibrare, in interiorul profilului") : "";
            return e <= SyntheticFlatResult.SmallExtrapolation
                ? T($"; your corner falls at {es} x the calibration corner: a small extrapolation",
                    $"; coltul tau cade la {es} x coltul de calibrare: extrapolare mica")
                : T($"; your corner falls at {es} x the calibration corner: a LARGE extrapolation, the polynomial cannot be trusted beyond what was calibrated - take a profile calibrated on your format",
                    $"; coltul tau cade la {es} x coltul de calibrare: extrapolare MARE, polinomul nu e de incredere dincolo de ce a fost calibrat - ia un profil calibrat pe formatul tau");
        }

        /// <summary>
        /// The extrapolation tail of a written flat; empty when the corner is inside the profile.
        /// </summary>
        /// <param name="r">The synthetic flat built.</param>
        /// <returns>As <see cref="ExtrapolationTail"/>.</returns>
        internal static string ExtrapolationNote(SyntheticFlatResult r) => ExtrapolationTail(r.Extrapolation);

        /// <summary>What was requested, and from where.</summary>
        /// <param name="req">The request.</param>
        /// <returns>"N mm din ..., f/N.N din ...".</returns>
        internal static string Requested(SyntheticRequest req)
            => T($"{Fmt(req.FocalMm, "0.#")} mm from {req.FocalSource}, f/{Fmt(req.FStop, "0.0")} from {req.FStopSource}",
                 $"{Fmt(req.FocalMm, "0.#")} mm din {req.FocalSource}, f/{Fmt(req.FStop, "0.0")} din {req.FStopSource}");

        /// <summary>
        /// When the profile used is far from what was requested: the focal length by more than
        /// Th.ProfileFocalTolMm / TolFrac, the aperture by more than Th.ProfileStopsTol stops.
        /// Nearest neither interpolates nor refuses, so 500 mm f/1.0 on the Nikkor 35 f/1.8 ended
        /// up silently at 35 mm f/1.8, with the only sign in "cerut". The sentence says what was
        /// requested, what was taken, what the lens has, and that the depth written is the
        /// profile's. Null when everything is within tolerance. <paramref name="all"/> are the
        /// lens's profiles (for the range).
        /// </summary>
        /// <param name="used">The profile used.</param>
        /// <param name="focalMm">The focal length requested, mm.</param>
        /// <param name="fStop">The aperture requested, f-number.</param>
        /// <param name="all">The lens's profiles; null for just <paramref name="used"/>.</param>
        /// <returns>The sentence, or null when within tolerance.</returns>
        internal static string ProfileMismatch(LensProfile used, double focalMm, double fStop, IReadOnlyList<LensProfile> all)
        {
            all ??= new[] { used };
            var parts = new List<string>();
            if (!LensSpec.Close(used.FocalMm, focalMm))
            {
                var focals = all.Select(p => p.FocalMm).Distinct().OrderBy(f => f).Select(f => Fmt(f, "0.#")).ToList();
                parts.Add(T($"the focal length requested, {Fmt(focalMm, "0.#")} mm, has no profile: {Fmt(used.FocalMm, "0.#")} mm was taken"
                            + $" (the lens has profiles at {string.Join(", ", focals)} mm)",
                            $"focala ceruta {Fmt(focalMm, "0.#")} mm n-are profil: s-a luat {Fmt(used.FocalMm, "0.#")} mm"
                            + $" (obiectivul are profile la {string.Join(", ", focals)} mm)"));
            }
            double stops = Math.Abs(LensCatalog.Stops(used.FStop) - LensCatalog.Stops(fStop));
            if (stops > Th.ProfileStopsTol)
            {
                double lo = all.Min(p => p.FStop), hi = all.Max(p => p.FStop);
                string range = Math.Abs(lo - hi) < 1e-9
                    ? T($"a single profile, f/{Fmt(lo, "0.0")}", $"un singur profil, f/{Fmt(lo, "0.0")}")
                    : T($"profiles from f/{Fmt(lo, "0.0")} to f/{Fmt(hi, "0.0")}", $"profile de la f/{Fmt(lo, "0.0")} la f/{Fmt(hi, "0.0")}");
                parts.Add(T($"the aperture requested, f/{Fmt(fStop, "0.0")}, is {Fmt(stops, "0.0")} stops from the nearest profile, f/{Fmt(used.FStop, "0.0")} ({range})",
                            $"diafragma ceruta f/{Fmt(fStop, "0.0")} e la {Fmt(stops, "0.0")} stopuri de cel mai apropiat profil, f/{Fmt(used.FStop, "0.0")} ({range})"));
            }
            if (parts.Count == 0) return null;
            return string.Join("; ", parts) + T(". The depth written is that of the profile used, not that of the values requested.",
                                                ". Adancimea scrisa e a profilului folosit, nu a valorilor cerute.");
        }

        /// <summary>Whether the profile used is outside the tolerance of the request.</summary>
        /// <param name="used">The profile used.</param>
        /// <param name="focalMm">The focal length requested, mm.</param>
        /// <param name="fStop">The aperture requested, f-number.</param>
        /// <returns>True when <see cref="ProfileMismatch"/> has something to say.</returns>
        internal static bool ProfileMismatches(LensProfile used, double focalMm, double fStop)
            => ProfileMismatch(used, focalMm, fStop, null) != null;

        /// <summary>A number in a fixed format, invariant.</summary>
        /// <param name="v">The value.</param>
        /// <param name="f">The .NET format string.</param>
        /// <returns>The formatted number.</returns>
        static string Fmt(double v, string f) => v.ToString(f, CultureInfo.InvariantCulture);

        /// <summary>The depth rows as the console prints them.</summary>
        /// <param name="r">The synthetic flat built.</param>
        /// <returns>One "label: value" line per point.</returns>
        internal static string[] DepthLines(SyntheticFlatResult r)
            => DepthRows(r).Select(d => $"{d.Label,-28}: {d.Value}").ToArray();

        /// <summary>
        /// The four points of the depth written, with each one's note - which point it is and
        /// what it compares with. The console prints only the label and the value; the window the
        /// note too.
        /// </summary>
        /// <param name="r">The synthetic flat built.</param>
        /// <returns>Four rows: the corner, the long edge, the short edge, half the
        /// half-diagonal.</returns>
        internal static (string Label, string Value, string Note)[] DepthRows(SyntheticFlatResult r) => new[]
        {
            (T("corner (the 5 % box)", "colt (cutia de 5 %)"), Depth(r.CornerStops),
             T("the centre of the 5 % corner box the corner test measures too, so directly comparable with the master's \"vignetting centre -> corner\"; it is the deepest point of the flat",
               "centrul cutiei de 5 % din colt pe care o masoara si testul de colturi, deci direct comparabil cu \"vignetare centru -> colt\" al masterului; e punctul cel mai adanc al flatului")),
            (T("long edge (left/right)", "marginea lunga (st/dr)"), Depth(r.LongEdgeStops),
             T("the middle of the left / right side (95 % of the half-width, on the horizontal axis); less deep than the corner because it is nearer the axis",
               "mijlocul laturii din stanga / dreapta (95 % din semilatime, pe axa orizontala); mai putin adanc decat coltul fiindca e mai aproape de axa")),
            (T("short edge (top/bottom)", "marginea scurta (sus/jos)"), Depth(r.ShortEdgeStops),
             T("the middle of the top / bottom side (95 % of the half-height, on the vertical axis); the least deep of the three edge points, being nearest the axis",
               "mijlocul laturii de sus / jos (95 % din semiinaltime, pe axa verticala); cel mai putin adanc dintre cele trei puncte de margine, fiind cel mai aproape de axa")),
            (T("half the half-diagonal", "jumatate de semidiagonala"), Depth(r.HalfDiagonalStops),
             T("halfway between the centre and the corner, on the diagonal: the middle of the field, where most of the subject sits; it says how much of the fall-off is already there",
               "la jumatatea drumului dintre centru si colt, pe diagonala: mijlocul campului, unde sta cea mai mare parte a obiectului; spune cat din cadere e deja acolo")),
        };

        /// <summary>A depth as the level against the axis and in stops.</summary>
        /// <param name="stops">The depth, stops.</param>
        /// <returns>"NN.N % din axa  (N.NN stopuri)".</returns>
        internal static string Depth(double stops)
            => T($"{100 * Math.Pow(2, -stops),5:F1} % of the axis  ({stops,5:F2} stops)",
                 $"{100 * Math.Pow(2, -stops),5:F1} % din axa  ({stops,5:F2} stopuri)");

        /// <summary>What the synthetic flat is and is not.</summary>
        /// <returns>Eight lines joined by '\n'.</returns>
        internal static string Caveat()
            => T("the synthetic flat is the lens MODEL, not a measurement of your copy. It has\n"
                 + "only the vignetting, radial and achromatic (the published profiles have a single set of\n"
                 + "parameters for every channel): it holds no chromatic part of the fall-off, no shading\n"
                 + "specific to the body, no dust, nor any non-radial structure. The two databases differ\n"
                 + "from each other by ~0.2 stops in the corner on the same lens; ask no more precision of it.\n"
                 + "What it lacks against a real flat - and what it has besides: no light source,\n"
                 + "so neither the sky's curvature nor a panel's fall-off. Check a real flat against\n"
                 + "it with --against.",
                 "flatul sintetic e MODELUL obiectivului, nu o masuratoare a exemplarului tau. Are\n"
                 + "doar vignetarea, radiala si acromatica (profilele publicate au un singur set de\n"
                 + "parametri pentru toate canalele): nu contine partea cromatica a caderii, umbrirea\n"
                 + "specifica body-ului, praful, nici vreo structura neradiala. Cele doua baze difera\n"
                 + "intre ele cu ~0.2 stopuri in colt pe acelasi obiectiv; atata precizie sa-i ceri.\n"
                 + "Ce ii lipseste fata de un flat real - si ce are in plus: nicio sursa de lumina,\n"
                 + "deci nici curbura cerului, nici caderea unui panou. Verifica un flat real fata de\n"
                 + "el cu --against.");

        /// <summary>
        /// How the file gets into Siril: the scripts do not take it by themselves.
        /// </summary>
        /// <returns>Two lines joined by '\n'.</returns>
        internal static string SirilNote()
            => T("The Siril scripts name pp_flat_stacked explicitly, so they do not take the synthetic flat by themselves:\n"
                 + "rename it in place of the master (keep the original) or give it to calibrate with -flat=.",
                 "Scripturile Siril numesc pp_flat_stacked explicit, deci nu iau flatul sintetic singure:\n"
                 + "redenumeste-l in locul masterului (pastreaza originalul) sau da-l la calibrate cu -flat=.");

        /// <summary>The line that announces the file written.</summary>
        /// <param name="path">The path written to, as printed.</param>
        /// <returns>The line, after an empty one.</returns>
        internal static string SavedLine(string path)
            => T($"\n    -> synthetic flat written: {path}", $"\n    -> flat sintetic scris: {path}");

        // ---------------------------------------------------------------- refusals

        /// <summary>
        /// "IMG_7290.CR2 says nothing about the lens (no lens ID, no lens name, no focal range): a
        /// telescope, or a lens without contacts" - a raw that was read and carries no lens (R-106). One
        /// sentence for the Structure report's gap, the Structure tab's card and the Synthetic
        /// tab's prefill note.
        /// </summary>
        /// <param name="rawName">The raw's file name; null when the raw is the reference itself,
        /// which gives "the raw".</param>
        /// <returns>The sentence, without a remedy.</returns>
        internal static string RawWithoutLensNote(string rawName)
            => T($"{rawName ?? "the raw"} says nothing about the lens (no lens ID, no lens name, no focal range): a telescope, or a lens without contacts",
                 $"{rawName ?? "raw-ul"} nu spune nimic despre obiectiv (niciun ID de obiectiv, niciun nume de obiectiv, nicio gama de focale): un telescop, sau un obiectiv fara contacte");

        /// <summary>
        /// What the raw said about its lens, as it said it (R-108): "the lens "EF50mm f/1.4 USM"
        /// (ID 198) from the raw (Canon EF, 50 mm f/1.4)", "the lens "FE 16-35mm F2.8 GM" from the
        /// raw (Sony E, 16-35 mm f/2.8)", "the lens ID 2263 from the raw (Pentax K)", "the lens ID
        /// 139 from the raw (11-20 mm f/2.8)" when the mount is unknown; no parenthesis when
        /// neither the mount nor the range is known.
        /// </summary>
        /// <param name="f">The loaded raw; null, or one with neither a lens ID nor a lens name,
        /// gives a placeholder.</param>
        /// <returns>The lens's name and ID, with the mount and the range read from the raw.</returns>
        internal static string RawLensLabel(LoadedFrame f)
        {
            bool id = f?.LensId != null, named = !string.IsNullOrWhiteSpace(f?.LensName);
            if (!id && !named) return T("(lens ID)", "(ID obiectiv)");
            string tail = RawLensTail(f);
            if (named && id)
                return T($"the lens \"{f.LensName}\" (ID {f.LensId.Value}) from the raw", $"obiectivul \"{f.LensName}\" (ID {f.LensId.Value}) din raw") + tail;
            if (named)
                return T($"the lens \"{f.LensName}\" from the raw", $"obiectivul \"{f.LensName}\" din raw") + tail;
            return T($"the lens ID {f.LensId.Value} from the raw", $"ID-ul obiectivului {f.LensId.Value} din raw") + tail;
        }

        /// <summary>
        /// " (Canon EF, 50 mm f/1.4)": the mount and the range the raw gives, whichever are known;
        /// "" when neither is.
        /// </summary>
        /// <param name="f">The loaded raw.</param>
        /// <returns>The parenthesis with its leading space, or "".</returns>
        static string RawLensTail(LoadedFrame f)
        {
            string range = LensSpec.Describe(f.LensMinFocalMm, f.LensMaxFocalMm, f.LensMaxAperture);
            string tail = string.Join(", ", new[] { f.LensMount, range }.Where(p => !string.IsNullOrEmpty(p)));
            return tail.Length > 0 ? $" ({tail})" : "";
        }

        /// <summary>
        /// "the range 135 mm f/2.0 from the raw (the lens ID 74 matches no profile)" - the
        /// candidates by range alone, and why the ID or the name did not decide.
        /// </summary>
        /// <param name="f">The loaded raw; null allowed.</param>
        /// <returns>The range read from the raw, and why the ID or the name did not decide.</returns>
        internal static string RawRangeLabel(LoadedFrame f)
        {
            string range = LensSpec.Describe(f?.LensMinFocalMm, f?.LensMaxFocalMm, f?.LensMaxAperture);
            bool id = f?.LensId != null, named = !string.IsNullOrWhiteSpace(f?.LensName);
            string why = named && id
                ? T($" (the lens \"{f.LensName}\", ID {f.LensId.Value}, matches no profile)", $" (obiectivul \"{f.LensName}\", ID {f.LensId.Value}, nu se potriveste cu niciun profil)")
                : named ? T($" (the lens \"{f.LensName}\" matches no profile)", $" (obiectivul \"{f.LensName}\" nu se potriveste cu niciun profil)")
                : id ? T($" (the lens ID {f.LensId.Value} matches no profile)", $" (ID-ul obiectivului {f.LensId.Value} nu se potriveste cu niciun profil)")
                : T(" (no lens ID)", " (fara ID de obiectiv)");
            return T($"the range {range} from the raw", $"gama {range} din raw") + why;
        }

        /// <summary>The refusal when no lens was given.</summary>
        /// <returns>One line.</returns>
        internal static string NoLensGiven()
            => T("SYNTHETIC FLAT: name the lens with --lens <text> (it searches the names; see --lenses for the list).",
                 "FLAT SINTETIC: spune obiectivul cu --lens <text> (cauta in nume; vezi --lenses pentru lista).");

        /// <summary>The refusal when no lens matches the text.</summary>
        /// <param name="text">The --lens text, as given.</param>
        /// <param name="sources">What profiles were found, for the summary.</param>
        /// <returns>Lines joined by '\n'.</returns>
        internal static string NoMatch(string text, ProfileSources sources)
            => T($"SYNTHETIC FLAT: no lens matches \"{text}\".\n", $"FLAT SINTETIC: niciun obiectiv nu se potriveste cu \"{text}\".\n")
             + SourcesSummary(sources)
             + T("\nTry another text, or --lenses <text> to see what is in the list.",
                 "\nIncearca alt text, sau --lenses <text> ca sa vezi ce e in lista.");

        /// <summary>
        /// The refusal when several lenses match; the first 20 are listed.
        /// </summary>
        /// <param name="what">What matched them, ready to print: the --lens text in quotes, or
        /// the raw's lens label as it stands.</param>
        /// <param name="hits">The lenses that match.</param>
        /// <returns>Lines joined by '\n'.</returns>
        internal static string Ambiguous(string what, IReadOnlyList<LensChoice> hits)
            => T($"SYNTHETIC FLAT: {what} matches {hits.Count} lenses; choose one with a longer text"
                 + " (or --source adobe|lensfun):\n",
                 $"FLAT SINTETIC: {what} se potriveste cu {hits.Count} obiective; alege unul cu un text mai lung"
                 + " (sau --source adobe|lensfun):\n")
             + string.Join("\n", hits.Take(20).Select(h => "    " + h))
             + (hits.Count > 20 ? T($"\n    ... and {hits.Count - 20} more", $"\n    ... si inca {hits.Count - 20}") : "");

        /// <summary>The refusal when the focal length is not known.</summary>
        /// <returns>One line.</returns>
        internal static string NoFocal()
            => T("SYNTHETIC FLAT: the focal length is not known - the reference frame has no FOCALLEN and is not a raw; give --focal <mm>.",
                 "FLAT SINTETIC: nu stiu focala - cadrul de referinta n-are FOCALLEN si nu e un raw; da --focal <mm>.");

        /// <summary>The refusal when the aperture is not known.</summary>
        /// <returns>One line.</returns>
        internal static string NoFStop()
            => T("SYNTHETIC FLAT: the aperture is not known - a FITS does not carry it; give --fstop <f> (or a raw as the reference).",
                 "FLAT SINTETIC: nu stiu diafragma - un FITS n-o poarta; da --fstop <f> (sau un raw ca referinta).");

        /// <summary>The refusal when a lensfun profile needs a crop nobody supplied.</summary>
        /// <param name="camera">The camera's name from the reference frame; null when it does not
        /// say.</param>
        /// <returns>One line, with the known sensor formats.</returns>
        internal static string NoCrop(string camera)
            => T("SYNTHETIC FLAT: the lensfun profile needs the camera's crop factor, and ",
                 "FLAT SINTETIC: profilul lensfun are nevoie de crop factor-ul aparatului, si ")
             + (camera == null
                 ? T("the reference frame does not say which camera it is", "cadrul de referinta nu spune ce aparat e")
                 : T($"\"{camera}\" is not in the lensfun database", $"\"{camera}\" nu e in baza lensfun"))
             + T($". Give --crop <factor>: {SensorFormats.Describe()}.", $". Da --crop <factor>: {SensorFormats.Describe()}.");

        /// <summary>The refusal for a colour cube as the reference.</summary>
        /// <param name="path">The refused file's path, as printed.</param>
        /// <param name="planes">How many colour planes it has.</param>
        /// <returns>One line.</returns>
        internal static string RefusalCube(string path, int planes)
            => T($"SYNTHETIC FLAT: {path} has {planes} colour planes (debayered); give a CFA or mono frame as the reference.",
                 $"FLAT SINTETIC: {path} are {planes} planuri de culoare (debayerizat); da un cadru CFA sau mono ca referinta.");

        /// <summary>What profiles were found, in two lines: Adobe and lensfun.</summary>
        /// <param name="s">The profile sources found on the machine.</param>
        /// <returns>Two lines joined by '\n'.</returns>
        internal static string SourcesSummary(ProfileSources s)
            => T($"    Adobe  : {s.AdobeLenses} lenses from {s.AdobeFiles} files", $"    Adobe  : {s.AdobeLenses} obiective din {s.AdobeFiles} fisiere")
             + (s.AdobeFilesSkipped > 0 ? T($" ({s.AdobeFilesSkipped} skipped)", $" ({s.AdobeFilesSkipped} sarite)") : "")
             + (s.AdobeFilesDuplicate > 0 ? T($" ({s.AdobeFilesDuplicate} duplicates skipped)", $" ({s.AdobeFilesDuplicate} duplicate sarite)") : "")
             + (s.AdobeFiles == 0 ? T(" - no profile found in ", " - niciun profil gasit in ") + string.Join(" / ", s.AdobeDirectories) : "") + "\n"
             + T($"    lensfun: {s.LensfunLenses} lenses from {s.LensfunDirectory}", $"    lensfun: {s.LensfunLenses} obiective din {s.LensfunDirectory}")
             + (s.LensfunPresent ? "" : T(" - the folder is missing or empty", " - folderul lipseste sau e gol"));

        // ---------------------------------------------------------------- --lenses

        internal const int ListDetailsMax = 40;

        /// <summary>
        /// Writes the <c>--lenses</c> list: the sources, then one row per lens, with its
        /// details when the list has at most <see cref="ListDetailsMax"/> lenses.
        /// </summary>
        /// <param name="lenses">The lenses to list, filtered already.</param>
        /// <param name="catalog">The catalog, for the details.</param>
        /// <param name="text">The filter text, or null / blank for none.</param>
        /// <param name="sources">What profiles were found, for the summary.</param>
        /// <param name="w">Where to write.</param>
        internal static void WriteList(IReadOnlyList<LensChoice> lenses, LensCatalog catalog, string text, ProfileSources sources, TextWriter w)
        {
            w.WriteLine(T("\n=== LENSES WITH A VIGNETTING PROFILE", "\n=== OBIECTIVE CU PROFIL DE VIGNETARE")
                        + (string.IsNullOrWhiteSpace(text) ? "" : T($" that contain \"{text}\"", $" care contin \"{text}\"")) + " ===");
            w.WriteLine(SourcesSummary(sources));
            w.WriteLine();
            if (lenses.Count == 0) { w.WriteLine(T("    (none)", "    (niciunul)")); return; }
            // The focal lengths and the apertures need the Adobe file parsed; on a long list (no
            // filter, thousands of lenses) that would add up to tens of seconds, so only on a short
            // one.
            bool details = lenses.Count <= ListDetailsMax;
            foreach (LensChoice l in lenses)
            {
                w.WriteLine($"    {l.SourceLabel,-7} {l.Name}  [{l.Where}]");
                if (details) w.WriteLine("            " + LensDetails(l, catalog));
            }
            if (!details) w.WriteLine(T($"\n    ({ListDetailsHidden(lenses.Count)} - give a text to --lens)",
                                        $"\n    ({ListDetailsHidden(lenses.Count)} - da un text la --lens)"));
        }

        /// <summary>
        /// What a lens has: "focale: 18, 20 mm; diafragme: f/2.8, f/4.0; distante: 0.36, 1.51 m,
        /// infinit; 3 profile". The distances ascending, then infinity, then unspecified (the
        /// copied
        /// lensfun pair). An Adobe file is parsed here, so the console and the window ask for it
        /// only on a list of at most <see cref="ListDetailsMax"/>. The same sentence in both: the
        /// console prints it under the row, the window puts it on the row's second line.
        /// </summary>
        /// <param name="lens">The lens.</param>
        /// <param name="catalog">The catalog that loads its profiles.</param>
        /// <returns>The details; "" when the lens has no profile.</returns>
        internal static string LensDetails(LensChoice lens, LensCatalog catalog)
        {
            var ps = catalog.ProfilesOf(lens);
            if (ps.Count == 0) return "";
            var focals = ps.Select(p => p.FocalMm).Distinct().OrderBy(f => f).ToList();
            var stops = ps.Select(p => p.FStop).Distinct().OrderBy(f => f).ToList();
            var metres = ps.Select(p => p.DistanceM).Where(d => !double.IsNaN(d) && d < 1000).Distinct().OrderBy(d => d)
                           .Select(d => d.ToString("0.##", CultureInfo.InvariantCulture)).ToList();
            var distances = new List<string>();
            if (metres.Count > 0) distances.Add(string.Join(", ", metres) + " m");
            if (ps.Any(p => p.DistanceM >= 1000)) distances.Add(LensProfile.DistanceInfinity);
            if (ps.Any(p => double.IsNaN(p.DistanceM))) distances.Add(LensProfile.DistanceUnspecified);
            string focalList = string.Join(", ", focals.Select(f => f.ToString("0.#", CultureInfo.InvariantCulture)));
            string stopList = string.Join(", f/", stops.Select(f => f.ToString("0.0", CultureInfo.InvariantCulture)));
            return T($"focal lengths: {focalList} mm; apertures: f/{stopList}; distances: {string.Join(", ", distances)}; {ps.Count} profiles",
                     $"focale: {focalList} mm; diafragme: f/{stopList}; distante: {string.Join(", ", distances)}; {ps.Count} profile");
        }

        /// <summary>
        /// Why the details are missing on a long list; the console and the window each add how to
        /// shorten the list.
        /// </summary>
        /// <param name="count">How many lenses are listed.</param>
        /// <returns>One sentence, without a final full stop.</returns>
        internal static string ListDetailsHidden(int count)
            => T($"{count} lenses; the focal lengths, apertures and distances are shown when the list has at most {ListDetailsMax}",
                 $"{count} obiective; focalele, diafragmele si distantele se arata cand lista are cel mult {ListDetailsMax}");

        // ---------------------------------------------------------------- the window's panel

        /// <summary>Where the Adobe profiles come from and where they are read.</summary>
        /// <param name="s">The profile sources, with the Adobe folders.</param>
        /// <returns>Lines joined by '\n'.</returns>
        internal static string AdobeWhere(ProfileSources s)
            => T("The Adobe profiles (.lcp) are installed by Lightroom, Camera Raw or DNG Converter (free) in:\n",
                 "Profilele Adobe (.lcp) le instaleaza Lightroom, Camera Raw sau DNG Converter (gratuit) in:\n")
             + string.Join("\n", s.AdobeDirectories.Select(d => "  " + d)) + "\n"
             + T("They are read from there at start-up; a file present in two folders is read once, from the first. They are not copied and not redistributed. Without Adobe, this half of the list is empty.",
                 "Se citesc de acolo la pornire; un fisier prezent in doua foldere se citeste o data, din primul. Nu se copiaza si nu se redistribuie. Fara Adobe, jumatatea asta a listei e goala.");

        /// <summary>Where the lensfun database is and how to update it.</summary>
        /// <param name="s">The profile sources, with the lensfun folder and version.</param>
        /// <returns>Lines joined by '\n'.</returns>
        internal static string LensfunWhere(ProfileSources s)
            => T("The lensfun database (CC BY-SA 3.0) ships with the tool, in the folder:\n  ",
                 "Baza lensfun (CC BY-SA 3.0) e livrata cu unealta in folderul:\n  ") + s.LensfunDirectory + "\n"
             + (s.LensfunVersion != null ? s.LensfunVersion + "\n" : "")
             + T("To update: download the XML files from https://github.com/lensfun/lensfun/tree/master/data/db,\n"
                 + "replace those in the folder and restart. The copy's version is in ",
                 "Actualizare: descarca fisierele XML din https://github.com/lensfun/lensfun/tree/master/data/db,\n"
                 + "inlocuieste-le pe cele din folder si reporneste. Versiunea copiei e in ")
             + ProfileLocator.LensfunVersionFile + ".";

        /// <summary>Writes each line of a text indented by four spaces.</summary>
        /// <param name="text">The lines, joined by '\n'.</param>
        /// <param name="w">Where to write.</param>
        static void Indent(string text, TextWriter w)
        {
            foreach (string line in text.Split('\n')) w.WriteLine("    " + line);
        }
    }
}
