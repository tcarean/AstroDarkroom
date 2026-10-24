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
            w.WriteLine($"\n=== FLAT SINTETIC din profilul obiectivului ===");
            w.WriteLine($"    cadru de referinta : {referencePath}  ({r.Width} x {r.Height})");
            w.WriteLine($"    obiectiv           : {p.Lens.Name}  [{p.Lens.Where}]  sursa: {p.Lens.SourceLabel}");
            w.WriteLine($"    profil folosit     : {p.Settings}   (cerut: {Requested(req)})");
            string mismatch = ProfileMismatch(p, req.FocalMm, req.FStop, r.LensProfiles);
            if (mismatch != null) Indent("ATENTIE: " + mismatch, w);
            w.WriteLine($"    model              : {p.Model.Describe()}");
            w.WriteLine($"    fisier profil      : {Path.GetFileName(p.File)}");
            w.WriteLine("    crop aparat        : " + CropLine(r, req));
            w.WriteLine("    format             : " + FormatMatch(p.Model, r.CameraCrop, r.Width, r.Height));

            w.WriteLine("\n  ADANCIMEA VIGNETARII SCRISE (fata de axa)");
            foreach (string line in DepthLines(r)) w.WriteLine("    " + line);

            w.WriteLine();
            Indent("INFORMATIV: " + Caveat(), w);
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
                ? $"necunoscut  ({req.CropSource}; SFF {Fmt(r.Profile.Model.CropFactor, "0.###")})"
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
                return $"profil calibrat pe {calib}; crop-ul tau nu se stie, se ia senzorul de calibrare";
            string yours = $"{SensorFormats.LabelOf(cameraCrop)} ({Fmt(cameraCrop, "0.###")})";
            bool same = Math.Abs(cameraCrop - m.CropFactor) / m.CropFactor <= SameFormatTolerance;
            string head = same
                ? $"profil calibrat pe {calib}; senzorul tau {yours}: acelasi format"
                : $"profil calibrat pe {calib}; senzorul tau {yours}: alt format, raza se recalculeaza prin mm";
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
                return e < 0.97 ? $"; coltul tau cade la {es} din coltul de calibrare, in interiorul profilului" : "";
            return e <= SyntheticFlatResult.SmallExtrapolation
                ? $"; coltul tau cade la {es} x coltul de calibrare: extrapolare mica"
                : $"; coltul tau cade la {es} x coltul de calibrare: extrapolare MARE, polinomul nu e de incredere dincolo de ce a fost calibrat - ia un profil calibrat pe formatul tau";
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
            => $"{Fmt(req.FocalMm, "0.#")} mm din {req.FocalSource}, f/{Fmt(req.FStop, "0.0")} din {req.FStopSource}";

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
                parts.Add($"focala ceruta {Fmt(focalMm, "0.#")} mm n-are profil: s-a luat {Fmt(used.FocalMm, "0.#")} mm"
                        + $" (obiectivul are profile la {string.Join(", ", focals)} mm)");
            }
            double stops = Math.Abs(LensCatalog.Stops(used.FStop) - LensCatalog.Stops(fStop));
            if (stops > Th.ProfileStopsTol)
            {
                double lo = all.Min(p => p.FStop), hi = all.Max(p => p.FStop);
                string range = Math.Abs(lo - hi) < 1e-9 ? $"un singur profil, f/{Fmt(lo, "0.0")}" : $"profile de la f/{Fmt(lo, "0.0")} la f/{Fmt(hi, "0.0")}";
                parts.Add($"diafragma ceruta f/{Fmt(fStop, "0.0")} e la {Fmt(stops, "0.0")} stopuri de cel mai apropiat profil, f/{Fmt(used.FStop, "0.0")} ({range})");
            }
            if (parts.Count == 0) return null;
            return string.Join("; ", parts) + ". Adancimea scrisa e a profilului folosit, nu a valorilor cerute.";
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
            ("colt (cutia de 5 %)", Depth(r.CornerStops),
             "centrul cutiei de 5 % din colt pe care o masoara si testul de colturi, deci direct comparabil cu \"vignetare centru -> colt\" al masterului; e punctul cel mai adanc al flatului"),
            ("marginea lunga (st/dr)", Depth(r.LongEdgeStops),
             "mijlocul laturii din stanga / dreapta (95 % din semilatime, pe axa orizontala); mai putin adanc decat coltul fiindca e mai aproape de axa"),
            ("marginea scurta (sus/jos)", Depth(r.ShortEdgeStops),
             "mijlocul laturii de sus / jos (95 % din semiinaltime, pe axa verticala); cel mai putin adanc dintre cele trei puncte de margine, fiind cel mai aproape de axa"),
            ("jumatate de semidiagonala", Depth(r.HalfDiagonalStops),
             "la jumatatea drumului dintre centru si colt, pe diagonala: mijlocul campului, unde sta cea mai mare parte a obiectului; spune cat din cadere e deja acolo"),
        };

        /// <summary>A depth as the level against the axis and in stops.</summary>
        /// <param name="stops">The depth, stops.</param>
        /// <returns>"NN.N % din axa  (N.NN stopuri)".</returns>
        internal static string Depth(double stops)
            => $"{100 * Math.Pow(2, -stops),5:F1} % din axa  ({stops,5:F2} stopuri)";

        /// <summary>What the synthetic flat is and is not.</summary>
        /// <returns>Eight lines joined by '\n'.</returns>
        internal static string Caveat()
            => "flatul sintetic e MODELUL obiectivului, nu o masuratoare a exemplarului tau. Are\n"
             + "doar vignetarea, radiala si acromatica (profilele publicate au un singur set de\n"
             + "parametri pentru toate canalele): nu contine partea cromatica a caderii, umbrirea\n"
             + "specifica body-ului, praful, nici vreo structura neradiala. Cele doua baze difera\n"
             + "intre ele cu ~0.2 stopuri in colt pe acelasi obiectiv; atata precizie sa-i ceri.\n"
             + "Ce ii lipseste fata de un flat real - si ce are in plus: nicio sursa de lumina,\n"
             + "deci nici curbura cerului, nici caderea unui panou. Verifica un flat real fata de\n"
             + "el cu --against.";

        /// <summary>
        /// How the file gets into Siril: the scripts do not take it by themselves.
        /// </summary>
        /// <returns>Two lines joined by '\n'.</returns>
        internal static string SirilNote()
            => "Scripturile Siril numesc pp_flat_stacked explicit, deci nu iau flatul sintetic singure:\n"
             + "redenumeste-l in locul masterului (pastreaza originalul) sau da-l la calibrate cu -flat=.";

        /// <summary>The line that announces the file written.</summary>
        /// <param name="path">The path written to, as printed.</param>
        /// <returns>The line, after an empty one.</returns>
        internal static string SavedLine(string path) => $"\n    -> flat sintetic scris: {path}";

        // ---------------------------------------------------------------- refusals

        /// <summary>
        /// "ID-ul Nikon 139 din raw (11-20 mm f/2.8)" - what the raw said about the lens.
        /// </summary>
        /// <param name="f">The loaded raw; null or without a Nikon lens ID gives a
        /// placeholder.</param>
        /// <returns>The lens ID and the range read from the raw.</returns>
        internal static string RawLensLabel(LoadedFrame f)
        {
            if (f?.NikonLensId == null) return "(ID obiectiv)";
            string range = LensSpec.Describe(f.LensMinFocalMm, f.LensMaxFocalMm, f.LensMaxAperture);
            return $"ID-ul Nikon {f.NikonLensId.Value} din raw" + (range.Length > 0 ? $" ({range})" : "");
        }

        /// <summary>
        /// "gama 135 mm f/2.0 din raw (ID-ul Nikon 74 nu se potriveste cu niciun profil)" - the
        /// candidates by range alone.
        /// </summary>
        /// <param name="f">The loaded raw; null allowed.</param>
        /// <returns>The range read from the raw, and why the ID did not decide.</returns>
        internal static string RawRangeLabel(LoadedFrame f)
        {
            string range = LensSpec.Describe(f?.LensMinFocalMm, f?.LensMaxFocalMm, f?.LensMaxAperture);
            string id = f?.NikonLensId != null ? $" (ID-ul Nikon {f.NikonLensId.Value} nu se potriveste cu niciun profil)" : " (fara ID de obiectiv)";
            return $"gama {range} din raw" + id;
        }

        /// <summary>The refusal when no lens was given.</summary>
        /// <returns>One line.</returns>
        internal static string NoLensGiven()
            => "FLAT SINTETIC: spune obiectivul cu --lens <text> (cauta in nume; vezi --lenses pentru lista).";

        /// <summary>The refusal when no lens matches the text.</summary>
        /// <param name="text">The --lens text, as given.</param>
        /// <param name="sources">What profiles were found, for the summary.</param>
        /// <returns>Lines joined by '\n'.</returns>
        internal static string NoMatch(string text, ProfileSources sources)
            => $"FLAT SINTETIC: niciun obiectiv nu se potriveste cu \"{text}\".\n" + SourcesSummary(sources)
             + "\nIncearca alt text, sau --lenses <text> ca sa vezi ce e in lista.";

        /// <summary>
        /// The refusal when several lenses match the text; the first 20 are listed.
        /// </summary>
        /// <param name="text">The --lens text, as given.</param>
        /// <param name="hits">The lenses that match.</param>
        /// <returns>Lines joined by '\n'.</returns>
        internal static string Ambiguous(string text, IReadOnlyList<LensChoice> hits)
            => $"FLAT SINTETIC: \"{text}\" se potriveste cu {hits.Count} obiective; alege unul cu un text mai lung"
             + " (sau --source adobe|lensfun):\n" + string.Join("\n", hits.Take(20).Select(h => "    " + h))
             + (hits.Count > 20 ? $"\n    ... si inca {hits.Count - 20}" : "");

        /// <summary>The refusal when the focal length is not known.</summary>
        /// <returns>One line.</returns>
        internal static string NoFocal()
            => "FLAT SINTETIC: nu stiu focala - cadrul de referinta n-are FOCALLEN si nu e un raw; da --focal <mm>.";

        /// <summary>The refusal when the aperture is not known.</summary>
        /// <returns>One line.</returns>
        internal static string NoFStop()
            => "FLAT SINTETIC: nu stiu diafragma - un FITS n-o poarta; da --fstop <f> (sau un raw ca referinta).";

        /// <summary>The refusal when a lensfun profile needs a crop nobody supplied.</summary>
        /// <param name="camera">The camera's name from the reference frame; null when it does not
        /// say.</param>
        /// <returns>One line, with the known sensor formats.</returns>
        internal static string NoCrop(string camera)
            => "FLAT SINTETIC: profilul lensfun are nevoie de crop factor-ul aparatului, si "
             + (camera == null ? "cadrul de referinta nu spune ce aparat e" : $"\"{camera}\" nu e in baza lensfun")
             + $". Da --crop <factor>: {SensorFormats.Describe()}.";

        /// <summary>The refusal for a colour cube as the reference.</summary>
        /// <param name="path">The refused file's path, as printed.</param>
        /// <param name="planes">How many colour planes it has.</param>
        /// <returns>One line.</returns>
        internal static string RefusalCube(string path, int planes)
            => $"FLAT SINTETIC: {path} are {planes} planuri de culoare (debayerizat); da un cadru CFA sau mono ca referinta.";

        /// <summary>What profiles were found, in two lines: Adobe and lensfun.</summary>
        /// <param name="s">The profile sources found on the machine.</param>
        /// <returns>Two lines joined by '\n'.</returns>
        internal static string SourcesSummary(ProfileSources s)
            => $"    Adobe  : {s.AdobeLenses} obiective din {s.AdobeFiles} fisiere"
             + (s.AdobeFilesSkipped > 0 ? $" ({s.AdobeFilesSkipped} sarite)" : "")
             + (s.AdobeFiles == 0 ? " - niciun profil gasit in " + string.Join(" / ", s.AdobeDirectories) : "") + "\n"
             + $"    lensfun: {s.LensfunLenses} obiective din {s.LensfunDirectory}"
             + (s.LensfunPresent ? "" : " - folderul lipseste sau e gol");

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
            w.WriteLine($"\n=== OBIECTIVE CU PROFIL DE VIGNETARE" + (string.IsNullOrWhiteSpace(text) ? "" : $" care contin \"{text}\"") + " ===");
            w.WriteLine(SourcesSummary(sources));
            w.WriteLine();
            if (lenses.Count == 0) { w.WriteLine("    (niciunul)"); return; }
            // The focal lengths and the apertures need the Adobe file parsed; on a long list (no
            // filter, thousands of lenses) that would add up to tens of seconds, so only on a short
            // one.
            bool details = lenses.Count <= ListDetailsMax;
            foreach (LensChoice l in lenses)
            {
                w.WriteLine($"    {l.SourceLabel,-7} {l.Name}  [{l.Where}]");
                if (details) w.WriteLine("            " + LensDetails(l, catalog));
            }
            if (!details) w.WriteLine($"\n    ({ListDetailsHidden(lenses.Count)} - da un text la --lens)");
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
            return $"focale: {string.Join(", ", focals.Select(f => f.ToString("0.#", CultureInfo.InvariantCulture)))} mm;"
                 + $" diafragme: f/{string.Join(", f/", stops.Select(f => f.ToString("0.0", CultureInfo.InvariantCulture)))};"
                 + $" distante: {string.Join(", ", distances)}; {ps.Count} profile";
        }

        /// <summary>
        /// Why the details are missing on a long list; the console and the window each add how to
        /// shorten the list.
        /// </summary>
        /// <param name="count">How many lenses are listed.</param>
        /// <returns>One sentence, without a final full stop.</returns>
        internal static string ListDetailsHidden(int count)
            => $"{count} obiective; focalele, diafragmele si distantele se arata cand lista are cel mult {ListDetailsMax}";

        // ---------------------------------------------------------------- the window's panel

        /// <summary>Where the Adobe profiles come from and where they are read.</summary>
        /// <param name="s">The profile sources, with the Adobe folders.</param>
        /// <returns>Lines joined by '\n'.</returns>
        internal static string AdobeWhere(ProfileSources s)
            => "Profilele Adobe (.lcp) le instaleaza Lightroom, Camera Raw sau DNG Converter (gratuit) in:\n"
             + string.Join("\n", s.AdobeDirectories.Select(d => "  " + d)) + "\n"
             + "Se citesc de acolo la pornire; nu se copiaza si nu se redistribuie. Fara Adobe, jumatatea asta a listei e goala.";

        /// <summary>Where the lensfun database is and how to update it.</summary>
        /// <param name="s">The profile sources, with the lensfun folder and version.</param>
        /// <returns>Lines joined by '\n'.</returns>
        internal static string LensfunWhere(ProfileSources s)
            => "Baza lensfun (CC BY-SA 3.0) e livrata cu unealta in folderul:\n  " + s.LensfunDirectory + "\n"
             + (s.LensfunVersion != null ? s.LensfunVersion + "\n" : "")
             + "Actualizare: descarca fisierele XML din https://github.com/lensfun/lensfun/tree/master/data/db,\n"
             + "inlocuieste-le pe cele din folder si reporneste. Versiunea copiei e in " + ProfileLocator.LensfunVersionFile + ".";

        /// <summary>Writes each line of a text indented by four spaces.</summary>
        /// <param name="text">The lines, joined by '\n'.</param>
        /// <param name="w">Where to write.</param>
        static void Indent(string text, TextWriter w)
        {
            foreach (string line in text.Split('\n')) w.WriteLine("    " + line);
        }
    }
}
