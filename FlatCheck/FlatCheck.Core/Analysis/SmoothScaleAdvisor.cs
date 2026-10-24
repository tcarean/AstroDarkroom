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

// SmoothScaleAdvisor.cs - proposes the smoothing scale. It PROPOSES, it does not decide: what
// belongs to the lens and what to the source does not show in the pixels, so the person presses
// the button.
//
// The scale must sit between the size of what must be removed (the diffuser's cells) and the
// size of the smallest real structure to keep. Both are read from the master:
//
//   1. The texture of the input (Metrics.FineTexture on the analysis raster) says whether there
//      is anything to smooth. Below Th.SmoothTextureMin the answer is "nothing to smooth", with
//      no run at all: smoothing a clean master removes only the dust, and that is a decision
//      about using the flat, not about the flat.
//   2. Otherwise, one run on each step of the ladder (the window's three chips:
//      half, default, double), ~4-5 s each on 24 MP, and two figures per step:
//      the texture left in the file (below Th.SmoothTextureGone = it went) and how much the
//      structure battery DROPPED - but only the azimuthal asymmetry and the tilt, the only two
//      tests the texture does not touch (the corners, the residual and the worst tile drop
//      precisely when the texture goes, so they cannot be the guard). A drop above
//      Th.SmoothScaleLoss means real structure went: the scale is too large.
//   3. The smallest step at which the texture went and the guard stays quiet. If there is none,
//      NEDETERMINAT - the texture and the real structure have the same size - the way the
//      --light series answers when the sky and the flat do not come apart.
//
// Measured (2026-09-16, docs/design/smooth-validation.md): on the master with the diffuser on the
// monitor it proposes 300 px (at 150, 0.09 % texture remains, at 300, 0.02 % and the guard is at
// 0.00 / -0.01); on the other four "nothing to smooth". At 600 px the guard catches the real ring
// of the Nikkor (asymmetry -0.79) and the monitor's spots on the Tokina (-0.80), which the figure
// removed at the large scale did not catch both of - that is why the guard is the battery.
//
// What it does NOT know, and says in the report: that an LCD's hot spot belongs to the source;
// that a real structure only in the corners, outside the inscribed circle and which is not a
// plane, escapes the guards.

using System;
using System.Collections.Generic;
using System.Linq;

namespace FlatCheck
{
    /// <summary>What <see cref="SmoothScaleAdvisor.Advise"/> concluded.</summary>
    internal enum SmoothAdviceKind
    {
        /// <summary>
        /// The input has no texture above the threshold: there is nothing to smooth.
        /// </summary>
        NoTexture,
        /// <summary>A step removed the texture without taking structure: ScalePx.</summary>
        Proposed,
        /// <summary>
        /// There is texture, but any step that removes it takes real structure too.
        /// </summary>
        Undetermined,
    }

    /// <summary>A step tried: what remained, what went, and why it passed or not.</summary>
    internal sealed class SmoothScaleTrial
    {
        internal double ScalePx;
        internal double TextureAfterPct;
        /// <summary>After minus before, in points; negative = it dropped.</summary>
        internal double AzimuthalDelta, TiltDelta, CornerDelta;
        internal double RemovedPtpPct, RemovedMaxTilePct;
        internal bool TextureGone;
        internal bool StructureLost;
        internal bool Acceptable => TextureGone && !StructureLost;
    }

    /// <summary>
    /// The proposal: its kind, the input's texture, the scale proposed and every step tried.
    /// </summary>
    internal sealed class SmoothScaleAdvice
    {
        internal SmoothAdviceKind Kind;
        internal double InputTexturePct;
        /// <summary>The proposed scale; NaN when Kind is not Proposed.</summary>
        internal double ScalePx = double.NaN;
        /// <summary>Empty on NoTexture; otherwise one entry per step, in ladder order.</summary>
        internal IReadOnlyList<SmoothScaleTrial> Trials = Array.Empty<SmoothScaleTrial>();
    }

    /// <summary>
    /// <c>--smooth --scale auto</c>: tries the steps of a ladder and proposes the smallest one
    /// that removes the texture without taking real structure.
    /// </summary>
    internal static class SmoothScaleAdvisor
    {
        /// <summary>
        /// The steps: the window's three chips, so that there is no second list of numbers.
        /// </summary>
        internal static IReadOnlyList<double> Ladder => new[]
        {
            SmoothAnalyzer.DefaultScalePx / 2, SmoothAnalyzer.DefaultScalePx, SmoothAnalyzer.DefaultScalePx * 2,
        };

        /// <summary>
        /// Measures the input's texture and, when it reaches <see cref="Th.SmoothTextureMin"/>,
        /// smooths the master once per step and decides on the figures.
        /// </summary>
        /// <param name="input">The master to smooth, as loaded.</param>
        /// <param name="ladder">The steps to try, ascending, sensor pixels; <see cref="Ladder"/>
        /// by default. The tests give steps to suit their fixtures.</param>
        /// <returns>The proposal; never null.</returns>
        internal static SmoothScaleAdvice Advise(Img input, IReadOnlyList<double> ladder = null)
        {
            double texture = Metrics.FineTexture(Img.Prepare(input));
            if (texture < Th.SmoothTextureMin)
                return new SmoothScaleAdvice { Kind = SmoothAdviceKind.NoTexture, InputTexturePct = texture };

            var trials = new List<SmoothScaleTrial>();
            foreach (double scale in ladder ?? Ladder)
            {
                // The result is thrown away once the figures are read: the smoothed raster is
                // 190 MB on 24 MP, and three at once would have no business in memory.
                SmoothResult r = SmoothAnalyzer.Analyze(input, scale);
                trials.Add(Trial(r));
            }
            return Decide(texture, trials);
        }

        /// <summary>
        /// The figures of a step, from a smoothing result: texture gone below
        /// <see cref="Th.SmoothTextureGone"/>, structure lost when the azimuthal asymmetry or the
        /// tilt drops by <see cref="Th.SmoothScaleLoss"/> or more. Pure; the tests use it directly.
        /// </summary>
        /// <param name="r">The result of one smoothing run.</param>
        /// <returns>The step's figures and flags.</returns>
        internal static SmoothScaleTrial Trial(SmoothResult r)
        {
            double az = r.After.AzimuthalMaxPct - r.Before.AzimuthalMaxPct;
            double tilt = r.After.TiltPct - r.Before.TiltPct;
            return new SmoothScaleTrial
            {
                ScalePx = r.ScalePx,
                TextureAfterPct = r.OutputTexturePct,
                AzimuthalDelta = az,
                TiltDelta = tilt,
                CornerDelta = r.After.CornerSpreadPct - r.Before.CornerSpreadPct,
                RemovedPtpPct = r.RemovedPtpPct,
                RemovedMaxTilePct = r.RemovedMaxTilePct,
                TextureGone = r.OutputTexturePct < Th.SmoothTextureGone,
                // !(x > -threshold), so that NaN falls on the cautious side - the Stats.Verdict
                // convention
                StructureLost = !(az > -Th.SmoothScaleLoss) || !(tilt > -Th.SmoothScaleLoss),
            };
        }

        /// <summary>The smallest acceptable step, or NEDETERMINAT. Pure.</summary>
        /// <param name="inputTexture">The input's fine texture, percent.</param>
        /// <param name="trials">The steps tried, in ladder order.</param>
        /// <returns>Proposed with the first acceptable step's scale, else Undetermined.</returns>
        internal static SmoothScaleAdvice Decide(double inputTexture, IReadOnlyList<SmoothScaleTrial> trials)
        {
            SmoothScaleTrial pick = trials.FirstOrDefault(t => t.Acceptable);
            return new SmoothScaleAdvice
            {
                Kind = pick != null ? SmoothAdviceKind.Proposed : SmoothAdviceKind.Undetermined,
                InputTexturePct = inputTexture,
                ScalePx = pick?.ScalePx ?? double.NaN,
                Trials = trials,
            };
        }
    }
}
