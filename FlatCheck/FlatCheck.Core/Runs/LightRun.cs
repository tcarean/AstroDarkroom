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

// LightRun.cs - mode 3 on a single frame, end to end.

using System.IO;
using NLog;

namespace FlatCheck
{
    /// <summary>
    /// The result of a --light run. Unlike the other modes, this one can REFUSE to answer:
    /// beyond Th.LightFovMax degrees of diagonal the sky and the vignetting have the same
    /// shape, so any figure that came out would be the sky. The refusal is not a bad verdict,
    /// it is the absence of a verdict - which is why Result stays null and no figure is
    /// produced.
    /// </summary>
    internal sealed class LightRunOutcome
    {
        /// <summary>The field covered and where it was learned from - reported on a refusal
        /// too.</summary>
        internal readonly SuitabilityResult Suitability;

        /// <summary>The figures, or null when the test does not apply.</summary>
        internal readonly LightResult Result;

        /// <summary>The radial map as PNG, or null when the test does not apply.</summary>
        internal readonly byte[] Png;

        internal bool Refused => Result == null;

        /// <summary>Bundles the suitability check with the figures and the map.</summary>
        /// <param name="s">The field-of-view check; never null.</param>
        /// <param name="r">The figures, or null on a refusal.</param>
        /// <param name="png">The radial map as PNG, or null on a refusal.</param>
        internal LightRunOutcome(SuitabilityResult s, LightResult r, byte[] png)
        {
            Suitability = s;
            Result = r;
            Png = png;
        }
    }

    /// <summary>
    /// Mode 3, <c>--light</c>, on one calibrated light: does the flat correct a real light;
    /// refuses a field too wide before computing anything.
    /// </summary>
    internal static class LightRun
    {
        /// <summary>
        /// Loads the light, checks its field of view (<see cref="LightSuitability.Check"/>)
        /// and, when the field allows it, fits it and writes the report; on a refusal writes
        /// only the refusal. Throws what <see cref="ImageLoader.Load"/> throws.
        /// </summary>
        /// <param name="path">A calibrated, unregistered light.</param>
        /// <param name="fov">The diagonal field in degrees given with <c>--fov</c>, or null
        /// to derive it from the header.</param>
        /// <param name="report">Where the report's text is written.</param>
        /// <returns>The check, and the figures with the map unless the run refused.</returns>
        internal static LightRunOutcome Execute(string path, double? fov, TextWriter report)
        {
            using RunClock clock = RunClock.Start(Log, "light", path + " fov=" + LogText.Num(fov));
            LoadedFrame f = ImageLoader.Load(path);
            clock.Lap("load");

            // The guard BEFORE any computation. On a field too wide the figure would mean
            // nothing, so it is not produced at all, rather than produced and explained
            // underneath - a printed number is believed, however many brackets you add.
            var s = LightSuitability.Check(f.Header, fov);
            if (Log.IsDebugEnabled) Log.Debug("field of " + path + ": " + LogText.Suitability(s));
            if (!s.CanRun)
            {
                LightReport.WriteTooWide(s, path, report);
                clock.Done("refused: the field is too wide");
                return new LightRunOutcome(s, null, null);
            }

            var r = LightAnalyzer.Analyze(f.Image);
            clock.Lap("analyze");
            if (Log.IsDebugEnabled) Log.Debug("fit of " + path + ": " + LogText.Light(r));
            LightReport.Write(r, s, path, report);
            clock.Lap("report");
            byte[] png = PngWriter.EncodeResidualMap(r.RadialMap);
            clock.Lap("map");
            clock.Done("verdict=" + r.Verdict);
            return new LightRunOutcome(s, r, png);
        }

        /// <summary>The diagnostic log: the run's lines and figures (R-110).</summary>
        static readonly Logger Log = LogManager.GetLogger(nameof(LightRun));
    }
}
