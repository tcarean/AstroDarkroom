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

// LightSeriesRun.cs - mode 3 on the whole series of a folder, end to end.
//
// Of the five runs of 2.0.0 (Runs/ holds ten now), this was the only one that really had
// something to duplicate: it is a loop, with state, with progress and with a memory
// optimisation that does not show from the signature. Written a second time in the WPF
// application, it would surely have diverged - if only by the second version forgetting to
// release the first frame.
//
// Two outputs, not one, and that is why it works for both callers:
//   - the rows are written into the TextWriter as the frames are ready (the console is not
//     silent for two minutes), and
//   - the same frame also leaves through onFrame, so the window fills its table progressively.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace FlatCheck
{
    /// <summary>
    /// The result of a --light run on a series: the field-of-view check, the combined
    /// figures (null on a refusal), the map and the files that went in.
    /// </summary>
    internal sealed class LightSeriesRunOutcome
    {
        internal readonly SuitabilityResult Suitability;

        /// <summary>The synthesis over the series, or null when the test does not
        /// apply.</summary>
        internal readonly LightSeriesResult Result;

        internal readonly byte[] Png;

        /// <summary>The files that went into the run, in the order they were
        /// processed.</summary>
        internal readonly IReadOnlyList<string> Files;

        internal bool Refused => Result == null;

        /// <summary>Bundles the check, the synthesis, the map and the file list.</summary>
        /// <param name="s">The field-of-view check on the first frame; never null.</param>
        /// <param name="r">The synthesis, or null on a refusal.</param>
        /// <param name="png">The combined radial map as PNG, or null on a refusal.</param>
        /// <param name="files">The files found, in processing order.</param>
        internal LightSeriesRunOutcome(
            SuitabilityResult s, LightSeriesResult r, byte[] png, IReadOnlyList<string> files)
        {
            Suitability = s;
            Result = r;
            Png = png;
            Files = files;
        }
    }

    /// <summary>
    /// Mode 3, <c>--light</c> on a folder: analyses every calibrated light of a series one at
    /// a time and combines them (<see cref="LightSeriesAnalyzer.Combine"/>).
    /// </summary>
    internal static class LightSeriesRun
    {
        /// <summary>
        /// Finds the series, checks the first frame's field of view, then loads, analyses and
        /// reports each frame in turn and writes the synthesis. Throws when no file matches
        /// the pattern, and what <see cref="ImageLoader.Load"/> throws on a frame it cannot
        /// read; an <see cref="OperationCanceledException"/> when <paramref name="ct"/> is
        /// cancelled between frames.
        /// </summary>
        /// <param name="dir">The folder that holds the lights.</param>
        /// <param name="pattern">The file pattern the lights match in <paramref name="dir"/>
        /// (--pattern).</param>
        /// <param name="fov">The diagonal field of view in degrees given with --fov, or null to
        /// compute it from the first frame's header.</param>
        /// <param name="report">Where the report text goes.</param>
        /// <param name="onFrame">
        /// Called after each frame, with the index (from 0), the total, the file name and the
        /// result. For the console it is null - the row written into report is enough for it.
        /// For the window it is how the table fills and how the progress bar moves.
        /// </param>
        /// <param name="ct">
        /// Checked BETWEEN frames, not inside one: the analyzers are pure and synchronous, and
        /// a frame takes under a second. Sprinkling them with checks would have soiled the code
        /// that matters for half a second of responsiveness.
        /// </param>
        /// <returns>The check, the synthesis and the map, or the check alone on a
        /// refusal; the file list either way.</returns>
        internal static LightSeriesRunOutcome Execute(
            string dir,
            string pattern,
            double? fov,
            TextWriter report,
            Action<int, int, string, LightResult> onFrame = null,
            CancellationToken ct = default)
        {
            var files = SeriesFinder.Find(dir, pattern);
            if (files.Count == 0)
                throw new Exception(
                    $"Niciun fisier care sa se potriveasca cu '{pattern}' in {dir}. "
                    + "Da alt tipar cu --pattern.");

            // Applicability is decided on the first frame: they all come from the same
            // session, so they have the same focal length and the same scale.
            LoadedFrame first = ImageLoader.Load(files[0]);
            var s = LightSuitability.Check(first.Header, fov);
            if (!s.CanRun)
            {
                LightReport.WriteTooWide(s, dir, report);
                return new LightSeriesRunOutcome(s, null, null, files);
            }

            LightSeriesReport.WriteHeader(dir, pattern, files.Count, s, report);

            // The frames are loaded in turn and NOT kept: a 24 MP frame takes hundreds of MB,
            // so 32 held at once would be impossible to run. The first is already loaded for
            // the guard above, and is released as soon as it has been used - hence
            // "first = null", which would otherwise look gratuitous.
            var frames = new List<(string, LightResult)>();
            for (int i = 0; i < files.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                Img img = first?.Image ?? ImageLoader.LoadImage(files[i]);
                first = null;

                var r = LightAnalyzer.Analyze(img);
                string name = Path.GetFileName(files[i]);
                LightSeriesReport.WriteRow(name, r, report);
                frames.Add((name, r));
                onFrame?.Invoke(i, files.Count, name, r);
            }

            var series = LightSeriesAnalyzer.Combine(frames);
            LightSeriesReport.WriteSummary(series, report);
            return new LightSeriesRunOutcome(
                s, series, PngWriter.EncodeResidualMap(series.RadialMap), files);
        }
    }
}
