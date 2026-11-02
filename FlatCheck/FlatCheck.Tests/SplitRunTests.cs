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

    /// <summary>
    /// A fabricated raw of the Tokina's range with a lens ID no profile carries, so the lenses
    /// come by the range alone and the nearest row decides on each master: the path where a
    /// part's own deciding row can differ from the whole's (with the raw's 139 the Adobe row
    /// decides on every master, R-128, and AdobeVerdictTests pins that).
    /// </summary>
    static LoadedFrame RawFrame() => new()
    {
        Header = new Dictionary<string, string>(),
        FocalMm = 20,
        FNumber = 2.8,
        Camera = "NIKON D3300",
        LensId = 1,
        LensMinFocalMm = 11,
        LensMaxFocalMm = 20,
        LensMaxAperture = 2.8,
    };

    /// <summary>
    /// The whole at k = 0.45 and two parts either side of it, with the fixture profiles on two
    /// formats and a fabricated raw found by range: the whole's nearest is the full-frame
    /// lensfun entry (0.905 stops), the deeper part's own nearest is the Adobe one (1.248), so
    /// the reference rule has something to pin.
    /// </summary>
    static (string Whole, string[] Parts, StructureQuery? Query) Fixture(TempWorkspace ws, bool withRaw = true)
    {
        string whole = WriteFlat(ws, "intreg.fit", 0.45);
        string[] parts = { WriteFlat(ws, "parte-1.fit", 0.30), WriteFlat(ws, "parte-2.fit", 0.60) };
        StructureQuery? query = withRaw
            ? new StructureQuery { Sources = LensProfileTestData.Sources(ws, tokinaOnTwoFormats: true), RawPath = "DSC_0001.NEF", LoadInfo = _ => RawFrame() }
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

        // the reference is the row that gives the whole's verdict (by range alone, its nearest),
        // and every column is measured against that same one - which only means something when a
        // part's own deciding row is a different profile (ADR 0054's measurement)
        Assert.False(r.ReferenceByAdobe);
        Assert.Same(r.Whole.Outcome.Profiles.Deciding.Profile, r.ReferenceProfile);
        Assert.Contains(r.Parts, p => !ReferenceEquals(p.Outcome.Profiles.Deciding.Profile, r.ReferenceProfile));
        Assert.All(r.Columns, c => Assert.Equal(r.ReferenceProfile.File, c.Reference.Profile.File));
        Assert.All(r.Columns, c => Assert.Equal(c.Outcome.Structure.VignetteStops - c.Reference.CornerStops, c.Reference.DiffStops, 9));
    }

    [Fact]
    public void R093_SplitRun_EveryTestCellCarriesItsVerdict_AndTheLastRowIsTheColumnVerdictAgainstTheReference()
    {
        using var ws = new TempWorkspace();
        var (whole, parts, query) = Fixture(ws);

        SplitResult r = SplitRun.Execute(whole, parts, query, When, 10, new StringWriter()).Result;
        var rows = SplitReport.Rows(r);

        // five tests, the depth, one row per profile of the whole's comparison (as on the Structure
        // tab), the verdict
        Assert.True(r.Profiles.Count >= 2, "the fixture must offer more than one profile");
        Assert.Equal(6 + r.Profiles.Count + 1, rows.Count);
        Assert.Equal(StructureReport.TestKeys, rows.Take(5).Select(x => x.Key));
        Assert.All(rows, row => Assert.Equal(4, row.Cells.Count));
        foreach (SplitRow test in rows.Take(5))
        {
            Assert.All(test.Cells.Take(3), cell => Assert.Contains(cell.Verdict, Stats.ResidualBands));
            Assert.Null(test.Cells[3].Verdict);                  // a profile has no shape to test
        }
        SplitRow depth = rows[5];
        Assert.Equal("VignetteStops", depth.Key);
        Assert.Equal(SplitReport.Dash, depth.Cells[3].Value);

        var diffs = rows.Where(x => x.Key == SplitReport.DiffKey).ToList();
        Assert.Equal(r.Profiles.Count, diffs.Count);
        Assert.Equal(r.Profiles.Select(p => SplitReport.ProfileLabel(p.Profile)), diffs.Select(d => d.Label));
        foreach (SplitRow diff in diffs)
        {
            Assert.All(diff.Cells.Take(3), cell => Assert.Contains(cell.Verdict, Stats.ResidualBands));
            Assert.Contains("stopuri", diff.Cells[3].Value);     // that profile's own depth
            Assert.Null(diff.Cells[3].Verdict);
        }
        SplitRow reference = Assert.Single(diffs, d => d.IsReference);
        Assert.Equal(SplitReport.ProfileLabel(r.ReferenceProfile), reference.Label);
        Assert.Contains("REFERINTA", reference.Note);
        Assert.Equal(r.ReferenceDepth.Verdict, reference.Cells[2].Verdict);   // the whole against the reference
        Assert.Equal(r.Whole.Reference.Verdict, reference.Cells[2].Verdict);

        SplitRow verdict = rows[^1];
        Assert.Equal("Verdict", verdict.Key);
        Assert.Equal(r.Columns.Select(c => c.Verdict), verdict.Cells.Take(3).Select(c => c.Verdict));

        // the column verdict is the worse of shape and the depth AGAINST THE REFERENCE - the row
        // above it - not the run's own verdict, whose depth is against the column's own nearest
        // profile
        foreach (SplitColumn c in r.Columns)
        {
            string worse = Stats.Severity(c.Reference.Verdict) > Stats.Severity(c.Outcome.Structure.Verdict)
                ? c.Reference.Verdict : c.Outcome.Structure.Verdict;
            Assert.Equal(worse, c.Verdict);
        }
        Assert.Contains(r.Columns, c => c.Verdict != c.Outcome.Verdict);
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
        Assert.Empty(r.Profiles);
        Assert.DoesNotContain(rows, row => row.Key == SplitReport.DiffKey);
        Assert.All(rows, row => Assert.Equal(3, row.Cells.Count));
        Assert.All(r.Columns, c => Assert.Equal(c.Outcome.Structure.Verdict, c.Verdict));
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

    /// <summary>
    /// The disk estimate shown before a split: the copies exact, Siril's process/ from a
    /// bytes-per-pixel model measured on 2026-09-28 (a 30-bias, 10-flat, 2-light quarter of a 24.2
    /// Mpx session wrote 4148 MiB).
    /// </summary>
    [Fact]
    public void R091_SplitCost_EstimatesSirilsProcessFolderWithinTenPercentOfTheMeasuredQuarter()
    {
        const long pixels = 6016L * 4016;
        const long measuredQuarter = 4148L * 1024 * 1024;

        SplitCostEstimate one = SplitCost.Estimate(pixels, 1, wholeFrames: 30, wholeFolders: 1, flatsPerPart: 10, lightsPerPart: 2, copiedBytes: 673_000_000);
        SplitCostEstimate four = SplitCost.Estimate(pixels, 4, wholeFrames: 30, wholeFolders: 1, flatsPerPart: 10, lightsPerPart: 2, copiedBytes: 4 * 673_000_000L);

        Assert.Equal(673_000_000, one.CopiedBytes);
        Assert.InRange(one.ProcessBytes, measuredQuarter * 0.9, measuredQuarter * 1.1);
        Assert.Equal(4 * one.ProcessBytes, four.ProcessBytes);
        Assert.Equal(one.CopiedBytes + one.ProcessBytes, one.TotalBytes);

        // no sensor size: the copies are still exact, process/ is unknown, and the line says so
        SplitCostEstimate unknown = SplitCost.Estimate(0, 4, 30, 1, 10, 2, 100_000_000);
        Assert.Equal(0, unknown.ProcessBytes);
        Assert.Contains("nu se stie", SplitReport.CostLine(unknown, null));

        string line = SplitReport.CostLine(four, 187_000_000_000);
        Assert.Contains("2.5 GB copiate", line);            // Explorer's units: 2 692 000 000 B = 2.5 GiB
        Assert.Contains("GB scrise de Siril", line);
        Assert.Contains("174.2 GB", line);
        Assert.Equal("512 MB", SplitReport.Gb(512L << 20));

        // the pixel count comes from the FITS header, in the core
        Assert.Equal(6016L * 4016, SplitCost.Pixels(new Dictionary<string, string> { ["NAXIS1"] = "6016", ["NAXIS2"] = "4016" }));
        Assert.Equal(0, SplitCost.Pixels(new Dictionary<string, string>()));
        Assert.Equal(0, SplitCost.Pixels(null));
        Assert.DoesNotContain("minut", line);                // the time is not estimated (owner's decision)
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
