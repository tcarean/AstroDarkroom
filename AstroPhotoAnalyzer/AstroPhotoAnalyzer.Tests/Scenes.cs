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

namespace AstroPhotoAnalyzer.Tests;

/// <summary>
/// The whole scenes the chain runs on: sky + stars + noise, with or without defects.
/// All deterministic (fixed seed), so that the golden tests compare the same text
/// every time.
/// </summary>
internal static class Scenes
{
    /// <summary>
    /// The Dmax of the paper the scenes were built for: the baryta of 2.0 from before
    /// ADR 0028, with its black at L* 9.0. A sky at 0.08 is under that black and above
    /// today's (2.4, L* 3.6), so the tests about "background under paper black" give their
    /// paper explicitly, through <see cref="OnBuiltForPaper"/> or --dmax 2.0, instead of
    /// hanging on the default.
    /// </summary>
    internal const double BuiltForDmax = 2.0;

    /// <summary>
    /// The same parameters, on the paper the scenes were built for. A Dmax the test gives
    /// stays its own; the medium does not matter, the explicit Dmax beats it.
    /// </summary>
    internal static Parameters OnBuiltForPaper(Parameters? p = null)
    {
        p ??= new Parameters();
        return p with { DmaxOverride = p.DmaxOverride ?? BuiltForDmax };
    }

    /// <summary>Stars of FWHM 3 on a grid, 55 px apart, over any raster.</summary>
    internal static Raster WithStars(Raster r, int count = 64, double fwhm = 3.0, float amplitude = 0.3f)
    {
        int placed = 0;
        for (int j = 0; j < 8 && placed < count; j++)
            for (int i = 0; i < 8 && placed < count; i++, placed++)
            {
                double cx = 40 + i * 55, cy = 40 + j * 55;
                if (cx < r.W - 20 && cy < r.H - 20) r = SyntheticRasters.AddStar(r, cx, cy, fwhm, amplitude);
            }
        return r;
    }

    /// <summary>
    /// The "problem" image: a sky too low for the paper (0.08), a green cast of 2
    /// levels, a warm horizon corner at the bottom right, heavy denoise (0.3 LSB), and a
    /// nebula in the centre (a wide Gaussian) so that the tones are continuous - without
    /// it, the stress test would find gaps in the 16-bit data too, which does not happen
    /// on a real image. Every recommendation of the real image, in miniature.
    /// </summary>
    internal static Raster DarkSky()
    {
        const float sky = 0.08f;
        Raster r = SyntheticRasters.From(512, 512, 3, (x, y, p) =>
        {
            float v = p == 1 ? sky + 2f / 255 : sky;
            double d2 = (x - 256) * (x - 256) + (y - 256) * (y - 256);
            v += (float)(0.4 * Math.Exp(-d2 / (2 * 60.0 * 60.0)));
            // A dust band: the tones fall continuously down towards 0.02, as on a real
            // sky - that is where a shadow-lift from 8 bits bites hardest.
            double l2 = (x - 256) * (x - 256) + (y - 100) * (y - 100);
            v -= (float)(0.06 * Math.Exp(-l2 / (2 * 40.0 * 40.0)));
            if (y >= 435 && x >= 410) v += p == 0 ? 10f / 255 : p == 2 ? -8f / 255 : 0f;
            return v;
        });
        r = WithStars(r);
        r = SyntheticRasters.AddNoise(r, 0.3 / 255, 21);
        r.Source = new SourceInfo { Format = "memorie", BitDepth = 16, IsFloat = false, PlanesInFile = 3, Icc = new byte[1], IccDescription = "sRGB test" };
        return r;
    }

    /// <summary>
    /// The "clean" image: a print-safe background (0.25), neutral, good noise (1.0 LSB), an
    /// sRGB profile.
    /// </summary>
    internal static Raster CleanSky()
    {
        Raster r = WithStars(SyntheticRasters.Flat(512, 512, 0.25f));
        r = SyntheticRasters.AddNoise(r, 1.0 / 255, 22);
        r.Source = new SourceInfo { Format = "memorie", BitDepth = 16, IsFloat = false, PlanesInFile = 3, Icc = new byte[1], IccDescription = "sRGB IEC61966-2.1" };
        return r;
    }

    /// <summary>Mono, 8 bits, no profile.</summary>
    internal static Raster MonoSky()
    {
        Raster r = WithStars(SyntheticRasters.Flat(300, 300, 0.25f, planes: 1), count: 25);
        return SyntheticRasters.AddNoise(r, 1.0 / 255, 23);
    }
}
