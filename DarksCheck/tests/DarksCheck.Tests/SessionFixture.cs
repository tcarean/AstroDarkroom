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

namespace DarksCheck.Tests;

/// <summary>
/// A synthetic Siril session on disk: small FITS frames with the cards Siril writes on a
/// converted raw (DATE-OBS, EXPTIME, ISOSPEED, INSTRUME), in darks/, biases/ and lights/.
/// Generated, not stored: the generator is simpler than the files would be large.
/// </summary>
internal sealed class SessionFixture : IDisposable
{
    private readonly TempWorkspace _ws = new();

    internal string Root => _ws.Dir;

    internal string DarksDir => Path.Combine(Root, "darks");

    internal string BiasesDir => Path.Combine(Root, "biases");

    internal string LightsDir => Path.Combine(Root, "lights");

    /// <summary>The start of the first dark; the lights of a fixture end before it, as on a real night.</summary>
    internal static readonly DateTime FirstStart = new(2026, 8, 13, 0, 48, 53, DateTimeKind.Utc);

    /// <summary>
    /// Writes <paramref name="darks"/> frames of <paramref name="exposure"/> seconds at
    /// <paramref name="iso"/> into darks/ (or the folder itself), one every
    /// <paramref name="cadence"/> seconds from <see cref="FirstStart"/>; <paramref name="biases"/>
    /// frames of <paramref name="biasExposure"/> into biases/ and <paramref name="lights"/>
    /// frames into lights/ when asked, the lights every <paramref name="lightsCadence"/>
    /// seconds and ending an hour before the darks.
    /// </summary>
    internal SessionFixture WithDarks(int darks, double exposure = 119.2, double iso = 400, double cadence = 120, bool inSubfolder = true,
        int biases = 0, double biasExposure = 0.0002, int lights = 0, double lightsExposure = 119.2, double lightsIso = 400, double lightsCadence = 150)
    {
        string dir = inSubfolder ? DarksDir : Root;
        Directory.CreateDirectory(dir);
        for (int i = 0; i < darks; i++)
        {
            File.WriteAllBytes(Path.Combine(dir, $"DSC_{i + 1:0000}.fit"), Frame(FirstStart.AddSeconds(i * cadence), exposure, iso));
        }

        if (biases > 0)
        {
            Directory.CreateDirectory(BiasesDir);
            for (int i = 0; i < biases; i++)
            {
                File.WriteAllBytes(Path.Combine(BiasesDir, $"bias_{i + 1:0000}.fit"), Frame(FirstStart.AddSeconds(-7200 + i), biasExposure, iso));
            }
        }

        if (lights > 0)
        {
            Directory.CreateDirectory(LightsDir);
            DateTime start = FirstStart.AddSeconds(-3600 - lights * lightsCadence);
            for (int i = 0; i < lights; i++)
            {
                File.WriteAllBytes(Path.Combine(LightsDir, $"light_{i + 1:0000}.fit"), Frame(start.AddSeconds(i * lightsCadence), lightsExposure, lightsIso));
            }
        }

        return this;
    }

    /// <summary>
    /// Writes <paramref name="darks"/> synthetic darks built by <paramref name="dark"/> (given
    /// the 0-based index), one every 120 s from <see cref="FirstStart"/>, and
    /// <paramref name="biases"/> biases built by <paramref name="bias"/> an hour earlier.
    /// </summary>
    internal SessionFixture WithDarkSeries(int darks, Func<int, SyntheticDark> dark, int biases = 0, Func<int, SyntheticDark>? bias = null, double exposure = 120, double iso = 400)
    {
        ArgumentNullException.ThrowIfNull(dark);
        Directory.CreateDirectory(DarksDir);
        for (int i = 0; i < darks; i++)
        {
            File.WriteAllBytes(Path.Combine(DarksDir, $"DSC_{i + 1:0000}.fit"), dark(i).Build(FirstStart.AddSeconds(i * 120), exposure, iso));
        }

        if (biases > 0)
        {
            Directory.CreateDirectory(BiasesDir);
            for (int i = 0; i < biases; i++)
            {
                SyntheticDark b = bias?.Invoke(i) ?? new SyntheticDark { Seed = 500 + i, Noise = 2 };
                File.WriteAllBytes(Path.Combine(BiasesDir, $"bias_{i + 1:0000}.fit"), b.Build(FirstStart.AddSeconds(-3600 + i), 0.0002, iso));
            }
        }

        return this;
    }

    /// <summary>A frame with the metadata cards and a flat 300 ADU field (no pixel is read by the inventory); a null field leaves its card out.</summary>
    internal static byte[] Frame(DateTime? start, double? exposure, double? iso) =>
        FitsTestData.BuildFits(32, 24, (x, y) => 300, bitpix: 16, extraCards: Cards(start, exposure, iso));

    internal static string[] Cards(DateTime? start, double? exposure, double? iso)
    {
        List<string> cards = [FitsTestData.Card("INSTRUME", "'NIKON D3300'")];
        if (start is DateTime s)
        {
            cards.Add(FitsTestData.Card("DATE-OBS", "'" + s.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture) + "'"));
        }

        if (exposure is double e)
        {
            cards.Add(FitsTestData.Card("EXPTIME", e.ToString("0.####", CultureInfo.InvariantCulture)));
        }

        if (iso is double i)
        {
            cards.Add(FitsTestData.Card("ISOSPEED", i.ToString("0", CultureInfo.InvariantCulture)));
        }

        return [.. cards];
    }

    public void Dispose() => _ws.Dispose();
}
