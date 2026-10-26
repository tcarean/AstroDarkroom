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

namespace FlatCheck.Tests;

/// <summary>
/// The synthetic FITS files of the parity scenarios (39 since ADR 0067): the exact bytes the Python
/// prototype and the C# were compared on until 2026-09-26, when the cross-check was
/// retired and the prototype frozen at 2.2.0 (ADR 0047). <see cref="ParityGoldenTests"/>
/// runs the console over them and pins the text; <see cref="CrossCheckFixtures"/> can
/// still dump them to disk for a comparison by hand against the frozen flatcheck.py.
///
/// The scenes and their file names are the ones SCENARIOS in crosscheck.py names; do not
/// rename a file without renaming the scenario, or the golden no longer says what it pins.
/// </summary>
internal static class ParityFixtures
{
    const int Size = 512;

    internal static void WriteAll(string dir)
    {
        Directory.CreateDirectory(dir);

        var radial = FitsTestData.Radial(Size, Size);
        var gradient = FitsTestData.WithLinearGradient(FitsTestData.Radial(Size, Size), Size, 0.12);
        var rawCfa = FitsTestData.WithBayerMosaic(FitsTestData.Radial(Size, Size, center: 34000));
        var raw12 = FitsTestData.WithBayerMosaic(FitsTestData.Radial(Size, Size, center: 3600));

        Write(dir, "clean.fit", radial);        // structure / light / debayered-expo
        Write(dir, "grad.fit", gradient);       // structure
        Write(dir, "raw_cfa.fit", rawCfa);      // --expo and --expo --sky
        Write(dir, "raw12bit.fit", raw12);      // --expo --sat 4095 --black 150

        // The same mosaic, but with the colour order written in the header - as Siril writes
        // it on a frame passed through Conversion. Both implementations read BAYERPAT, so the
        // channels line must come out identical; on a raw the source would differ (libraw vs
        // rawpy), like the scale line.
        File.WriteAllBytes(Path.Combine(dir, "raw_bayerpat.fit"),
            FitsTestData.BuildFits(Size, Size, rawCfa, extraCards: FitsTestData.BayerCards("RGGB")));

        // And with ROWORDER 'BOTTOM-UP', as Siril actually writes it: on an even height the
        // mosaic's phase moves by one row, and both implementations must move it.
        File.WriteAllBytes(Path.Combine(dir, "raw_bayerpat_bottomup.fit"),
            FitsTestData.BuildFits(Size, Size, rawCfa,
                extraCards: FitsTestData.BayerCards("RGGB", rowOrder: "BOTTOM-UP")));

        // --- wider coverage, to exercise branches the golden scenarios miss ---

        // over-correction: background RISES toward the edges, so radPct goes negative
        Write(dir, "overcorrected.fit", TestImages.InvertedRadial(Size, Size));
        // a pure sky plane with no radial term at all
        Write(dir, "skyplane.fit", (x, y) => 8000 * (1.0 + 0.30 * (x / (double)Size - 0.5)));
        // a flat field: every structure metric should sit at zero
        Write(dir, "uniform.fit", FitsTestData.Constant(20000));
        // mild vs heavy vignetting, to move the stops figure
        Write(dir, "vign_mild.fit", FitsTestData.Radial(Size, Size, k: 0.15));
        Write(dir, "vign_heavy.fit", FitsTestData.Radial(Size, Size, k: 0.70));
        // gradients sized to land in each band of the residual scale, so the
        // scenarios exercise all four and not just the two the other scenes hit
        Write(dir, "grad_bun.fit",                       // ~5% -> BUN
            FitsTestData.WithLinearGradient(FitsTestData.Radial(Size, Size), Size, 0.05));
        Write(dir, "grad_refa.fit",                      // ~57% -> REFA
            FitsTestData.WithLinearGradient(FitsTestData.Radial(Size, Size), Size, 0.60));
        // a diagonal gradient - the golden one is horizontal only
        Write(dir, "diag.fit", (x, y) =>
            FitsTestData.Radial(Size, Size)(x, y)
            * (1.0 + 0.10 * ((x + y) / (2.0 * Size) - 0.5)));
        // nebulosity in one quadrant, to drive the sigma-clipping
        Write(dir, "nebula.fit", (x, y) =>
            FitsTestData.Radial(Size, Size)(x, y)
            + (x > Size * 0.6 && y > Size * 0.6 ? 6000 : 0));
        // an uncalibrated flat still carrying its offset pedestal
        Write(dir, "pedestal.fit", FitsTestData.WithOffset(FitsTestData.Radial(Size, Size), 600));

        // non-square, so the w/h asymmetries in every metric get exercised
        File.WriteAllBytes(Path.Combine(dir, "wide.fit"),
            FitsTestData.BuildFits(768, 512, FitsTestData.Radial(768, 512)));
        File.WriteAllBytes(Path.Combine(dir, "tall.fit"),
            FitsTestData.BuildFits(512, 768, FitsTestData.WithLinearGradient(
                FitsTestData.Radial(512, 768), 512, 0.12)));

        // other BITPIX, to confirm the reader's scaling
        File.WriteAllBytes(Path.Combine(dir, "float32.fit"),
            FitsTestData.BuildFits(Size, Size, radial, bitpix: -32));
        File.WriteAllBytes(Path.Combine(dir, "int32.fit"),
            FitsTestData.BuildFits(Size, Size, radial, bitpix: 32));
        // what Siril actually writes: float, divided by 65535 - pins the levels line,
        // which used to print "0 / 0" on every real master
        File.WriteAllBytes(Path.Combine(dir, "float01.fit"),
            FitsTestData.BuildFits(Size, Size, FitsTestData.Scaled(radial, 1.0 / 65535), bitpix: -32));
        // a dust shadow: the local-spot warning and its position
        Write(dir, "dust.fit", (x, y) =>
        {
            double dx = x - 0.3 * Size, dy = y - 0.3 * Size;
            return radial(x, y) * (dx * dx + dy * dy < 12 * 12 ? 0.95 : 1.0);
        });

        // exposure levels either side of the band boundaries
        foreach (var (name, pct) in new[]
                 { ("expo25", 25.0), ("expo30", 30.0), ("expo50", 50.0),
                   ("expo65", 65.0), ("expo81", 81.0) })
            File.WriteAllBytes(Path.Combine(dir, name + ".fit"),
                FitsTestData.BuildFits(256, 256, (x, y) =>
                {
                    double bright = 4095 * pct / 100.0;
                    if (y % 2 == 0 && x % 2 == 0) return bright * 0.62;
                    if (y % 2 == 1 && x % 2 == 1) return bright * 0.48;
                    return bright;
                }));

        // --light: the applicability guard. The scale comes from a plate solve, so the field
        // is exact and the refusal must be the same every time.
        File.WriteAllBytes(Path.Combine(dir, "widefield.fit"),
            FitsTestData.BuildFits(Size, Size, radial,
                extraCards: FitsTestData.WcsCards(0.1)));       // diag 724 px -> 72.4 degrees

        // --light on a series. Two subfolders, one for each verdict branch.
        // The names are Siril's, so that they pass the default pattern.
        SeriesDir(dir, "serie_stabil", Enumerable.Range(0, 6)
            .Select(i => FitsTestData.Radial(Size, Size, k: 0.44 + 0.002 * i)).ToArray());

        // k rises from under- to over-correction and the sky plane falls with it: exactly
        // the pattern of the real session, in which the two proved correlated at -0.94
        SeriesDir(dir, "serie_baleiaj", Enumerable.Range(0, 6)
            .Select(i => FitsTestData.WithLinearGradient(
                TestImages.InvertedRadial(Size, Size, k: -0.5 + 0.2 * i), Size, 0.6 - 0.1 * i))
            .ToArray());
    }

    static void SeriesDir(string root, string name, params Func<int, int, double>[] scenes)
    {
        string dir = Path.Combine(root, name);
        Directory.CreateDirectory(dir);
        for (int i = 0; i < scenes.Length; i++)
            File.WriteAllBytes(Path.Combine(dir, $"pp_light_{i + 1:00000}.fit"),
                FitsTestData.BuildFits(Size, Size, scenes[i]));
    }

    static void Write(string dir, string name, Func<int, int, double> scene)
        => File.WriteAllBytes(Path.Combine(dir, name), FitsTestData.BuildFits(Size, Size, scene));
}
