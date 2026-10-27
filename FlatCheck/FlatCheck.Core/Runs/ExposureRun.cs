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

// ExposureRun.cs - mode 4 end to end: a raw flat -> level + verdict + text.

using System.IO;

namespace FlatCheck
{
    /// <summary>
    /// Mode 4, <c>--expo</c>: is a raw flat exposed right; loads one frame, measures its
    /// level per CFA position and writes the report.
    /// </summary>
    internal static class ExposureRun
    {
        /// <summary>
        /// Img.Prepare is NOT called here, and that is all that sets this mode apart from the
        /// other three: it needs the raw CFA mosaic, at the original ADU scale. A 2x2
        /// block-mean would mix the four Bayer positions and erase exactly what is measured.
        ///
        /// No branching on the format: ImageLoader has already decided. A raw carries an
        /// exact scale, a FITS carries null and then the analyzer guesses it from the maximum -
        /// but the computation after that is the same, bit for bit.
        ///
        /// It produces no map: the mode measures a level, it has nothing to draw.
        ///
        /// <paramref name="withPreview"/>: also decodes the camera's preview, if the file has
        /// one. False by default, because only the window shows it: the console has nothing to
        /// do with it, and decoding costs tens to hundreds of milliseconds. Its bytes are in
        /// LoadedFrame anyway; here only the decoding is paid for.
        /// </summary>
        /// <param name="path">A raw or a FITS flat.</param>
        /// <param name="sat">The white level in ADU given with <c>--sat</c>, or null to take
        /// it from the file or guess it.</param>
        /// <param name="black">The black level in ADU given with <c>--black</c>, or null to
        /// take it from the file (0 when the file carries none).</param>
        /// <param name="sky">True for a sky flat (<c>--sky</c>), whose acceptable bands differ
        /// from a panel's.</param>
        /// <param name="report">Where the report's text is written.</param>
        /// <param name="withPreview">True to decode the camera's preview as well.</param>
        /// <returns>The exposure result, with the decoded preview when asked for; no
        /// map.</returns>
        internal static RunOutcome<ExposureResult> Execute(
            string path, double? sat, double? black, bool sky, TextWriter report,
            bool withPreview = false)
        {
            RunClock clock = RunClock.Start(Log, "expo", path + " sat=" + LogText.Num(sat) + " black=" + LogText.Num(black)
                                                   + " sky=" + LogText.Yes(sky) + " preview=" + LogText.Yes(withPreview));
            LoadedFrame f = ImageLoader.Load(path);
            clock.Lap("load");
            PreviewImage preview = withPreview ? PreviewReader.Decode(f.Preview) : null;
            if (withPreview) clock.Lap("preview");
            var r = ExposureAnalyzer.Analyze(f.Image, sat, black, sky, f.Scale, f.Cfa, preview);
            clock.Lap("analyze");
            if (Log.IsDebugEnabled) Log.Debug("exposure of " + path + ": " + LogText.Exposure(r));
            ExposureReport.Write(r, path, report);
            clock.Lap("report");
            clock.Done("verdict=" + r.Verdict);
            return new RunOutcome<ExposureResult>(r, null);
        }

        /// <summary>The diagnostic log: the run's lines and figures (R-110).</summary>
        static readonly NLog.Logger Log = NLog.LogManager.GetLogger(nameof(ExposureRun));
    }
}
