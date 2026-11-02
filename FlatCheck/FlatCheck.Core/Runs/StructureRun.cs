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

// StructureRun.cs - mode 1 end to end: file -> figures + text + map.
//
// With a StructureQuery, also the comparison of the vignetting with the lens's profiles: the
// identification (LensIdentification, from the master and the raw beside it or from --lens),
// the comparison (VignettingComparer), and the combined verdict - the worst of the five tests
// and the depth, as with --against. Without a query, exactly what it was: the five tests and
// their verdict, so the goldens and crosscheck.py do not move.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using static FlatCheck.Ui;

namespace FlatCheck
{
    /// <summary>
    /// What the caller wants besides the structure: the profiles to compare the vignetting
    /// with. The console builds it from --lens / --raw, the window from the raw in flats/.
    /// </summary>
    internal sealed class StructureQuery
    {
        internal ProfileSources Sources;
        /// <summary>A raw from beside the master, for the aperture and the lens ID; null when
        /// there is none.</summary>
        internal string RawPath;
        /// <summary>The metadata reader; in the tests, a made-up frame in place of a
        /// raw.</summary>
        internal Func<string, LoadedFrame> LoadInfo = ImageLoader.LoadInfo;
        /// <summary>The lenses by text (--lens), in place of those the raw points to; null =
        /// from the raw.</summary>
        internal string LensText;
        internal ProfileSource? Source;
        internal double? Focal, FStop, Crop;
        /// <summary>
        /// The lens chosen among the raw's candidates, by its catalog key
        /// (<see cref="LensChoice.Key"/>): the window's picker on a list found by range alone
        /// (R-121). Null = every candidate. Ignored with a <see cref="LensText"/>, and when the
        /// key matches no candidate.
        /// </summary>
        internal string LensKey;
    }

    /// <summary>
    /// What a structure run found: the five tests, the comparison with the profiles when one
    /// was asked for, and the combined verdict.
    /// </summary>
    internal sealed class StructureOutcome
    {
        internal readonly StructureResult Structure;
        /// <summary>Null without a query; otherwise the comparison, even when it could not
        /// compare (Gap says why).</summary>
        internal readonly VignettingComparison Profiles;
        internal readonly ShotIdentity Shot;
        /// <summary>The raw beside the master could not be read: the message, or
        /// null.</summary>
        internal readonly string RawError;
        /// <summary>The worse of the five tests' verdict and the depth's; the structure's
        /// when nothing was compared.</summary>
        internal readonly string Verdict;

        /// <summary>Holds the parts and combines the verdict by
        /// <see cref="Stats.Severity"/>.</summary>
        /// <param name="structure">The five tests; never null.</param>
        /// <param name="profiles">The comparison, or null when none was asked for.</param>
        /// <param name="shot">The identified shot, or null without a query.</param>
        /// <param name="rawError">The message when the raw could not be read, or null.</param>
        internal StructureOutcome(StructureResult structure, VignettingComparison profiles, ShotIdentity shot, string rawError)
        {
            Structure = structure;
            Profiles = profiles;
            Shot = shot;
            RawError = rawError;
            Verdict = profiles != null && profiles.Compared && Stats.Severity(profiles.Verdict) > Stats.Severity(structure.Verdict)
                ? profiles.Verdict : structure.Verdict;
        }

        internal bool Compared => Profiles != null && Profiles.Compared;

        /// <summary>True when the depth made the verdict worse: the advice is then the depth's,
        /// not the shape's.</summary>
        internal bool DepthCarriesTheVerdict
            => Compared && Stats.Severity(Profiles.Verdict) > Stats.Severity(Structure.Verdict);
    }

    /// <summary>
    /// Mode 1, structure: is the master radially symmetric, and, with a
    /// <see cref="StructureQuery"/>, does its vignetting match the lens's published profile.
    /// </summary>
    internal static class StructureRun
    {
        /// <summary>
        /// Analyses the master (<see cref="Analyze"/>), writes the report and encodes the
        /// residual map. Throws what <see cref="ImageLoader.Load(string)"/> throws.
        /// </summary>
        /// <param name="path">The master flat to analyze: a FITS or a camera raw.</param>
        /// <param name="report">
        /// Where the report is written. The console gives stdout, the window a StringWriter
        /// from which it then saves "Rezultat testare.txt" - the same text, by construction.
        /// </param>
        /// <param name="query">Null = the structure only; otherwise the comparison with the
        /// profiles too.</param>
        /// <returns>The outcome and the residual map as PNG.</returns>
        internal static RunOutcome<StructureOutcome> Execute(string path, TextWriter report, StructureQuery query = null)
        {
            using RunClock clock = RunClock.Start(Log, "structure", path + Describe(query));
            StructureOutcome o = Analyze(path, query, clock);
            StructureReport.Write(o, path, report);
            clock.Lap("report");
            byte[] png = PngWriter.EncodeResidualMap(o.Structure.Residual);
            clock.Lap("map");
            clock.Done("verdict=" + o.Verdict);
            return new RunOutcome<StructureOutcome>(o, png);
        }

        /// <summary>
        /// The comparison redone on a structure already computed, and the report written again
        /// (R-118, ADR 0078): with a query, <see cref="Compare"/>; without one, the plain outcome
        /// of the five tests. The window calls it when its "no filters" box is flipped, so the
        /// master is not loaded or analysed a second time; the map is unchanged, so it is not
        /// encoded again. The report is the one <see cref="Execute"/> writes for the same query.
        /// </summary>
        /// <param name="r">The five tests on the master, already computed.</param>
        /// <param name="master">The master's frame, for its header; its pixels are not
        /// read.</param>
        /// <param name="path">The master's path, named in the report.</param>
        /// <param name="report">Where the report is written.</param>
        /// <param name="query">What to compare with; null = the structure only.</param>
        /// <returns>The outcome; its <see cref="StructureOutcome.Profiles"/> is null without a
        /// query.</returns>
        internal static StructureOutcome Recompare(StructureResult r, LoadedFrame master, string path, TextWriter report, StructureQuery query)
        {
            using RunClock clock = RunClock.Start(Log, "structure recompare", path + Describe(query));
            StructureOutcome o = query == null ? new StructureOutcome(r, null, null, null) : Compare(r, master, query);
            clock.Lap("compare");
            StructureReport.Write(o, path, report);
            clock.Lap("report");
            clock.Done("verdict=" + o.Verdict);
            return o;
        }

        /// <summary>The diagnostic log: the run's lines and figures (R-110).</summary>
        static readonly Logger Log = LogManager.GetLogger(nameof(StructureRun));

        /// <summary>A query's options for the run's first line; empty without one.</summary>
        /// <param name="query">The query, or null.</param>
        /// <returns>The text, with a leading space when not empty.</returns>
        static string Describe(StructureQuery query)
            => query == null ? ""
             : " raw=" + LogText.Str(query.RawPath) + " lens=" + LogText.Str(query.LensText) + " lensKey=" + LogText.Str(query.LensKey)
             + " source=" + (query.Source.HasValue ? query.Source.Value.ToString() : "both")
             + " focal=" + LogText.Num(query.Focal) + " fstop=" + LogText.Num(query.FStop) + " crop=" + LogText.Num(query.Crop);

        /// <summary>
        /// The first half of Execute, without the report and without the map: loads, analyses
        /// and, with a query, compares. SplitRun calls it once per master, so that a part's
        /// figures are exactly those the structure run would give on its file.
        /// </summary>
        /// <param name="path">The master flat: a FITS or a camera raw.</param>
        /// <param name="query">Null = the structure only; otherwise the comparison
        /// too.</param>
        /// <param name="clock">The run's clock, whose stages are timed; null (SplitRun) times
        /// nothing.</param>
        /// <returns>The outcome; its <see cref="StructureOutcome.Profiles"/> is null without
        /// a query.</returns>
        internal static StructureOutcome Analyze(string path, StructureQuery query = null, RunClock clock = null)
        {
            LoadedFrame f = ImageLoader.Load(path);
            clock?.Lap("load");
            var r = StructureAnalyzer.Analyze(f.Image);
            clock?.Lap("analyze");
            if (Log.IsDebugEnabled) Log.Debug("structure of " + path + ": " + LogText.Structure(r));
            if (query == null) return new StructureOutcome(r, null, null, null);
            StructureOutcome o = Compare(r, f, query);
            clock?.Lap("compare");
            return o;
        }

        /// <summary>
        /// The comparison on a structure result already computed: identifies, picks the
        /// lenses, compares. Separate from Execute so the window can compare without reloading
        /// the master. A raw that cannot be read does not throw: its message goes into
        /// <see cref="StructureOutcome.RawError"/> and the identification goes on without it.
        /// </summary>
        /// <param name="r">The five tests on the master, already computed.</param>
        /// <param name="reference">The master's frame, for its header (focal length, crop);
        /// its pixels are not read again.</param>
        /// <param name="query">What to compare with; not null.</param>
        /// <returns>The outcome with the comparison, which says in its gap why when it could
        /// not compare.</returns>
        internal static StructureOutcome Compare(StructureResult r, LoadedFrame reference, StructureQuery query)
        {
            LoadedFrame raw = null;
            string rawError = null;
            if (query.RawPath != null)
            {
                try { raw = query.LoadInfo(query.RawPath); }
                catch (Exception e)
                {
                    Log.Warn(e, "the raw beside the master, " + query.RawPath + ", could not be read; the identification goes on without it");
                    rawError = T($"{Path.GetFileName(query.RawPath)} could not be read: {e.Message}",
                                 $"{Path.GetFileName(query.RawPath)} nu s-a putut citi: {e.Message}");
                }
            }
            ShotIdentity shot = LensIdentification.Identify(reference, raw, query.Sources, query.Focal, query.FStop, query.Crop,
                                                            LensIdentification.NameOf(query.RawPath));
            IReadOnlyList<LensChoice> lenses = string.IsNullOrWhiteSpace(query.LensText)
                ? (query.Source.HasValue ? shot.Candidates.Where(c => c.Source == query.Source.Value).ToList() : shot.Candidates)
                : SyntheticRun.MatchLenses(query.LensText, query.Source, query.Sources, shot);
            // The lens chosen among the candidates (R-121): that one alone is compared, the
            // candidates stay whole on the identity for the picker, and the choice is recorded
            // for the report. A key that matches none - the raw changed, the profile gone - is
            // ignored, so nothing stale is ever compared.
            if (string.IsNullOrWhiteSpace(query.LensText) && !string.IsNullOrEmpty(query.LensKey))
            {
                LensChoice chosen = lenses.FirstOrDefault(c => c.Key == query.LensKey);
                if (chosen != null) { shot.ChosenLens = chosen; lenses = new[] { chosen }; }
                else Log.Debug("the chosen lens " + query.LensKey + " is not among the candidates; every candidate is compared");
            }
            // The lenses a --lens text names were named, whatever the raw's range found (R-128).
            var cmp = VignettingComparer.Analyze(r, shot, query.Sources?.Catalog, lenses,
                                                 lensesByRange: string.IsNullOrWhiteSpace(query.LensText) && shot.CandidatesByRangeOnly);
            if (Log.IsDebugEnabled)
                Log.Debug("lenses compared " + LogText.List(lenses?.Select(l => l.Key)) + ": " + LogText.Comparison(cmp));
            return new StructureOutcome(r, cmp, shot, rawError);
        }
    }
}
