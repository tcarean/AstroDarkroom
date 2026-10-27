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

// One flat against another: the structure of each and of the A / B ratio, side by side.
// It exists for the synthetic flat - how much a real master differs from the lens's model,
// and in what shape - but it works on any pair of flats of the same lens: two diffuser
// thicknesses, two sources, the README's convergence test. It does not attribute the
// difference to the camera or to the world; it only says by how much and how two files differ.
//
// Three columns, not one: the first report showed only the battery on the ratio, and the
// figure "vignetting -0.42 stops" could not be read without knowing that the master has 0.26
// and the synthetic 0.69. Both are prepared (Img.Prepare) and normalised to median 1; A, B and
// A / B each go through StructureAnalyzer.AnalyzePrepared, so the figures mean the same as in
// structure, and in the difference column they are about THE DIFFERENCE: the ratio's
// vignetting is the depth difference in stops (positive = A falls off more towards the corner
// than B), the five tests say whether the difference is radial or not, the map shows its
// shape. The verdict is the difference's, and the text says so. The rows' notes are the
// comparison's, not the structure tab's: "it is a property of the lens, not an error" would
// have been exactly backwards here.
//
// The difference's verdict has two parts: THE SHAPE - the five tests on the ratio, which say
// whether what differs is radial - and THE DEPTH - the vignetting difference in stops. The
// first version had only the shape, and the sky master, with 0.42 stops less vignetting than
// the model, came out ACCEPTABIL on shape as if the difference were small; the user read it
// as "enormous, something is very wrong", and was right. The depth gets no new threshold: a
// difference of D stops at the corner leaves in the calibrated light's corner an error of
// 2^|D| - 1, a fractional amplitude like all the others, so it goes through the same residual
// scale (2 / 10 / 20 %): under 0.03 stops PERFECT, under 0.14 BUN, under 0.26 ACCEPTABIL,
// otherwise REFA. On the Tokina masters: sky -0.42 -> 34 % REFA; near-monitor +0.16 -> 12 %
// ACCEPTABIL; on-monitor +0.09 -> 6 % BUN; Adobe against lensfun 0.16 -> ACCEPTABIL - the two
// databases differ from each other by ~0.2 stops on the same lens, so under ACCEPTABIL the
// difference cannot be told from the model's uncertainty, and the row's note says so. The
// overall verdict is the worse of shape and depth. The chips on the five tests in the
// difference column were removed: "PERFECT" beside 0.11 % on a ratio said nothing the verdict
// at the top does not say.

using System;
using NLog;
using System.Collections.Generic;
using System.IO;
using static FlatCheck.Ui;

namespace FlatCheck
{
    /// <summary>
    /// What <c>--against</c> measured: the structure of A, of B and of A / B, and the
    /// difference's verdict from its shape and its depth.
    /// </summary>
    internal sealed class AgainstResult
    {
        /// <summary>The structure of A, of B and of the A / B ratio, all at the analysis
        /// resolution.</summary>
        internal readonly StructureResult A, B, Structure;
        internal readonly Img Ratio;
        internal readonly int Width, Height;

        /// <summary>The depth difference in stops (the ratio's), and the error it leaves at the
        /// corner, in percent.</summary>
        internal readonly double DepthDiffStops, DepthErrorPct;
        /// <summary>The depth's verdict, on the residual scale applied to the corner
        /// error.</summary>
        internal readonly string DepthVerdict;
        /// <summary>The difference's verdict: the worse of the shape (the five tests on the
        /// ratio) and the depth.</summary>
        internal readonly string Verdict;

        /// <summary>Holds the three structures and derives the depth figures and the combined
        /// verdict from the ratio's.</summary>
        /// <param name="a">The structure of A.</param>
        /// <param name="b">The structure of B.</param>
        /// <param name="ratio">The structure of A / B.</param>
        /// <param name="ratioRaster">The A / B raster, at the analysis resolution, median
        /// about 1.</param>
        /// <param name="w">A's width at full resolution, pixels.</param>
        /// <param name="h">A's height at full resolution, pixels.</param>
        internal AgainstResult(StructureResult a, StructureResult b, StructureResult ratio, Img ratioRaster, int w, int h)
        {
            A = a;
            B = b;
            Structure = ratio;
            Ratio = ratioRaster;
            Width = w;
            Height = h;
            DepthDiffStops = ratio.VignetteStops;
            DepthErrorPct = Stats.DepthErrorPct(DepthDiffStops);
            DepthVerdict = Stats.DepthVerdict(DepthDiffStops);
            Verdict = Stats.Severity(DepthVerdict) > Stats.Severity(ratio.Verdict) ? DepthVerdict : ratio.Verdict;
        }

        /// <summary>
        /// The corner error for a depth difference; it is <see cref="Stats.DepthErrorPct"/>,
        /// which now has a second consumer too.
        /// </summary>
        /// <param name="stops">A depth difference, in stops; either sign.</param>
        /// <returns>The error 2^|stops| - 1, in percent.</returns>
        internal static double DepthErrorOf(double stops) => Stats.DepthErrorPct(stops);
    }

    /// <summary>
    /// Compares two flats of the same lens: prepares and normalises both and runs the
    /// structure battery on each and on their ratio.
    /// </summary>
    internal static class AgainstAnalyzer
    {
        /// <summary>
        /// Prepares both flats, normalises each to median 1, divides them and analyses all
        /// three. Throws an <see cref="ArgumentException"/> when the two prepared rasters
        /// differ in size or a median is not positive.
        /// </summary>
        /// <param name="a">The first flat, as loaded (the synthetic one or the master).</param>
        /// <param name="b">The second flat, as loaded.</param>
        /// <returns>The three structures and the difference's verdict.</returns>
        internal static AgainstResult Analyze(Img a, Img b)
        {
            Img pa = Img.Prepare(a), pb = Img.Prepare(b);
            if (pa.W != pb.W || pa.H != pb.H)
                throw new ArgumentException(T($"the two flats have different sizes at the analysis resolution ({pa.W}x{pa.H} against {pb.W}x{pb.H})",
                                              $"cele doua flaturi au marimi diferite la rezolutia de analiza ({pa.W}x{pa.H} fata de {pb.W}x{pb.H})"));
            Normalise(pa);
            Normalise(pb);
            Img ratio = Img.Divide(pa, pb);
            return new AgainstResult(StructureAnalyzer.AnalyzePrepared(pa), StructureAnalyzer.AnalyzePrepared(pb),
                StructureAnalyzer.AnalyzePrepared(ratio), ratio, a.W, a.H);
        }

        /// <summary>Divides a raster by its median, in place. Throws an
        /// <see cref="ArgumentException"/> when the median is not positive.</summary>
        /// <param name="img">The prepared raster; modified.</param>
        static void Normalise(Img img)
        {
            double med = Stats.Percentile((double[])img.D.Clone(), 50);
            if (!(med > 0)) throw new ArgumentException(T("a flat with a zero or negative median cannot be normalised",
                                                          "un flat cu mediana zero sau negativa nu se poate normaliza"));
            for (int i = 0; i < img.D.Length; i++) img.D[i] /= med;
        }
    }

    /// <summary>A row of the table: the same quantity on A, on B and on A / B, with the verdict
    /// where there is a test.</summary>
    /// <param name="Key">The quantity's key (a <c>StructureResult</c> member name, as in
    /// <c>StructureReport.TestKeys</c>).</param>
    /// <param name="Label">The row's label, in the run's language.</param>
    /// <param name="A">The value on A, formatted.</param>
    /// <param name="AVerdict">The verdict on A, or null.</param>
    /// <param name="B">The value on B, formatted.</param>
    /// <param name="BVerdict">The verdict on B, or null.</param>
    /// <param name="Diff">The value on A / B, formatted.</param>
    /// <param name="DiffVerdict">The verdict on the difference, or null (only the depth row
    /// carries one).</param>
    /// <param name="Note">The row's note, about the difference.</param>
    /// <param name="IsKey">True for the row the verdict hangs on (the depth).</param>
    internal sealed record AgainstRow(string Key, string Label, string A, string AVerdict, string B, string BVerdict,
                                      string Diff, string DiffVerdict, string Note, bool IsKey);

    /// <summary>
    /// The words of <c>--against</c>: the console's block, and the pieces the window's
    /// Sintetic tab shows (rows, verdict detail, meaning).
    /// </summary>
    internal static class AgainstReport
    {
        /// <summary>Writes the console's report: the files, the meaning, the table and the
        /// verdict with its two parts.</summary>
        /// <param name="r">The comparison.</param>
        /// <param name="pathA">The first file, as given.</param>
        /// <param name="pathB">The second file, as given.</param>
        /// <param name="w">Where the text is written.</param>
        internal static void Write(AgainstResult r, string pathA, string pathB, TextWriter w)
        {
            w.WriteLine(T("\n=== COMPARISON: A / B ===", "\n=== COMPARATIE: A / B ==="));
            w.WriteLine($"    A : {pathA}");
            w.WriteLine($"    B : {pathB}");
            w.WriteLine(T($"    analysis size      : {r.Structure.Residual.W} x {r.Structure.Residual.H} (after 2x2 + downsample)",
                          $"    dimensiune analiza : {r.Structure.Residual.W} x {r.Structure.Residual.H} (dupa 2x2 + downsample)"));
            w.WriteLine();
            foreach (string line in Meaning().Split('\n')) w.WriteLine("    " + line);

            w.WriteLine($"\n  {"",-28}{"A",-38}{"B",-38}{T("A / B (difference)", "A / B (diferenta)")}");
            foreach (AgainstRow row in Rows(r))
                w.WriteLine($"    {row.Label,-26}{Cell(row.A, row.AVerdict),-38}{Cell(row.B, row.BVerdict),-38}{Cell(row.Diff, row.DiffVerdict)}");

            w.WriteLine(T($"\n  THE DIFFERENCE'S VERDICT: {VerdictWords.Of(r.Verdict)}", $"\n  VERDICTUL DIFERENTEI: {VerdictWords.Of(r.Verdict)}"));
            w.WriteLine(T($"    shape     : {ShapeDetail(r)}", $"    forma     : {ShapeDetail(r)}"));
            w.WriteLine(T($"                (under 2 % PERFECT, 2-10 GOOD, 10-20 ACCEPTABLE, over 20 REDO)",
                          $"                (sub 2 % PERFECT, 2-10 BUN, 10-20 ACCEPTABIL, peste 20 REFA)"));
            w.WriteLine(T($"    depth     : {DepthDetail(r)},", $"    adancimea : {DepthDetail(r)},"));
            w.WriteLine(T($"                on the same scale ({DepthScale()})", $"                pe aceeasi scara ({DepthScale()})"));
            foreach (string line in StructureReport.DepthLightScaleNote(r.DepthDiffStops).Split('\n'))
                w.WriteLine("                " + line);
            w.WriteLine("    " + WorseOfBoth());
        }

        /// <summary>"PERFECT: cele cinci teste pe raport, pe scara de reziduu a
        /// structurii".</summary>
        /// <param name="r">The comparison.</param>
        /// <returns>The shape's verdict with what it is.</returns>
        internal static string ShapeDetail(AgainstResult r)
            => T($"{VerdictWords.Of(r.Structure.Verdict)}: the five tests on the ratio, on the structure's residual scale",
                 $"{VerdictWords.Of(r.Structure.Verdict)}: cele cinci teste pe raport, pe scara de reziduu a structurii");

        /// <summary>"REFA: 0.30 stopuri = 23.1 % eroare in coltul light-ului
        /// calibrat".</summary>
        /// <param name="r">The comparison.</param>
        /// <returns>The depth's verdict with the stops and the corner error.</returns>
        internal static string DepthDetail(AgainstResult r)
            => T($"{VerdictWords.Of(r.DepthVerdict)}: {Stops(r.Structure)} = {r.DepthErrorPct:F1} % error in the corner of the calibrated light",
                 $"{VerdictWords.Of(r.DepthVerdict)}: {Stops(r.Structure)} = {r.DepthErrorPct:F1} % eroare in coltul light-ului calibrat");

        /// <summary>The sentence that closes the verdict: the worse of the two, and what two
        /// correct flats give.</summary>
        /// <returns>One sentence.</returns>
        internal static string WorseOfBoth()
            => T("The worse of the two. Two correct flats of the same lens give PERFECT and 0.00 stops.",
                 "Cel mai rau dintre ele. Doua flaturi corecte ale aceluiasi obiectiv dau PERFECT si 0.00 stopuri.");

        /// <summary>The verdict's two parts on one line, for the window, which does not have
        /// the console's block.</summary>
        /// <param name="r">The comparison.</param>
        /// <returns>The shape, the depth with its scale and its note, and the closing
        /// sentence.</returns>
        internal static string VerdictDetail(AgainstResult r)
            => T($"shape: {ShapeDetail(r)}. Depth: {DepthDetail(r)} ({DepthScale()}). ",
                 $"forma: {ShapeDetail(r)}. Adancimea: {DepthDetail(r)} ({DepthScale()}). ")
             + StructureReport.DepthLightScaleNote(r.DepthDiffStops).Replace('\n', ' ') + " " + WorseOfBoth();

        /// <summary>The depth's bands in stops, derived from the residual scale through
        /// 2^|D| - 1.</summary>
        /// <returns>The bands for <see cref="Th.ResidPerfect"/>, <see cref="Th.ResidUsable"/>
        /// and <see cref="Th.ResidCheck"/>.</returns>
        internal static string DepthScale()
            => T($"under {StopsFor(Th.ResidPerfect):F2} stops PERFECT, under {StopsFor(Th.ResidUsable):F2} GOOD, under {StopsFor(Th.ResidCheck):F2} ACCEPTABLE, over REDO",
                 $"sub {StopsFor(Th.ResidPerfect):F2} stopuri PERFECT, sub {StopsFor(Th.ResidUsable):F2} BUN, sub {StopsFor(Th.ResidCheck):F2} ACCEPTABIL, peste REFA");

        /// <summary>The inverse of <see cref="AgainstResult.DepthErrorOf"/>: the stops at which
        /// the corner error reaches a threshold.</summary>
        /// <param name="errorPct">A corner error, in percent.</param>
        /// <returns>The depth difference in stops, positive.</returns>
        internal static double StopsFor(double errorPct) => Stats.StopsForError(errorPct);

        /// <summary>A table cell: the value, with its verdict in brackets when there is
        /// one.</summary>
        /// <param name="value">The formatted value.</param>
        /// <param name="verdict">The verdict, or null or empty for none.</param>
        /// <returns>The cell's text.</returns>
        static string Cell(string value, string verdict)
            => string.IsNullOrEmpty(verdict) ? value : $"{value} [{VerdictWords.Of(verdict)}]";

        /// <summary>What the three columns mean, for the console's block and the
        /// window.</summary>
        /// <returns>A paragraph, lines separated by '\n'.</returns>
        internal static string Meaning()
            => T("Three columns: each flat through the structure battery, and their ratio. In the A / B column\n"
                 + "the figures are about the DIFFERENCE: \"vignetting\" is the depth difference (positive = A falls\n"
                 + "more toward the corner than B) and carries the depth verdict; the five tests say whether the difference\n"
                 + "is radial or has a shape, and the map shows it. What is in A and not in B - a saddle of the sky, a panel's\n"
                 + "fall-off, a spot - stays in the difference.",
                 "Trei coloane: fiecare flat prin bateria de structura, si raportul lor. In coloana A / B\n"
                 + "cifrele sunt despre DIFERENTA: \"vignetare\" e diferenta de adancime (pozitiv = A cade mai\n"
                 + "mult spre colt decat B) si poarta verdictul adancimii; cele cinci teste spun daca diferenta\n"
                 + "e radiala sau are forma, iar harta o arata. Ce e in A si nu e in B - o sa a cerului, caderea\n"
                 + "unui panou, o pata - ramane in diferenta.");

        /// <summary>
        /// The table's rows: the depth (the key row), the centroid offset, the local spot, then
        /// the five tests, none with a verdict on the difference.
        /// </summary>
        /// <param name="r">The comparison.</param>
        /// <returns>Eight rows, in display order.</returns>
        internal static IReadOnlyList<AgainstRow> Rows(AgainstResult r)
        {
            var rows = new List<AgainstRow>
            {
                new("VignetteStops", T("vignetting centre->corner", "vignetare centru -> colt"),
                    Stops(r.A), null, Stops(r.B), null, Stops(r.Structure), r.DepthVerdict,
                    T("the depth of each and their difference (positive = A falls more toward the corner than B). "
                      + $"The verdict is the difference's: {r.DepthErrorPct:F1} % error in the corner of a light calibrated with the wrong flat (2^|stops| - 1), on the residual scale; "
                      + "below ACCEPTABLE it cannot be told from the model's uncertainty - the two profile databases differ from each other by ~0.2 stops on the same lens.",
                      "adancimea fiecaruia si diferenta lor (pozitiv = A cade mai mult spre colt decat B). "
                      + $"Verdictul e al diferentei: {r.DepthErrorPct:F1} % eroare in coltul unui light calibrat cu flatul gresit (2^|stopuri| - 1), pe scara de reziduu; "
                      + "sub ACCEPTABIL nu se deosebeste de incertitudinea modelului - cele doua baze de profile difera intre ele cu ~0.2 stopuri pe acelasi obiectiv."), true),
                new("CentroidOffsetPct", T("illum. centre offset", "offset centru iluminare"),
                    Offset(r.A), null, Offset(r.B), null, Offset(r.Structure), null,
                    T("where the illumination peak sits against the sensor's centre, on each and on the ratio; "
                      + "near zero on the ratio = the difference is centred",
                      "unde sta varful iluminarii fata de centrul senzorului, la fiecare si la raport; "
                      + "aproape de zero pe raport = diferenta e centrata"), false),
                new("LocalSpotPct", T("local spots (dust etc.)", "pete locale (praf etc.)"),
                    Spot(r.A), null, Spot(r.B), null, Spot(r.Structure), null,
                    T("the deepest compact spot of each; a spot present in only one stays in the difference; "
                      + $"positions: A ({r.A.LocalSpotX}, {r.A.LocalSpotY}), B ({r.B.LocalSpotX}, {r.B.LocalSpotY}), "
                      + $"A / B ({r.Structure.LocalSpotX}, {r.Structure.LocalSpotY})",
                      "cea mai adanca pata compacta a fiecaruia; o pata prezenta doar intr-unul ramane in diferenta; "
                      + $"pozitii: A ({r.A.LocalSpotX}, {r.A.LocalSpotY}), B ({r.B.LocalSpotX}, {r.B.LocalSpotY}), "
                      + $"A / B ({r.Structure.LocalSpotX}, {r.Structure.LocalSpotY})"), false),
            };
            foreach (string key in StructureReport.TestKeys)
            {
                var (va, ba) = StructureReport.Test(r.A, key);
                var (vb, bb) = StructureReport.Test(r.B, key);
                var (vd, bd) = StructureReport.Test(r.Structure, key);
                // No verdict on the difference column: "PERFECT" beside 0.11 % on the ratio said
                // nothing the verdict at the top, which is the whole shape's, does not say.
                rows.Add(new AgainstRow(key, StructureReport.TestLabel(key), Pct(va), ba, Pct(vb), bb, Pct(vd), null,
                    TestNote(key), false));
            }
            return rows;
        }

        /// <summary>A test's note, said about the difference - not about one flat.</summary>
        /// <param name="key">A test key from <c>StructureReport.TestKeys</c>.</param>
        /// <returns>The note, or an empty string for an unknown key.</returns>
        internal static string TestNote(string key) => key switch
        {
            "CornerSpreadPct" => T("how much the four corners differ from each other; uneven corners in the A / B column = non-radial structure that one has and the other has not",
                                   "cat difera cele patru colturi intre ele; colturi inegale in coloana A / B = structura neradiala pe care unul o are si celalalt nu"),
            "AzimuthalMaxPct" => T("how much the 12 sectors of the same ring differ, in the inscribed circle; on the ratio, the azimuthal shape of the difference",
                                   "cat difera cele 12 sectoare ale aceluiasi inel, in cercul inscris; pe raport, forma azimutala a diferentei"),
            "ResidualPtpPct" => T("how much of each, and how much of the difference, a radial model does NOT explain; the most important",
                                  "cat din fiecare, si cat din diferenta, NU se explica printr-un model radial; cel mai important"),
            "TiltPct" => T("the linear gradient of each and of the difference; a source's gradient shows here",
                           "gradientul liniar al fiecaruia si al diferentei; un gradient de sursa apare aici"),
            "MaxTilePct" => T("the most deviant tile; a compact defect present in only one shows in the A / B column",
                              "dala cea mai abatuta; un defect compact prezent intr-unul singur apare in coloana A / B"),
            _ => "",
        };

        /// <summary>The vignetting depth, formatted.</summary>
        /// <param name="s">A structure result.</param>
        /// <returns>"0.26 stops" and the like.</returns>
        static string Stops(StructureResult s) => T($"{s.VignetteStops:F2} stops", $"{s.VignetteStops:F2} stopuri");

        /// <summary>The spot without its position - the positions go in the row's note, so the
        /// cell stays short.</summary>
        /// <param name="s">A structure result.</param>
        /// <returns><c>StructureReport.LocalSpotSize</c>: the size and the sign's word.</returns>
        static string Spot(StructureResult s) => StructureReport.LocalSpotSize(s.LocalSpotPct);
        /// <summary>The centroid offset, formatted.</summary>
        /// <param name="s">A structure result.</param>
        /// <returns>The offset in percent, or "undetermined" when the centroid is not
        /// valid.</returns>
        static string Offset(StructureResult s) => s.CentroidValid ? $"{s.CentroidOffsetPct:F2} %" : T("undetermined", "nedeterminat");
        /// <summary>A percentage, formatted.</summary>
        /// <param name="v">A value in percent, NaN when not measured.</param>
        /// <returns>The value with two decimals and " %", or "n/a" for NaN.</returns>
        static string Pct(double v) => double.IsNaN(v) ? "n/a" : $"{v:F2} %";
    }

    /// <summary>
    /// Mode 7's <c>--against</c>: loads two flats of the same lens, compares them and writes
    /// the report; the map is the ratio's residual.
    /// </summary>
    internal static class AgainstRun
    {
        /// <summary>
        /// Loads both files, compares them (<see cref="AgainstAnalyzer.Analyze"/>), writes the
        /// report and encodes the ratio's residual map. Throws what
        /// <see cref="ImageLoader.Load"/> or the analyzer throws.
        /// </summary>
        /// <param name="pathA">The first flat (A in the table).</param>
        /// <param name="pathB">The second flat (B).</param>
        /// <param name="report">Where the report's text is written.</param>
        /// <returns>The comparison and the A / B residual map as PNG.</returns>
        internal static RunOutcome<AgainstResult> Execute(string pathA, string pathB, TextWriter report)
        {
            using RunClock clock = RunClock.Start(Log, "against", pathA + " against " + pathB);
            var fa = ImageLoader.Load(pathA);
            var fb = ImageLoader.Load(pathB);
            clock.Lap("load");
            var r = AgainstAnalyzer.Analyze(fa.Image, fb.Image);
            clock.Lap("analyze");
            if (Log.IsDebugEnabled) Log.Debug("comparison of " + pathA + " with " + pathB + ": " + LogText.Against(r));
            AgainstReport.Write(r, pathA, pathB, report);
            clock.Lap("report");
            byte[] png = PngWriter.EncodeResidualMap(r.Structure.Residual);
            clock.Lap("map");
            clock.Done("verdict=" + r.Verdict);
            return new RunOutcome<AgainstResult>(r, png);
        }

        /// <summary>The diagnostic log: the run's lines and figures (R-110).</summary>
        static readonly Logger Log = LogManager.GetLogger(nameof(AgainstRun));
    }
}
