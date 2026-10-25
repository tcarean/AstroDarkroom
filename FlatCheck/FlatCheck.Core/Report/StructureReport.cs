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

// StructureReport.cs - StructureResult -> the console's text, and the pieces the window shows
// beside it (advice, notes, the profile card, the corner shares). Formatting only.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FlatCheck
{
    /// <summary>
    /// A row of the table "the vignetting against the lens's profile", ready formatted: the
    /// source (Adobe / lensfun), the lens, the mount and the calibration (Where), the profile
    /// used, its depth, the difference from the master, the error at the corner and the verdict;
    /// IsNearest on the one that gives the verdict, Note with the calibration format against the
    /// sensor (IsWarning when it is a large extrapolation).
    /// </summary>
    /// <param name="Key">The lens's catalog key.</param>
    /// <param name="Source">"Adobe" or "lensfun", as displayed.</param>
    /// <param name="Lens">The lens's name.</param>
    /// <param name="Where">The mount and the calibration it was profiled on.</param>
    /// <param name="Profile">The profile's settings (focal length, aperture).</param>
    /// <param name="Depth">The profile's depth at the corner, in stops and percent.</param>
    /// <param name="Diff">The master's depth minus the profile's, in stops, signed.</param>
    /// <param name="Error">The corner error that difference leaves, in percent.</param>
    /// <param name="Verdict">The row's verdict, on the residual scale.</param>
    /// <param name="IsNearest">True on the row that gives the comparison's verdict.</param>
    /// <param name="Note">The calibration format against the sensor, and any mismatch
    /// warning.</param>
    /// <param name="IsWarning">True when the note is a warning: a large extrapolation or a
    /// profile far from the shot's settings.</param>
    internal sealed record ProfileRow(string Key, string Source, string Lens, string Where, string Profile,
                                      string Depth, string Diff, string Error, string Verdict,
                                      bool IsNearest, string Note, bool IsWarning);

    /// <summary>
    /// The words of the structure mode: the console report of a <see cref="StructureResult"/>
    /// (and of a run with its lens-profile comparison), and every sentence the window shows
    /// beside the tests, the scale and the verdict.
    /// </summary>
    internal static class StructureReport
    {
        /// <summary>Writes the report of a structure result without a profile comparison.</summary>
        /// <param name="r">The structure result.</param>
        /// <param name="path">The master's path, as the header prints it.</param>
        /// <param name="w">Where to write.</param>
        internal static void Write(StructureResult r, string path, TextWriter w)
            => Write(new StructureOutcome(r, null, null, null), path, w);

        /// <summary>
        /// With the vignetting comparison, when the run had one; otherwise identical to the variant
        /// above.
        /// </summary>
        /// <param name="o">The whole run: the structure result and, when made, the
        /// comparison.</param>
        /// <param name="path">The master's path, as the header prints it.</param>
        /// <param name="w">Where to write.</param>
        internal static void Write(StructureOutcome o, string path, TextWriter w)
        {
            StructureResult r = o.Structure;
            w.WriteLine($"\n=== STRUCTURA: {path} ===");
            w.WriteLine($"    dimensiune analiza : {r.Width} x {r.Height} (dupa 2x2 + downsample)");
            w.WriteLine($"    median / max ADU   : {Levels(r)}");
            WriteBody(r, w, o);
        }

        /// <summary>
        /// "median / max", formatted by the file's scale. Siril writes the master as a float
        /// divided by 65535, so the median is ~0.014 - and with zero decimals the line came out
        /// "0 / 0", as if the file were empty. The threshold of 1.5 is the same one --expo
        /// uses to recognise a normalised file. The window shows it too.
        /// </summary>
        /// <param name="r">The structure result, with RawMedian and RawMax.</param>
        /// <returns>The two levels: whole numbers on an ADU file; four decimals and a note on a
        /// normalised one (RawMax at most 1.5).</returns>
        internal static string Levels(StructureResult r)
        {
            bool normalised = r.RawMax <= 1.5;
            string fmt = normalised ? "F4" : "F0";
            string line = $"{r.RawMedian.ToString(fmt)} / {r.RawMax.ToString(fmt)}";
            return normalised ? line + " (float normalizat 0..1)" : line;
        }

        /// <summary>
        /// Everything that follows the header: the informative figures, the five tests, the
        /// scale and the verdict. Taken out separately because the attribution mode runs the same
        /// battery on the combined master (A+B)/2 and wants exactly this body under its own header
        /// - but not the ADU row, which on a raster normalised to median 1 would mean
        /// nothing.
        /// </summary>
        /// <param name="r">The structure result.</param>
        /// <param name="w">Where to write.</param>
        internal static void WriteBody(StructureResult r, TextWriter w) => WriteBody(r, w, null);

        /// <summary>
        /// The body, with the profile comparison's block and verdict when the run had one.
        /// </summary>
        /// <param name="r">The structure battery's result to write.</param>
        /// <param name="w">Where the text goes.</param>
        /// <param name="o">
        /// The whole run, when it had a comparison with the profiles: its block is written after
        /// the scale, and the verdict and the advice are its own (the worst of shape and depth).
        /// Null or without a comparison = the five tests and nothing more.
        /// </param>
        static void WriteBody(StructureResult r, TextWriter w, StructureOutcome o)
        {
            WriteInformative(r, w);

            w.WriteLine("\n  TESTE");
            w.WriteLine($"    imprastiere 4 colturi   : {r.CornerSpreadPct,5:F2} %   [{r.CornerVerdict}]");
            foreach (var kv in r.Corners)
                w.WriteLine($"        {kv.Key,-14}: {CornerSharePct(r, kv.Value),6:F2} % din media colturilor");
            w.WriteLine($"    asimetrie azimutala max : {r.AzimuthalMaxPct,5:F2} %   [{r.AzimuthalVerdict}]");
            w.WriteLine($"    reziduu dupa model rad. : {r.ResidualPtpPct,5:F2} %   [{r.ResidualVerdict}]  <- CEL MAI IMPORTANT");
            w.WriteLine($"    inclinare (gradient)    : {r.TiltPct,5:F2} %   [{r.TiltVerdict}]");
            w.WriteLine($"    pata maxima (o dala)    : {r.MaxTilePct,5:F2} %   [{r.MaxTileVerdict}]");

            WriteScale(w);

            if (o?.Profiles != null) WriteProfiles(o, w);

            w.WriteLine($"\n  VERDICT: {o?.Verdict ?? r.Verdict}");
            if (o != null && o.Compared)
            {
                Indent(VerdictDetail(o), w);
                Indent(Advice(o), w);
            }
            else WriteAdvice(r.Verdict, w);
        }

        // ------------------------------------------------ the vignetting against the lens's
        // profile

        /// <summary>The block's title; the window puts it on its card.</summary>
        internal const string ProfilesHeading = "VIGNETAREA fata de profilul obiectivului";

        /// <summary>
        /// The block of the profile comparison: the master's depth, one row per profile, the
        /// summary and the notes - or why it could not compare.
        /// </summary>
        /// <param name="o">The run, with a non-null comparison.</param>
        /// <param name="w">Where to write.</param>
        static void WriteProfiles(StructureOutcome o, TextWriter w)
        {
            VignettingComparison c = o.Profiles;
            w.WriteLine($"\n  {ProfilesHeading}");
            if (!c.Compared)
            {
                Indent("nu se poate compara: " + GapText(o), w);
                Indent(GapMeaning(), w);
                return;
            }
            w.WriteLine($"    master                  : {MasterDepth(c)}");
            foreach (ProfileRow row in ProfileRows(c))
            {
                w.WriteLine($"    {row.Source,-7} {row.Lens}  [{row.Where}]");
                w.WriteLine($"        profil {row.Profile,-24}: {row.Depth}   diferenta {row.Diff} = {row.Error} [{row.Verdict}]"
                          + (row.IsNearest ? "  <- cel mai apropiat, da verdictul" : ""));
                if (row.IsWarning) w.WriteLine("        " + row.Note);
            }
            Indent(ComparisonSummary(c), w);
            w.WriteLine($"    ({AgainstReport.DepthScale()})");
            Indent(DepthScaleNote(), w);
            Indent(DepthLightScaleNote(c.Nearest.DiffStops), w);
            Indent(ComparisonNote(), w);
        }

        /// <summary>
        /// What the --light mode would read from the same difference, and why the depth verdict
        /// does not sit on its scale: the error at the corner, its word on --light's 2 / 5 % scale
        /// (through Stats.DepthOnLightScale, so with its thresholds, not copied), and the
        /// reference's uncertainty, Th.ProfileDatabaseSpreadStops, translated into the same
        /// corner error. The two scales judge the same quantity with different uncertainties; the
        /// report shows both instead of choosing for the reader. Printed by the console under the
        /// depth scale and by --against under its depth row; the window glues it to the scale's
        /// note. On NaN it says it cannot be read, like the rest of the block.
        /// </summary>
        /// <param name="diffStops">The master's depth minus the profile's, stops; NaN when
        /// undetermined.</param>
        /// <returns>Three lines joined by '\n'; two on NaN.</returns>
        internal static string DepthLightScaleNote(double diffStops)
        {
            double spread = Stats.DepthErrorPct(Th.ProfileDatabaseSpreadStops);
            string reference = $"Referinta insasi difera cu {Th.ProfileDatabaseSpreadStops:F1} stopuri = {spread:F0} % intre bazele de profile,\n"
                             + "deci verdictul adancimii nu poate fi mai fin de atat si ramane pe scara de reziduu.";
            if (double.IsNaN(diffStops))
                return "Pe scara modului --light diferenta nu se poate citi (nedeterminata). " + reference;
            double err = Stats.DepthErrorPct(diffStops);
            return $"In coltul unui light calibrat cu masterul: {err:F1} %; pe scara modului --light ({Th.LightRadOk:F0} % OK,\n"
                 + $"{Th.LightRadBad:F0} % PROBLEMA), care masoara aceeasi marime direct in light, ar fi {Stats.DepthOnLightScale(diffStops)}.\n"
                 + reference;
        }

        /// <summary>
        /// The depth scale as data, for the window: the same four bands as the residual scale,
        /// with the interval in stops and the corner error that corresponds to it. The thresholds
        /// are still Th.Resid*, passed through Stats.StopsForError - not a second list of numbers.
        /// </summary>
        /// <returns>Four rows, PERFECT to REFA: the band, its interval in stops, its
        /// meaning.</returns>
        internal static IReadOnlyList<(string Band, string Range, string Meaning)> DepthScaleRows()
        {
            string S(double pct) => Stats.StopsForError(pct).ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            return new[]
            {
                ("PERFECT", $"sub {S(Th.ResidPerfect)} stopuri", $"eroare in colt sub {Th.ResidPerfect:F0} %"),
                ("BUN", $"{S(Th.ResidPerfect)} - {S(Th.ResidUsable)} stopuri", $"eroare in colt {Th.ResidPerfect:F0} - {Th.ResidUsable:F0} %"),
                ("ACCEPTABIL", $"{S(Th.ResidUsable)} - {S(Th.ResidCheck)} stopuri", $"eroare in colt {Th.ResidUsable:F0} - {Th.ResidCheck:F0} %; sub atat nu se deosebeste de incertitudinea modelului"),
                ("REFA", $"peste {S(Th.ResidCheck)} stopuri", $"eroare in colt peste {Th.ResidCheck:F0} %; refa flat-urile"),
            };
        }

        /// <summary>
        /// Where the depth scale comes from: it is not a new threshold, it is the five tests' scale
        /// translated into stops.
        /// </summary>
        /// <returns>Three lines joined by '\n'.</returns>
        internal static string DepthScaleNote()
            => $"Aceeasi scara de reziduu ca cele cinci teste ({Th.ResidPerfect:F0} / {Th.ResidUsable:F0} / {Th.ResidCheck:F0} %), aplicata erorii pe care\n"
             + "diferenta de adancime o lasa in coltul unui light calibrat cu masterul, 2^|stopuri| - 1, si\n"
             + "tradusa in stopuri. Nu e un prag nou.";

        /// <summary>
        /// "1.26 stopuri (colt = 42 % din centru)" - the master's depth, as the informative row
        /// measures it too.
        /// </summary>
        /// <param name="c">The comparison.</param>
        /// <returns>The master's depth in stops and as the corner's percent of the
        /// centre.</returns>
        internal static string MasterDepth(VignettingComparison c) => Depth(c.MasterStops);

        /// <summary>A depth in stops, with the corner's level as a percent of the centre.</summary>
        /// <param name="stops">The depth, stops.</param>
        /// <returns>"N.NN stopuri (colt = N % din centru)".</returns>
        static string Depth(double stops) => $"{stops:F2} stopuri (colt = {100 * Math.Pow(2, -stops):F0} % din centru)";

        /// <summary>
        /// One row per profile, ready formatted, for the console and for the window.
        /// </summary>
        /// <param name="c">The comparison.</param>
        /// <returns>The rows, in the comparison's order.</returns>
        internal static IReadOnlyList<ProfileRow> ProfileRows(VignettingComparison c)
        {
            var rows = new List<ProfileRow>();
            foreach (ProfileDepth d in c.Profiles)
            {
                LensProfile p = d.Profile;
                bool warning = d.Extrapolation > SyntheticFlatResult.SmallExtrapolation;
                string note = SyntheticReport.FormatMatch(p.Model, d.CameraCrop, d.Extrapolation);
                // The profile is the nearest to the raw's focal length and aperture; when it is far
                // (a raw at 20 mm on a lens with profiles up to 16), the row says so.
                string mismatch = c.Shot?.FocalMm != null && c.Shot.FStop != null
                    ? SyntheticReport.ProfileMismatch(p, c.Shot.FocalMm.Value, c.Shot.FStop.Value, d.LensProfiles) : null;
                if (mismatch != null) { note += " ATENTIE: " + mismatch; warning = true; }
                rows.Add(new ProfileRow(p.Lens.Key, p.Lens.SourceLabel, p.Lens.Name, p.Lens.Where, p.Settings,
                    Depth(d.CornerStops), $"{d.DiffStops:+0.00;-0.00} stopuri", $"{d.ErrorPct:F1} %", d.Verdict,
                    ReferenceEquals(d, c.Nearest), note, warning));
            }
            return rows;
        }

        /// <summary>
        /// Whose the verdict is and how much the profiles differ among themselves. "The nearest",
        /// not "the worst": the two databases differ from each other by ~0.2 stops on the same
        /// lens, and a master that matches one of them is not wrong.
        /// </summary>
        /// <param name="c">The comparison, with at least one row.</param>
        /// <returns>Two or three lines joined by '\n'.</returns>
        internal static string ComparisonSummary(VignettingComparison c)
        {
            string who = $"{c.Nearest.Profile.Lens.SourceLabel} {c.Nearest.Profile.Lens.Title}";
            string verdict = $"verdictul comparatiei: {c.Verdict} - al celui mai apropiat profil ({who}), "
                           + $"{c.Nearest.DiffStops:+0.00;-0.00} stopuri = {c.Nearest.ErrorPct:F1} % eroare in coltul unui light calibrat cu masterul";
            string spread = c.Profiles.Count > 1
                ? $"\ncele {c.Profiles.Count} profile difera intre ele cu {c.SpreadStops:F2} stopuri in colt; un master care coincide cu unul din ele nu e gresit"
                : "\nun singur profil: fara a doua baza nu se stie cat de incert e modelul (de obicei ~0.2 stopuri)";
            return verdict + spread + RangeOnlyNote(c.Shot);
        }

        /// <summary>
        /// When the lenses were found only by their range (a manual lens, no usable ID): the list
        /// may include another lens with the same range, and the person must know.
        /// </summary>
        /// <param name="shot">The shot's identity; null when unknown.</param>
        /// <returns>The note, starting with '\n'; "" when the lenses were matched by ID.</returns>
        internal static string RangeOnlyNote(ShotIdentity shot)
            => shot != null && shot.CandidatesByRangeOnly
                ? $"\nobiectivele de mai sus sunt cele ale monturii cu {shot.RawLensLabel}: obiectiv manual sau necunoscut bazelor. "
                + "Daca printre ele e si altul cu aceeasi gama, citeste randul obiectivului tau, nu pe cel mai apropiat."
                : "";

        /// <summary>
        /// What the comparison can and cannot say. The window shows it too, under the table.
        /// </summary>
        /// <returns>Three lines joined by '\n'.</returns>
        internal static string ComparisonNote()
            => "Diferenta e RADIALA, deci invizibila celor cinci teste, hartii si modului --ratio: e singurul\n"
             + "loc unde se vede o sursa care cade cu unghiul (plus) sau umple colturile (minus). Profilul e\n"
             + "modelul obiectivului, nu exemplarul tau, si e acromatic; masterul e media 2x2 a mozaicului.";

        /// <summary>Why it could not compare, in words.</summary>
        /// <param name="o">The run, with a comparison that was not made.</param>
        /// <returns>The reason, with the option that fixes it; "" for None.</returns>
        internal static string GapText(StructureOutcome o)
        {
            VignettingComparison c = o.Profiles;
            ShotIdentity shot = c.Shot;
            switch (c.Gap)
            {
                case ComparisonGap.NoProfiles:
                    return "niciun profil de obiectiv pe masina - nici Adobe (Lightroom / Camera Raw / DNG Converter), nici folderul lensfun/ langa program";
                case ComparisonGap.NoRaw:
                    return o.RawError != null
                        ? $"raw-ul de langa master nu s-a putut citi ({o.RawError}), deci nu se stie obiectivul"
                        : "niciun raw langa master (in flats/), deci niciun ID de obiectiv; un FITS nu spune ce obiectiv l-a facut. Da --lens <text> sau --raw <fisier>";
                case ComparisonGap.NoLensMatch:
                    return shot != null && shot.HasRawLensId
                        ? $"{shot.RawLensLabel}: niciun obiectiv din catalog nu i se potriveste (dupa ID si gama). Da --lens <text>"
                        : "niciun obiectiv nu se potriveste cu textul dat; vezi --lenses";
                case ComparisonGap.NoFocal:
                    return "focala nu se stie - nici FOCALLEN in master, nici raw; da --focal <mm>";
                case ComparisonGap.NoFStop:
                    return "diafragma nu se stie - Siril n-o scrie in master, si n-a fost niciun raw langa el; da --fstop <f> sau --raw <fisier>";
                case ComparisonGap.NoCrop:
                    return $"doar profile lensfun, si crop-ul aparatului nu se stie ({shot?.CropSource}); da --crop <factor>";
                default:
                    return "";
            }
        }

        /// <summary>
        /// What the comparison would have shown, so that it is known what is missing.
        /// </summary>
        /// <returns>Two lines joined by '\n'.</returns>
        internal static string GapMeaning()
            => "Comparatia ar fi spus daca adancimea vignetarii e cea a obiectivului: un exces radial - caderea\n"
             + "unui panou, curbura cerului - trece neobservat prin cele cinci teste si prin --ratio.";

        /// <summary>
        /// The two parts of the verdict, on two rows, as the console prints them under VERDICT.
        /// </summary>
        /// <param name="o">The run, with a comparison made.</param>
        /// <returns>The shape row, the depth row and <see cref="WorseOfBoth"/>, joined by
        /// '\n'.</returns>
        internal static string VerdictDetail(StructureOutcome o)
            => $"forma     : {ShapeDetail(o)}\nadancimea : {DepthDetail(o)}\n{WorseOfBoth}";

        /// <summary>
        /// "PERFECT - cele cinci teste de abatere de la simetria radiala". Separate, so that the
        /// window can put its own label in front.
        /// </summary>
        /// <param name="o">The run.</param>
        /// <returns>The shape verdict and what it covers.</returns>
        internal static string ShapeDetail(StructureOutcome o)
            => $"{o.Structure.Verdict} - cele cinci teste de abatere de la simetria radiala";

        /// <summary>
        /// "REFA - +0.34 stopuri fata de lensfun Tokina ... = 27.0 % eroare in coltul light-ului
        /// calibrat".
        /// </summary>
        /// <param name="o">The run, with a comparison made.</param>
        /// <returns>The depth verdict, the difference against the nearest profile and its corner
        /// error.</returns>
        internal static string DepthDetail(StructureOutcome o)
        {
            ProfileDepth n = o.Profiles.Nearest;
            return $"{o.Profiles.Verdict} - {n.DiffStops:+0.00;-0.00} stopuri fata de {n.Profile.Lens.SourceLabel} {n.Profile.Lens.Title} = {n.ErrorPct:F1} % eroare in coltul light-ului calibrat";
        }

        internal const string WorseOfBoth = "Cel mai rau dintre ele.";

        /// <summary>
        /// The run's advice: the depth's when it worsened the verdict, otherwise the shape's.
        /// </summary>
        /// <param name="o">The run, with a comparison made.</param>
        /// <returns>The advice, lines joined by '\n'.</returns>
        internal static string Advice(StructureOutcome o)
            => o.DepthCarriesTheVerdict ? DepthAdvice(o.Profiles.Nearest.DiffStops, o.Profiles.Verdict) : Advice(o.Structure.Verdict);

        /// <summary>
        /// What the person should do when the master's vignetting is not the lens's. The sign is
        /// the diagnosis: plus = the master falls more than the model, so the SOURCE falls with
        /// the angle (measured on the tablet masters: cos^2 of the field angle, +0.57 stops, made
        /// radial by the 90-degree rotation); minus = the source fills the corners (the sky's dome
        /// brightens toward the horizon all around the zenith, ambient light, a reflection).
        /// Neither --ratio nor more rotations help: the excess rotates with the camera or is
        /// symmetrised by the rotation.
        /// </summary>
        /// <param name="diffStops">The master's depth minus the nearest profile's, stops.</param>
        /// <param name="verdict">The depth verdict; "REFA" adds the advice to reshoot.</param>
        /// <returns>The cause by the sign and its weight, lines joined by '\n'.</returns>
        internal static string DepthAdvice(double diffStops, string verdict)
        {
            string cause = diffStops > 0
                ? "Masterul cade spre colt MAI MULT decat modelul obiectivului: sursa de lumina cade cu\n"
                + "unghiul - un panou (monitor, tableta) prea aproape sau fara un difuzor adevarat; foile\n"
                + "mate nu difuzeaza. Ajuta un difuzor lambertian (acril opal, un teanc gros de hartie\n"
                + "alba) sau un perete alb luminat din lateral; nu ajuta mai multe rotatii."
                : "Masterul cade spre colt MAI PUTIN decat modelul obiectivului: sursa umple colturile - o\n"
                + "cupola de cer (se lumineaza spre orizont de jur imprejur), lumina ambianta pe langa\n"
                + "sursa, o reflexie in parasolar. Fereste obiectivul de tot ce nu e sursa; pe cer, fereste\n"
                + "zenitul si asteapta un cer mai uniform.";
            string weight = verdict == "REFA"
                ? "\nExcesul e radial, deci cele cinci teste si --ratio nu-l vad; nu-l va scoate nici background\n"
                + "extraction. Refa flat-urile."
                : "\nExcesul e radial, deci cele cinci teste si --ratio nu-l vad. Sub ACCEPTABIL nu se deosebeste\n"
                + "de incertitudinea modelului (cele doua baze difera intre ele cu ~0.2 stopuri).";
            return cause + weight;
        }

        /// <summary>
        /// The quantities that are NOT tests. Vignetting is a property of the
        /// lens, not an error. The offset of the illumination centre was a
        /// test too until 1.2.0 and no longer is: being ~ -b/(2d), that is the ratio of the
        /// gradient to the curvature, it is not scale-invariant - and above the threshold at which
        /// the vertex leaves the frame, not even monotonic. See Metrics.FitCenter.
        /// </summary>
        /// <param name="r">The structure result.</param>
        /// <param name="w">Where to write.</param>
        static void WriteInformative(StructureResult r, TextWriter w)
        {
            w.WriteLine("\n  INFORMATIV (nu sunt teste)");
            w.WriteLine($"    vignetare centru->colt  : {r.VignetteStops:F2} stopuri (colt = {CornerOverCentrePct(r):F0}% din centru)");
            if (r.CentroidValid)
            {
                w.WriteLine($"    offset centru iluminare : {r.CentroidOffsetPct,5:F2} %");
                w.WriteLine($"        varf iluminare = ({r.FitCx:F0}, {r.FitCy:F0})  vs  centru senzor = ({r.SensorCx:F0}, {r.SensorCy:F0})");
            }
            else
            {
                w.WriteLine("    offset centru iluminare : nedeterminat (varf in afara cadrului sau fit degenerat)");
            }
            w.WriteLine("        E raportul dintre gradient si curbura, deci nu e scale-invariant: pe un");
            w.WriteLine("        obiectiv aproape fara vignetare creste mult fara ca eroarea sa creasca,");
            w.WriteLine("        iar peste un prag cade inapoi spre 0. Amplitudinea o da \"inclinare\".");
            w.WriteLine($"    pete locale (praf etc.) : {LocalSpotValue(r)}");
            foreach (string line in LocalSpotNote().Split('\n')) w.WriteLine("        " + line);
            string warning = LocalSpotWarning(r);
            if (warning != null) Indent(warning, w);
        }

        /// <summary>
        /// The "pete locale" figure: the signed size, what the sign means, and the position in
        /// the analysis raster.
        ///
        /// The sign is named right on the row, not left to be inferred. A minus on a line called
        /// "pete locale" looks like a calculation error if you do not know that the direction is
        /// precisely the information - the first question asked after the figure appeared was
        /// whether it can be negative.
        /// </summary>
        /// <param name="r">The structure result.</param>
        /// <returns>The signed percentage, the sign's word and the position.</returns>
        internal static string LocalSpotValue(StructureResult r)
            => LocalSpotValue(r.LocalSpotPct, r.LocalSpotX, r.LocalSpotY);

        /// <summary>
        /// The same wording for a spot measured on another raster - the smoothing mode reads it
        /// from the removed component, not from the structure's residual.
        /// </summary>
        /// <param name="pct">The spot's depth, percent, signed.</param>
        /// <param name="x">Its column, analysis raster pixels.</param>
        /// <param name="y">Its row, analysis raster pixels.</param>
        /// <returns>"+N.NN % (mai luminos) la (x, y)", with no word at zero.</returns>
        internal static string LocalSpotValue(double pct, int x, int y)
        {
            // What rounds to zero IS SHOWN as zero: .NET keeps the sign of a negative zero and
            // "-0.00001" came out "-+0.00" (the format's positive section, with the negative
            // zero's minus in front). Measured on the smoothing mode's golden.
            double shown = Math.Abs(pct) < 0.005 ? 0.0 : pct;
            return $"{shown:+0.00;-0.00} %{SignWord(shown)} la ({x}, {y})";
        }

        /// <summary>
        /// A test's label, keyed by the name of the figure in <see cref="StructureResult"/>,
        /// like <see cref="TestNote"/>. It is what WriteBody prints in front of the figure; the
        /// smoothing report reuses it in its "inainte / dupa" table, so that the two reports name
        /// the tests the same way.
        /// </summary>
        /// <param name="metric">One of <see cref="TestKeys"/>.</param>
        /// <returns>The label; null for an unknown key.</returns>
        internal static string TestLabel(string metric) => metric switch
        {
            nameof(StructureResult.CornerSpreadPct) => "imprastiere 4 colturi",
            nameof(StructureResult.AzimuthalMaxPct) => "asimetrie azimutala max",
            nameof(StructureResult.ResidualPtpPct) => "reziduu dupa model rad.",
            nameof(StructureResult.TiltPct) => "inclinare (gradient)",
            nameof(StructureResult.MaxTilePct) => "pata maxima (o dala)",
            _ => null,
        };

        /// <summary>The keys of the five tests, in the report's order.</summary>
        internal static readonly string[] TestKeys =
        {
            nameof(StructureResult.CornerSpreadPct), nameof(StructureResult.AzimuthalMaxPct),
            nameof(StructureResult.ResidualPtpPct), nameof(StructureResult.TiltPct),
            nameof(StructureResult.MaxTilePct),
        };

        /// <summary>
        /// The figure and the verdict of the test <paramref name="metric"/> of
        /// <paramref name="r"/>.
        /// </summary>
        /// <param name="r">The structure result.</param>
        /// <param name="metric">One of <see cref="TestKeys"/>; any other throws an
        /// ArgumentException.</param>
        /// <returns>The value, percent, and its verdict word.</returns>
        internal static (double Value, string Verdict) Test(StructureResult r, string metric) => metric switch
        {
            nameof(StructureResult.CornerSpreadPct) => (r.CornerSpreadPct, r.CornerVerdict),
            nameof(StructureResult.AzimuthalMaxPct) => (r.AzimuthalMaxPct, r.AzimuthalVerdict),
            nameof(StructureResult.ResidualPtpPct) => (r.ResidualPtpPct, r.ResidualVerdict),
            nameof(StructureResult.TiltPct) => (r.TiltPct, r.TiltVerdict),
            nameof(StructureResult.MaxTilePct) => (r.MaxTilePct, r.MaxTileVerdict),
            _ => throw new ArgumentException("test necunoscut: " + metric),
        };

        /// <summary>
        /// The direction of the deviation, in words. Empty at exactly zero (a constant residual),
        /// where "brighter" would be a statement about nothing.
        /// </summary>
        /// <param name="pct">The spot's depth as shown, percent, signed.</param>
        /// <returns>" (mai intunecat)", " (mai luminos)", or "" at zero.</returns>
        static string SignWord(double pct)
            => pct < 0 ? " (mai intunecat)" : pct > 0 ? " (mai luminos)" : "";

        /// <summary>
        /// What the figure is, what its sign means, and from what value a warning follows.
        ///
        /// The threshold is INTERPOLATED from <see cref="Th.LocalSpotWarn"/>, not written as a
        /// literal: otherwise a recalibration of the threshold would leave the text stating the
        /// old value, and this very text is the only place from which the user learns from where
        /// on the figure matters. Pinned by <c>TheLocalSpotNoteQuotesTheThresholdFromTh</c>.
        ///
        /// Returned as a string, like <see cref="Advice(StructureOutcome)"/>: the window shows it
        /// too, under the row.
        /// </summary>
        /// <returns>Four lines joined by '\n'.</returns>
        internal static string LocalSpotNote()
            => "Cea mai adanca pata compacta, dupa scoaterea structurii de scara mare.\n"
             + "Semnul spune ce fel: minus = mai intunecat decat imprejurimile (praf,\n"
             + "condens, un obstacol), plus = mai luminos (scurgere de lumina, reflexie).\n"
             + $"Peste {Th.LocalSpotWarn:F0} % urmeaza o atentionare; sub atat cifra e doar informativa.";

        /// <summary>A corner's level as a percentage of the four corners' mean - the ratio of the
        /// corner rows, in the console and in the window.</summary>
        /// <param name="r">The structure result.</param>
        /// <param name="corner">The corner's level, in the raster's units.</param>
        /// <returns>The percentage; 100 for a corner at the mean.</returns>
        static double CornerSharePct(StructureResult r, double corner) => corner / r.CornerMean * 100;

        /// <summary>The corners' mean as a percentage of the centre's median - the "colt = N %
        /// din centru" of the vignetting row, in the console and in the window.</summary>
        /// <param name="r">The structure result.</param>
        /// <returns>The percentage; 100 without vignetting.</returns>
        static double CornerOverCentrePct(StructureResult r) => r.CornerMean / r.CentreMedian * 100;

        /// <summary>The four corners, each as a percentage of their mean, on one line - the
        /// window's version of the corner rows Write prints under the corner spread.</summary>
        /// <param name="r">The structure result.</param>
        /// <returns>Each corner's name and percentage with one decimal, separated by three
        /// spaces.</returns>
        internal static string CornerShares(StructureResult r)
            => string.Join("   ", r.Corners.Select(
                c => $"{c.Key} {CornerSharePct(r, c.Value):F1} %"));

        /// <summary>The note under the window's vignetting row: the corner as a percentage of
        /// the centre, as WriteInformative prints it, and why it is not an error.</summary>
        /// <param name="r">The structure result.</param>
        /// <returns>One line.</returns>
        internal static string VignetteNote(StructureResult r)
            => $"coltul e {CornerOverCentrePct(r):F0} % din centru; "
             + "e o proprietate a obiectivului, nu o eroare";

        /// <summary>The note under the window's illumination-centre row: the fitted vertex
        /// against the sensor's centre and why the figure is not scale-invariant, or why there is
        /// no vertex.</summary>
        /// <param name="r">The structure result.</param>
        /// <returns>One line.</returns>
        internal static string CentroidNote(StructureResult r)
            => r.CentroidValid
                ? $"varf ({r.FitCx:F0}, {r.FitCy:F0}) vs centru senzor ({r.SensorCx:F0}, {r.SensorCy:F0}); "
                  + "raport gradient/curbura, deci NU e scale-invariant - amplitudinea o da inclinarea"
                : "varf in afara cadrului sau fit degenerat";

        /// <summary>
        /// The warning for a compact spot, or null when there is none. It is not a verdict,
        /// and that is why it does not vote: a spot fixed to the camera is right for the session
        /// whose lights have it too, and wrong for a library flat - the answer depends on what you
        /// do with the flat, not on the flat. Returned as a string because the window shows it
        /// too, under the verdict.
        /// </summary>
        /// <param name="r">The structure result.</param>
        /// <returns>Four lines joined by '\n' when the spot is flagged, else null.</returns>
        internal static string LocalSpotWarning(StructureResult r)
            => r.LocalSpotFlagged
                ? $"ATENTIE: pata compacta de {Math.Abs(r.LocalSpotPct):F2} % la ({r.LocalSpotX}, {r.LocalSpotY}), peste {Th.LocalSpotWarn:F0} %.\n"
                + "    E fixa pe aparat - praf pe senzor sau pe filtru, condens, o scurgere mica.\n"
                + "    Corecta pentru sesiunea ale carei lights au aceeasi pata; NU pentru un\n"
                + "    flat de biblioteca. Cauta pozitia de mai sus in harta reziduala."
                : null;

        /// <summary>
        /// What each of the five tests measures, in one sentence.
        ///
        /// NO CALLER IN THE CONSOLE, like <see cref="RunReport.MapScaleLabel"/> and the others:
        /// the console report keeps its explanations grouped under the scale, and five notes
        /// interleaved between the rows would drown the very figures. The window puts each note
        /// under its row, where it costs nothing.
        ///
        /// They sit here all the same, and not in the ViewModel, because they are sentences of
        /// diagnosis: the rule is that no explanation for the user is written in a ViewModel,
        /// so that there are not two wordings of the same thing when another consumer appears.
        /// Two of them were in fact written inline there.
        ///
        /// The key is the name of the figure in <see cref="StructureResult"/>, not an index: a
        /// list by position would break silently if someone reorders the rows.
        /// </summary>
        /// <param name="metric">One of <see cref="TestKeys"/>.</param>
        /// <returns>The note; null for an unknown key.</returns>
        internal static string TestNote(string metric) => metric switch
        {
            nameof(StructureResult.CornerSpreadPct) =>
                "cat difera intre ele cele patru cutii de colt; pe un camp pur radial ar fi egale",

            nameof(StructureResult.AzimuthalMaxPct) =>
                "cat difera intre ele cele 12 sectoare ale aceluiasi inel, pe inelul cel mai "
              + "prost; doar in cercul inscris, singurul loc unde toate azimuturile sunt "
              + "esantionate",

            nameof(StructureResult.ResidualPtpPct) =>
                "masoara direct cat din cadru NU se explica printr-un model pur radial",

            nameof(StructureResult.TiltPct) =>
                "amplitudinea varf-la-varf a gradientului liniar peste cadru; prinde si o axa "
              + "optica decentrata, ponderata cu cat costa ea fotometric",

            nameof(StructureResult.MaxTilePct) =>
                "cea mai abatuta dala de pe tot cadrul; prinde un defect compact pe care "
              + "reziduul il taie ca extrema",

            _ => null,
        };

        /// <summary>
        /// The common scale of the five tests of deviation from radial symmetry: the band's
        /// name, the interval and the meaning. It does not apply to the offset of the illumination
        /// centre, which measures a position.
        ///
        /// It is returned as data, not as text already written, because two consumers read it:
        /// the report below, which lays it out in columns, and the WPF application, which puts it
        /// in a table with the current band highlighted. The thresholds stay the only ones in Th;
        /// a second list of bands would have diverged at the first recalibration.
        /// </summary>
        /// <returns>Four rows, PERFECT to REFA: the band, its interval in percent, its
        /// meaning.</returns>
        internal static IReadOnlyList<(string Band, string Range, string Meaning)> Scale()
            => new[]
            {
                ("PERFECT", $"sub {Th.ResidPerfect:F0} %",
                            "calitate perfecta"),
                ("BUN", $"{Th.ResidPerfect:F0} - {Th.ResidUsable:F0} %",
                        "utilizabil; background extraction scoate ce ramane"),
                ("ACCEPTABIL", $"{Th.ResidUsable:F0} - {Th.ResidCheck:F0} %",
                               "utilizabil, dar confirma cu --light inainte de un stretch dur"),
                ("REFA", $"peste {Th.ResidCheck:F0} %",
                         "refa flat-urile"),
            };

        /// <summary>
        /// Why the residual after the radial model is the one that matters. Taken out separately
        /// for the same reason as <see cref="Scale"/>: the window shows it too, under the table.
        /// </summary>
        /// <returns>Eight lines joined by '\n'.</returns>
        internal static string WhyResidualMatters()
            => "Cel mai important e \"reziduu dupa model rad.\": masoara direct cat din\n"
             + "cadru NU se explica printr-un model pur radial. E o LIMITA SUPERIOARA a\n"
             + "erorii de flat, nu eroarea insasi - cuprinde si partea fixa pe aparat\n"
             + "(optica; se imparte corect din lights) si partea fixa in lume (iluminarea;\n"
             + "nu se reproduce). Modul --ratio le desparte. Imprastierea colturilor,\n"
             + "asimetria azimutala si inclinarea sunt vederi partiale ale aceleiasi abateri.\n"
             + "\"Pata maxima\" prinde ce le scapa celorlalte: un defect compact - o scurgere\n"
             + "mica, o pata de condens - pe care reziduul il taie ca extrema.";

        /// <summary>Writes the residual scale in columns, and why the residual matters.</summary>
        /// <param name="w">Where to write.</param>
        static void WriteScale(TextWriter w)
        {
            w.WriteLine("\n  SCARA celor cinci teste de abatere de mai sus");
            foreach (var (_, range, meaning) in Scale())
                w.WriteLine($"    {range,-13}{meaning}");
            Indent(WhyResidualMatters(), w);
        }

        /// <summary>
        /// What the person should do with this verdict. Returned as a string, not written
        /// directly, because the window shows it too, under the coloured verdict - and two
        /// wordings of the same advice would diverge exactly where it matters most not to diverge.
        /// </summary>
        /// <param name="verdict">A word of the residual scale; anything else gets REFA's
        /// advice.</param>
        /// <returns>The advice, lines joined by '\n'.</returns>
        internal static string Advice(string verdict) => verdict switch
        {
            "PERFECT" =>
                "Abaterea de la simetria radiala e sub zgomot. Flat-ul e curat.",

            "BUN" =>
                "Utilizabil ca atare. Ce ramane sta in intervalul pe care background\n"
              + "extraction il scoate fara efort.",

            "ACCEPTABIL" =>
                "Utilizabil, dar ruleaza modul --light pe un light calibrat inainte de\n"
              + "un stretch dur, ca sa confirmi ca flat-ul chiar corecteaza.",

            // Points to --ratio BEFORE "reshoot": the figure is an upper bound, and a part fixed
            // to the camera (a genuinely non-radial optic, an off-centre axis) comes out here
            // too, without being a flat error. Measured: an axis off-centre by 5% of the
            // width gives REFA on a flat that corrects perfectly.
            _ =>
                "Abatere prea mare de la simetria radiala => sursa neuniforma, light\n"
              + "leak, camera inclinata, sau ambient. NU e vignetare. Inainte sa refaci,\n"
              + "ruleaza --ratio pe doua stive rotite: daca abaterea e fixa pe aparat,\n"
              + "flat-ul e corect. Altfel refa flat-urile. Vezi harta pentru forma problemei.",
        };

        /// <summary>Writes the advice of a verdict, indented.</summary>
        /// <param name="verdict">A word of the residual scale.</param>
        /// <param name="w">Where to write.</param>
        static void WriteAdvice(string verdict, TextWriter w) => Indent(Advice(verdict), w);

        /// <summary>Writes each line of a text indented by four spaces.</summary>
        /// <param name="text">The lines, joined by '\n'.</param>
        /// <param name="w">Where to write.</param>
        static void Indent(string text, TextWriter w)
        {
            foreach (string line in text.Split('\n')) w.WriteLine("    " + line);
        }
    }
}
