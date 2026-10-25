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

// LiftTable.cs - the linearisation of an sRGB sample, by two paths: exact
// (ColorMath.SrgbToLinear), or lifted with a gamma and linearised through a single table on the
// 65536 levels of 16 bits (ADR 0035).
//
// The print simulation lifts the subsample ~56 times (the ladder of the exact target, ADR 0025)
// and re-measures the patches' L* each time: linearising every lifted sample with
// Math.Pow(x, 2.4) meant 250 million Pow calls on a 23 MP image, 11 seconds of the
// simulation's 17. The table holds lin(pow(k / 65535, gamma)) for every level k and
// interpolates linearly between two neighbouring levels - ON THE LINEAR VALUE, not on the
// lifted one: the intermediate lifted raster (RasterOps.Gamma, the same indexing and the same
// interpolation, on the lifted value) is no longer built at all. Measured on the reference
// images (docs/validation.md, 2026-10-09): no patch moves by more than 7.63e-6 of its L* (one
// float ulp at L* 64..128, two below 64); on an isolated sample the two interpolations differ
// most in the first 16-bit step at a small gamma, where the interpolation of the power curve was
// coarse too. The "asa cum e" row does not go through the table: it stays on the exact path.
//
// The two paths are structs that implement the same interface, and the loops over the samples
// (DustAnalyzer.PatchLstars, RasterOps.LstarPlane) are generic on it: the JIT specialises the
// loop on the struct and inlines the call, so there is no delegate per sample and no second
// copy of the loop.

using System;

namespace AstroPhotoAnalyzer
{
    /// <summary>
    /// An encoded sRGB sample (0..1) -> its linear value, by one of the two paths.
    /// </summary>
    internal interface ISrgbLinearizer
    {
        /// <summary>Linearises one encoded sample by this path.</summary>
        /// <param name="s">The encoded sRGB sample, 0..1.</param>
        /// <returns>The linear value, 0..1 (the luminance only on mono).</returns>
        double Linear(float s);
    }

    /// <summary>
    /// The exact path: <see cref="ColorMath.SrgbToLinear"/> on the sample as it is. The "asa cum e"
    /// measurements go this way.
    /// </summary>
    internal readonly struct ExactLinearizer : ISrgbLinearizer
    {
        /// <inheritdoc/>
        public double Linear(float s) => ColorMath.SrgbToLinear(s);
    }

    /// <summary>
    /// The sample lifted with <see cref="Gamma"/> and linearised, through a table on the 16-bit
    /// levels with linear interpolation between two neighbouring levels, on the linear value.
    /// </summary>
    internal readonly struct LiftTable : ISrgbLinearizer
    {
        readonly double[] _lin;

        internal double Gamma { get; }

        /// <summary>
        /// Builds the table of one lift: <see cref="RasterOps.PowerLut"/> of the gamma, every
        /// entry linearised.
        /// </summary>
        /// <param name="gamma">
        /// The lift's exponent, positive and finite, as <see cref="RasterOps.GammaFor"/> gives it.
        /// </param>
        internal LiftTable(double gamma)
        {
            // RasterOps.GammaFor always gives a positive, finite gamma; anything else would fill
            // the table with NaN or infinity and come out of the simulation as NaN targets, with
            // no error at the source.
            if (!(gamma > 0) || double.IsInfinity(gamma))
                throw new ArgumentException(Tr.Pick("gamma must be a finite positive number", "gamma trebuie sa fie un numar pozitiv finit"));
            Gamma = gamma;
            double[] pow = RasterOps.PowerLut(gamma);
            _lin = new double[pow.Length];
            for (int k = 0; k < pow.Length; k++) _lin[k] = ColorMath.SrgbToLinear(pow[k]);
        }

        /// <summary>
        /// The sample lifted and linearised: the same indexing and interpolation as
        /// <see cref="RasterOps.Gamma"/>, on the linearised table.
        /// </summary>
        /// <param name="s">
        /// The encoded sRGB sample, 0..1; clamped to that range, and NaN taken as 0.
        /// </param>
        /// <returns>The linear value of the lifted sample, 0..1.</returns>
        public double Linear(float s) => RasterOps.Interpolate(_lin, s);
    }
}
