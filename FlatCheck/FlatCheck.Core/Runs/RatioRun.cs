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

// RatioRun.cs - mode 2 end to end: two masters -> attribution + text + two maps.
//
// It loads through ImageLoader.Load, not LoadImage, because the mode needs the headers too:
// STACKCNT and DATE-OBS are the only place in the report where one can see whether the two
// stacks can really come from two different orientations. Their absence is not an error - a
// raw has no FITS header - and then the report writes "necunoscut".

using System.IO;

namespace FlatCheck
{
    /// <summary>
    /// Mode 2, <c>--ratio</c>: loads two masters shot 180 degrees apart, attributes their
    /// structure to the camera or the world (<see cref="RatioAnalyzer"/>) and writes the
    /// report.
    /// </summary>
    internal static class RatioRun
    {
        /// <summary>
        /// Loads both files, analyses them, copies STACKCNT and DATE-OBS from their headers
        /// into the result, writes the report and encodes both maps. Throws what
        /// <see cref="ImageLoader.Load"/> or <see cref="RatioAnalyzer.Analyze"/> throws.
        /// </summary>
        /// <param name="pathA">The first master.</param>
        /// <param name="pathB">The second master, after a 180-degree rotation.</param>
        /// <param name="report">Where the report's text is written.</param>
        /// <returns>The result, the camera-fixed residual map as the main PNG and the A/B map
        /// as the second.</returns>
        internal static RunOutcome<RatioResult> Execute(string pathA, string pathB, TextWriter report)
        {
            var fa = ImageLoader.Load(pathA);
            var fb = ImageLoader.Load(pathB);

            var r = RatioAnalyzer.Analyze(fa.Image, fb.Image);
            r.StackCountA = FitsReader.HInt(fa.Header, "STACKCNT", 0);
            r.StackCountB = FitsReader.HInt(fb.Header, "STACKCNT", 0);
            r.DateObsA = FitsReader.HStr(fa.Header, "DATE-OBS", null);
            r.DateObsB = FitsReader.HStr(fb.Header, "DATE-OBS", null);

            RatioReport.Write(r, pathA, pathB, report);

            // The main map is the residual of the camera-fixed part, because it is the one
            // that matches the verdict. The second is A/B, for the shape of the external
            // gradient.
            return new RunOutcome<RatioResult>(
                r,
                PngWriter.EncodeResidualMap(r.Sensor.Residual),
                PngWriter.EncodeResidualMap(r.World));
        }
    }
}
