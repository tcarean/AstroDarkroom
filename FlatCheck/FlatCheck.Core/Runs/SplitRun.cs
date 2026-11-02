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

// SplitRun.cs - the parts' masters beside the whole session's master: N + 1 structure runs,
// a table, a report, a mosaic of residual maps.
//
// It knows nothing about temp folders or about Siril: it receives file paths. The window does
// the splitting and the runs; the console could give the same paths with a flag, if it is
// ever needed (ADR 0054). Each column's figures are exactly those of the structure run on its
// file - StructureRun.Analyze, the same function.
//
// THE REFERENCE PROFILE is the row that gives the WHOLE's verdict, the same on every column: the
// whole's Adobe row when the lens was named (R-128), its nearest row otherwise. The deciding row
// is chosen on each master separately, and on the first real session part 1 and the whole chose
// different profiles under the nearest rule (Adobe +0.07 against lensfun -0.08); a table with a
// different reference on each column would compare nothing.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using static FlatCheck.Ui;

namespace FlatCheck
{
    /// <summary>A column of the table: a part or the whole, with its structure run.</summary>
    internal sealed class SplitColumn
    {
        internal readonly string Path;
        /// <summary>"partea 1 din 4" or "intreg" - the text in the column's header and in the
        /// report.</summary>
        internal readonly string Label;
        internal readonly StructureOutcome Outcome;
        /// <summary>This master's depth against the reference profile; null without a
        /// reference.</summary>
        internal readonly ProfileDepth Reference;
        /// <summary>
        /// The column's verdict: the worst of the five tests and the depth against THE
        /// REFERENCE PROFILE - not the run's verdict (Outcome.Verdict), which takes the depth
        /// against the column's own nearest profile; in the table the row above is against the
        /// reference, and the two rows must speak about the same profile.
        /// Without a reference, the five tests' verdict.
        /// </summary>
        internal readonly string Verdict;

        /// <summary>Holds a column and combines its verdict by
        /// <see cref="Stats.Severity"/>.</summary>
        /// <param name="path">The column's master.</param>
        /// <param name="label">The column's heading (<c>SplitReport.PartLabel</c> or
        /// <c>WholeLabel</c>).</param>
        /// <param name="outcome">The structure run on <paramref name="path"/>.</param>
        /// <param name="reference">The depth against the reference profile, or null.</param>
        internal SplitColumn(string path, string label, StructureOutcome outcome, ProfileDepth reference)
        {
            Path = path;
            Label = label;
            Outcome = outcome;
            Reference = reference;
            Verdict = reference != null && Stats.Severity(reference.Verdict) > Stats.Severity(outcome.Structure.Verdict)
                ? reference.Verdict : outcome.Structure.Verdict;
        }
    }

    /// <summary>
    /// What a split measured: one column per part, the whole, the reference profile they are
    /// all read against, and the mosaic's shared scale.
    /// </summary>
    internal sealed class SplitResult
    {
        internal readonly IReadOnlyList<SplitColumn> Parts;
        internal readonly SplitColumn Whole;
        /// <summary>The profile of the row that gives the whole's verdict (R-128), or null when
        /// the whole could not be compared.</summary>
        internal readonly LensProfile ReferenceProfile;
        /// <summary>The reference profile's depth, measured with the master's operator; null
        /// without a reference.</summary>
        internal readonly ProfileDepth ReferenceDepth;
        /// <summary>Every profile the whole was compared with, in the Structure tab's order;
        /// empty without a comparison.</summary>
        internal readonly IReadOnlyList<ProfileDepth> Profiles;
        internal readonly int FramesPerPart;
        internal readonly DateTime When;
        /// <summary>The mosaic's shared scale: the automatic one over all the maps at
        /// once.</summary>
        internal readonly PngWriter.MapRange MapRange;

        /// <summary>Holds the columns and the reference; takes the profile list from the
        /// whole's comparison.</summary>
        /// <param name="parts">The parts' columns, in order.</param>
        /// <param name="whole">The whole session's column.</param>
        /// <param name="referenceProfile">The whole's nearest profile, or null.</param>
        /// <param name="referenceDepth">That profile's depth on the whole, or null.</param>
        /// <param name="framesPerPart">The frames each part had; 0 when unknown.</param>
        /// <param name="when">The run's date and time.</param>
        /// <param name="mapRange">The scale shared by every map of the mosaic.</param>
        internal SplitResult(IReadOnlyList<SplitColumn> parts, SplitColumn whole, LensProfile referenceProfile,
                             ProfileDepth referenceDepth, int framesPerPart, DateTime when, PngWriter.MapRange mapRange)
        {
            Parts = parts;
            Whole = whole;
            ReferenceProfile = referenceProfile;
            ReferenceDepth = referenceDepth;
            Profiles = whole.Outcome.Compared ? whole.Outcome.Profiles.Profiles : Array.Empty<ProfileDepth>();
            FramesPerPart = framesPerPart;
            When = when;
            MapRange = mapRange;
        }

        internal bool HasReference => ReferenceProfile != null;

        /// <summary>True when the reference is the whole's Adobe row by R-128; false when it is
        /// the whole's nearest row, or when there is none.</summary>
        internal bool ReferenceByAdobe => HasReference && Whole.Outcome.Profiles.AdobeDecides;

        /// <summary>The parts in order, then the whole - the order of the columns and of the
        /// tiles.</summary>
        internal IEnumerable<SplitColumn> Columns => Parts.Append(Whole);
    }

    /// <summary>
    /// The Split tab's analysis (ADR 0054): the structure run on each part's master and on the
    /// whole's, read against one reference profile, with a labelled mosaic of their maps.
    /// </summary>
    internal static class SplitRun
    {
        /// <summary>
        /// Analyses every master (<see cref="Analyze"/>), writes the report and encodes the
        /// mosaic, parts first and the whole last, on the shared scale. Throws what
        /// <see cref="Analyze"/> throws.
        /// </summary>
        /// <param name="wholePath">The whole session's master.</param>
        /// <param name="partPaths">The parts' masters, in the parts' order.</param>
        /// <param name="query">The profiles and the raw beside the master, as for structure;
        /// null = no comparison.</param>
        /// <param name="when">The run's date and time, written in the report and in the
        /// mosaic's title.</param>
        /// <param name="framesPerPart">How many frames each part had; 0 when unknown.</param>
        /// <param name="report">Where the report text goes.</param>
        /// <returns>The result and the mosaic as PNG.</returns>
        internal static RunOutcome<SplitResult> Execute(string wholePath, IReadOnlyList<string> partPaths, StructureQuery query,
                                                        DateTime when, int framesPerPart, TextWriter report)
        {
            using RunClock clock = RunClock.Start(Log, "split", wholePath + " parts=" + LogText.List(partPaths)
                                                   + " framesPerPart=" + LogText.Int(framesPerPart) + " compare=" + LogText.Yes(query != null));
            SplitResult r = Analyze(wholePath, partPaths, query, when, framesPerPart);
            clock.Lap("analyze");
            if (Log.IsDebugEnabled) Log.Debug("split of " + wholePath + ": " + LogText.Split(r));
            SplitReport.Write(r, wholePath, report);
            clock.Lap("report");

            var tiles = new List<(Img, string)>();
            for (int i = 0; i < r.Parts.Count; i++)
                tiles.Add((r.Parts[i].Outcome.Structure.Residual, SplitReport.PngLabel(i + 1, r.Parts.Count)));
            tiles.Add((r.Whole.Outcome.Structure.Residual, SplitReport.PngLabelWhole));

            byte[] png = PngWriter.EncodeMosaic(tiles, r.MapRange, SplitReport.PngTitle(when, r.MapRange));
            clock.Lap("map");
            clock.Done("verdicts " + LogText.List(r.Parts.Select(p => p.Verdict)) + " whole " + r.Whole.Verdict);
            return new RunOutcome<SplitResult>(r, png);
        }

        /// <summary>The diagnostic log: the run's lines and every column's figures
        /// (R-110).</summary>
        static readonly Logger Log = LogManager.GetLogger(nameof(SplitRun));

        /// <summary>
        /// Runs <see cref="StructureRun.Analyze"/> on the whole and on each part, takes the
        /// whole's nearest profile as the reference and reads every part against it. Throws an
        /// <see cref="ArgumentException"/> when there is no part, and what the structure run
        /// throws on a master it cannot read.
        /// </summary>
        /// <param name="wholePath">The whole session's master.</param>
        /// <param name="partPaths">The parts' masters, in order; at least one.</param>
        /// <param name="query">The profiles and the raw, as for structure; null = no
        /// comparison, hence no reference.</param>
        /// <param name="when">The run's date and time.</param>
        /// <param name="framesPerPart">How many frames each part had; 0 when unknown.</param>
        /// <returns>The columns, the reference and the shared map scale.</returns>
        internal static SplitResult Analyze(string wholePath, IReadOnlyList<string> partPaths, StructureQuery query,
                                            DateTime when, int framesPerPart)
        {
            if (partPaths == null || partPaths.Count == 0) throw new ArgumentException(T("no part", "nicio parte"), nameof(partPaths));

            StructureOutcome whole = StructureRun.Analyze(wholePath, query);
            var parts = new List<StructureOutcome>(partPaths.Count);
            foreach (string p in partPaths) parts.Add(StructureRun.Analyze(p, query));

            ProfileDepth referenceDepth = whole.Compared ? whole.Profiles.Deciding : null;
            LensProfile reference = referenceDepth?.Profile;

            var columns = new List<SplitColumn>(partPaths.Count);
            for (int i = 0; i < partPaths.Count; i++)
                columns.Add(new SplitColumn(partPaths[i], SplitReport.PartLabel(i + 1, partPaths.Count), parts[i], Against(parts[i], reference)));
            var wholeColumn = new SplitColumn(wholePath, SplitReport.WholeLabel, whole, referenceDepth);

            return new SplitResult(columns, wholeColumn, reference, referenceDepth, framesPerPart, when,
                                   SharedRange(columns.Append(wholeColumn)));
        }

        /// <summary>
        /// A master's depth against a given profile: the row of the same lens in its own
        /// comparison, the same profile file first; null when there is none.
        /// </summary>
        /// <param name="o">A master's structure run, with or without a comparison.</param>
        /// <param name="profile">The reference profile, or null.</param>
        /// <returns>The matching row, or null when either is missing or the lens was not
        /// compared.</returns>
        internal static ProfileDepth Against(StructureOutcome o, LensProfile profile)
        {
            if (profile == null || o.Profiles == null) return null;
            return o.Profiles.Profiles.FirstOrDefault(d => d.Profile.Lens.Key == profile.Lens.Key && d.Profile.File == profile.File)
                ?? o.Profiles.Profiles.FirstOrDefault(d => d.Profile.Lens.Key == profile.Lens.Key);
        }

        /// <summary>The automatic scale (the 1/99 percentiles plus padding, R-006) over all the
        /// residuals at once.</summary>
        /// <param name="columns">Every column, parts and whole.</param>
        /// <returns>The range <see cref="PngWriter.AutoRange"/> gives on their pixels
        /// pooled.</returns>
        static PngWriter.MapRange SharedRange(IEnumerable<SplitColumn> columns)
        {
            var maps = columns.Select(c => c.Outcome.Structure.Residual).ToList();
            var all = new Img(1, maps.Sum(m => m.D.Length));
            int at = 0;
            foreach (Img m in maps)
            {
                Array.Copy(m.D, 0, all.D, at, m.D.Length);
                at += m.D.Length;
            }
            return PngWriter.AutoRange(all);
        }
    }
}
