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
/// The split comparison in the core (R-093): N part masters beside the whole session's
/// master, one structure run each, one table, one report, one mosaic of residual maps.
/// The window copies files and runs Siril; nothing here needs either.
/// </summary>
public class SplitRunTests
{
    const int Size = 256;
    static readonly DateTime When = new(2026, 9, 28, 10, 31, 0);

    static string WriteFlat(TempWorkspace ws, string name, double k)
        => ws.WriteFile(name, FitsTestData.BuildFits(Size, Size, FitsTestData.Radial(Size, Size, k: k)));

    static LoadedFrame RawFrame() => new()
    {
        Header = new Dictionary<string, string>(),
        FocalMm = 20,
        FNumber = 2.8,
        Camera = "NIKON D3300",
        NikonLensId = 139,
        LensMinFocalMm = 11,
        LensMaxFocalMm = 20,
        LensMaxAperture = 2.8,
    };

    /// <summary>The whole at k = 0.45 and two parts either side of it; the query is the fixture profiles with a fabricated raw.</summary>
    static (string Whole, string[] Parts, StructureQuery? Query) Fixture(TempWorkspace ws, bool withRaw = true)
    {
        string whole = WriteFlat(ws, "intreg.fit", 0.45);
        string[] parts = { WriteFlat(ws, "parte-1.fit", 0.40), WriteFlat(ws, "parte-2.fit", 0.50) };
        StructureQuery? query = withRaw
            ? new StructureQuery { Sources = LensProfileTestData.Sources(ws), RawPath = "DSC_0001.NEF", LoadInfo = _ => RawFrame() }
            : null;
        return (whole, parts, query);
    }

    [Fact]
    public void R093_SplitRun_HasOneColumnPerPartTheWholeAndTheReferenceProfile()
    {
        using var ws = new TempWorkspace();
        var (whole, parts, query) = Fixture(ws);

        var run = SplitRun.Execute(whole, parts, query, When, 10, new StringWriter());
        SplitResult r = run.Result;

        Assert.Equal(2, r.Parts.Count);
        Assert.True(r.HasReference);
        Assert.Equal(new[] { "partea 1 din 2", "partea 2 din 2", "intreg" }, r.Columns.Select(c => c.Label));
        Assert.Equal(4, SplitReport.Headers(r).Count);

        // the reference is the whole's nearest profile, and every column is measured against that same one
        Assert.Same(r.Whole.Outcome.Profiles.Nearest.Profile, r.ReferenceProfile);
        Assert.All(r.Columns, c => Assert.Equal(r.ReferenceProfile.File, c.Reference.Profile.File));
        Assert.All(r.Columns, c => Assert.Equal(c.Outcome.Structure.VignetteStops - c.Reference.CornerStops, c.Reference.DiffStops, 9));
    }

    [Fact]
    public void R093_SplitRun_EveryTestCellCarriesItsVerdict_AndTheLastRowIsTheRunVerdict()
    {
        using var ws = new TempWorkspace();
        var (whole, parts, query) = Fixture(ws);

        SplitResult r = SplitRun.Execute(whole, parts, query, When, 10, new StringWriter()).Result;
        var rows = SplitReport.Rows(r);

        // five tests, the depth, the difference from the profile, the verdict
        Assert.Equal(8, rows.Count);
        Assert.Equal(StructureReport.TestKeys, rows.Take(5).Select(x => x.Key));
        Assert.All(rows, row => Assert.Equal(4, row.Cells.Count));
        foreach (SplitRow test in rows.Take(5))
        {
            Assert.All(test.Cells.Take(3), cell => Assert.Contains(cell.Verdict, Stats.ResidualBands));
            Assert.Null(test.Cells[3].Verdict);                  // a profile has no shape to test
        }
        SplitRow depth = rows[5];
        Assert.Equal("VignetteStops", depth.Key);
        Assert.Contains("stopuri", depth.Cells[3].Value);         // the profile's own depth
        SplitRow diff = rows[6];
        Assert.Equal("DiffStops", diff.Key);
        Assert.All(diff.Cells.Take(3), cell => Assert.Contains(cell.Verdict, Stats.ResidualBands));
        SplitRow verdict = rows[7];
        Assert.Equal("Verdict", verdict.Key);
        Assert.Equal(r.Columns.Select(c => c.Outcome.Verdict), verdict.Cells.Take(3).Select(c => c.Verdict));
    }

    [Fact]
    public void R093_SplitRun_WithoutARawThereIsNoProfileColumnAndNoDifferenceRow()
    {
        using var ws = new TempWorkspace();
        var (whole, parts, _) = Fixture(ws, withRaw: false);

        SplitResult r = SplitRun.Execute(whole, parts, null, When, 10, new StringWriter()).Result;
        var rows = SplitReport.Rows(r);

        Assert.False(r.HasReference);
        Assert.Equal(3, SplitReport.Headers(r).Count);
        Assert.Equal(7, rows.Count);
        Assert.DoesNotContain(rows, row => row.Key == "DiffStops");
        Assert.All(rows, row => Assert.Equal(3, row.Cells.Count));
        Assert.All(r.Columns, c => Assert.Equal(c.Outcome.Structure.Verdict, c.Outcome.Verdict));
    }

    [Fact]
    public void R093_SplitRun_TheReportCarriesTheDateTheColumnsAndTheMapOrder()
    {
        using var ws = new TempWorkspace();
        var (whole, parts, query) = Fixture(ws);
        var w = new StringWriter();

        RunReport.WriteBanner(w);
        SplitRun.Execute(whole, parts, query, When, 10, w);
        string text = w.ToString();

        Assert.StartsWith(VersionInfo.Banner, text);
        Assert.Contains("2026-09-28 10:31", text);
        Assert.Contains(whole, text);
        Assert.Contains("2 x 10", text);
        Assert.Contains("partea 1 din 2", text);
        Assert.Contains("intreg", text);
        foreach (string key in StructureReport.TestKeys) Assert.Contains(StructureReport.TestLabel(key), text);
        Assert.Contains(SplitReport.MapOrder(2), text);
        Assert.Contains(SplitReport.Meaning().Split('\n')[0], text);
    }

    [Fact]
    public void R093_SplitRun_TheMosaicIsOneRowOfLabelledTilesUnderATitleOnOneSharedScale()
    {
        using var ws = new TempWorkspace();
        var (whole, parts, query) = Fixture(ws);

        var run = SplitRun.Execute(whole, parts, query, When, 10, new StringWriter());
        SplitResult r = run.Result;
        var png = PngDecoder.Decode(run.Png);

        int w = r.Whole.Outcome.Structure.Width, h = r.Whole.Outcome.Structure.Height;
        int band = PngWriter.MosaicBandHeight;
        Assert.Equal(3 * w + 2 * PngWriter.MosaicGap, png.Width);
        Assert.Equal(2 * band + h, png.Height);

        // the shared scale is the automatic one over every map at once
        var all = new Img(1, 3 * w * h);
        int at = 0;
        foreach (SplitColumn c in r.Columns) { Array.Copy(c.Outcome.Structure.Residual.D, 0, all.D, at, w * h); at += w * h; }
        PngWriter.MapRange expected = PngWriter.AutoRange(all);
        Assert.Equal(expected.Lo, r.MapRange.Lo, 12);
        Assert.Equal(expected.Hi, r.MapRange.Hi, 12);

        // a pixel of the second tile is that map's value on that scale, not on its own
        Img second = r.Parts[1].Outcome.Structure.Residual;
        var (er, eg, eb) = PngWriter.Colormap(second.At(h / 2, w / 2), r.MapRange.Lo, PngWriter.Mid, r.MapRange.Hi);
        Assert.Equal((er, eg, eb), png.At(w + PngWriter.MosaicGap + w / 2, 2 * band + h / 2));

        // the title band and the label band hold text: some pixels in the text colour
        Assert.Contains(PngWriter.MosaicText, BandPixels(png, 0, band));
        Assert.Contains(PngWriter.MosaicText, BandPixels(png, band, 2 * band));
    }

    static IEnumerable<(byte, byte, byte)> BandPixels(DecodedPng png, int y0, int y1)
    {
        for (int y = y0; y < y1; y++)
            for (int x = 0; x < png.Width; x++)
                yield return png.At(x, y);
    }

    [Fact]
    public void R093_SplitRun_TheTitleNamesTheVersionTheDateAndTheScaleEnds()
    {
        string title = SplitReport.PngTitle(When, new PngWriter.MapRange(0.9, 1.1));

        Assert.Contains(VersionInfo.Value.ToUpperInvariant(), title);
        Assert.Contains("2026-09-28 10:31", title);
        Assert.Contains("-10.0", title);
        Assert.Contains("+10.0", title);
        Assert.All(title, c => Assert.True(BitmapFont.Has(c), $"'{c}' has no glyph"));
        Assert.All(SplitReport.PngLabel(3, 4), c => Assert.True(BitmapFont.Has(c)));
        Assert.All(SplitReport.PngLabelWhole, c => Assert.True(BitmapFont.Has(c)));
    }

    [Fact]
    public void R093_SplitRun_ThePartCountAndTheOrderComeFromTheCaller()
    {
        using var ws = new TempWorkspace();
        var (whole, parts, _) = Fixture(ws, withRaw: false);

        SplitResult r = SplitRun.Execute(whole, parts.Reverse().ToArray(), null, When, 7, new StringWriter()).Result;

        Assert.Equal(7, r.FramesPerPart);
        Assert.Equal(parts[1], r.Parts[0].Path);
        Assert.Equal(parts[0], r.Parts[1].Path);
        Assert.Equal(When, r.When);
    }
}
