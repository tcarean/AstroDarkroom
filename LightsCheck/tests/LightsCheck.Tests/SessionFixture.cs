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

using System.Globalization;

namespace LightsCheck.Tests;

/// <summary>
/// A synthetic Siril session on disk: small FITS lights with the cards Siril writes on a
/// converted raw (DATE-OBS, EXPTIME, ISOSPEED, INSTRUME, FOCALLEN, XPIXSZ), and optionally
/// biases. Generated, not stored: the generator is simpler than the files would be large.
/// </summary>
internal sealed class SessionFixture : IDisposable
{
    private readonly TempWorkspace _ws = new();

    internal string Root => _ws.Dir;

    internal string LightsDir => Path.Combine(Root, "lights");

    internal static readonly DateTime FirstStart = new(2026, 10, 6, 20, 35, 3, DateTimeKind.Utc);

    /// <summary>
    /// Writes <paramref name="lights"/> frames of <paramref name="exposure"/> seconds into
    /// lights/, one every <paramref name="cadence"/> seconds from <see cref="FirstStart"/>,
    /// and <paramref name="biases"/> frames into biases/ when asked.
    /// </summary>
    internal SessionFixture WithLights(int lights, double exposure = 120.2, double cadence = 141, int biases = 0, bool inSubfolder = true, bool withStars = false)
    {
        string dir = inSubfolder ? LightsDir : Root;
        Directory.CreateDirectory(dir);
        for (int i = 0; i < lights; i++)
        {
            DateTime t = FirstStart.AddSeconds(i * cadence);
            File.WriteAllBytes(Path.Combine(dir, $"DSC_{i + 1:0000}.fit"), withStars ? StarLight(t, exposure, i) : Light(t, exposure));
        }

        if (biases > 0)
        {
            string bdir = Path.Combine(Root, "biases");
            Directory.CreateDirectory(bdir);
            for (int i = 0; i < biases; i++)
            {
                File.WriteAllBytes(Path.Combine(bdir, $"bias_{i + 1:0000}.fit"), Light(FirstStart, 0.0002));
            }
        }

        return this;
    }

    /// <summary>A frame with the metadata cards and a flat 300 ADU field (no stars; the inventory reads no pixel).</summary>
    internal static byte[] Light(DateTime start, double exposure) =>
        FitsTestData.BuildFits(32, 24, (x, y) => 300, bitpix: 16, extraCards: Cards(start, exposure));

    /// <summary>
    /// A 640 x 480 RGGB frame with 40 round stars of FWHM 3.0 on a 16-bit scale, the same
    /// field in every frame (the seed varies the noise only), with the metadata cards and
    /// BAYERPAT so the analyzer knows the colour order.
    /// </summary>
    internal static byte[] StarLight(DateTime start, double exposure, int seed) => StarLight(start, exposure, seed, 0, 0, 1.0);

    /// <summary>The same field shifted by (dx, dy) pixels and, when elongation is above 1, every star trailed along the vertical axis.</summary>
    internal static byte[] StarLight(DateTime start, double exposure, int seed, double dx, double dy, double elongation)
    {
        SyntheticFrame f = new SyntheticFrame
        {
            Width = 640,
            Height = 480,
            White = 65535,
            Seed = 100 + seed,
            BackgroundR = 400,
            BackgroundG = 2000,
            BackgroundB = 2300,
            NoiseSigma = 40,
        }.WithStarGrid(40, 3.0, 3000, 30000);
        for (int i = 0; i < f.Stars.Count; i++)
        {
            SyntheticStar s = f.Stars[i];
            f.Stars[i] = elongation > 1
                ? SyntheticStar.Elongated(s.X + dx, s.Y + dy, s.Peak, 3.0, elongation, 90)
                : SyntheticStar.Round(s.X + dx, s.Y + dy, s.Peak, 3.0);
        }

        Func<int, int, double> px = f.Pixel();
        string[] cards = [.. Cards(start, exposure), .. FitsTestData.BayerCards("RGGB")];
        return FitsTestData.BuildFits(f.Width, f.Height, px, bitpix: 16, extraCards: cards);
    }

    /// <summary>
    /// A series with a known dither pattern: one frame per entry, shifted by (Dx, Dy) px,
    /// trailed when Elongation is above 1, started PauseSeconds after the previous frame's end.
    /// </summary>
    internal SessionFixture WithStarSeries(IReadOnlyList<(double Dx, double Dy, double Elongation, double PauseSeconds)> frames, double exposure = 120, int biases = 0)
    {
        Directory.CreateDirectory(LightsDir);
        DateTime t = FirstStart;
        for (int i = 0; i < frames.Count; i++)
        {
            if (i > 0)
            {
                t = t.AddSeconds(exposure + frames[i].PauseSeconds);
            }

            File.WriteAllBytes(Path.Combine(LightsDir, $"DSC_{i + 1:0000}.fit"), StarLight(t, exposure, i, frames[i].Dx, frames[i].Dy, frames[i].Elongation));
        }

        if (biases > 0)
        {
            string bdir = Path.Combine(Root, "biases");
            Directory.CreateDirectory(bdir);
            for (int i = 0; i < biases; i++)
            {
                SyntheticFrame b = new() { Width = 640, Height = 480, White = 65535, Seed = 500 + i, BackgroundR = 100, BackgroundG = 100, BackgroundB = 100, NoiseSigma = 12 };
                File.WriteAllBytes(Path.Combine(bdir, $"bias_{i + 1:0000}.fit"), FitsTestData.BuildFits(b.Width, b.Height, b.Pixel(), bitpix: 16, extraCards: [.. Cards(FirstStart, 0.0002), .. FitsTestData.BayerCards("RGGB")]));
            }
        }

        return this;
    }

    /// <summary>The eight-frame pattern the series tests and the golden use: pairs at the same spot, a move every two frames, frames 3 and 6 trailed.</summary>
    internal static readonly (double Dx, double Dy, double Elongation, double PauseSeconds)[] Pattern =
    [
        (0, 0, 1.0, 0), (0.5, 0.2, 1.0, 1), (12, 0, 2.0, 30), (12.5, 0.3, 1.0, 1),
        (12, -15, 1.0, 30), (12.5, -15, 2.0, 1), (40, -15, 1.0, 30), (40.5, -15.3, 1.0, 1),
    ];

    internal static string[] Cards(DateTime start, double exposure) =>
    [
        FitsTestData.Card("DATE-OBS", "'" + start.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture) + "'"),
        FitsTestData.Card("EXPTIME", exposure.ToString("0.####", CultureInfo.InvariantCulture)),
        FitsTestData.Card("ISOSPEED", "1600"),
        FitsTestData.Card("INSTRUME", "'NIKON D3300'"),
        FitsTestData.Card("FOCALLEN", "135"),
        FitsTestData.Card("XPIXSZ", "3.92"),
    ];

    public void Dispose() => _ws.Dispose();
}
