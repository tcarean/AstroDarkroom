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
/// Regression, R-129. A signed difference that rounds to zero at the decimals shown reads
/// "+0.00", never "-+0.00". The format with sections "+0.00;-0.00" gave the latter for a
/// negative that rounds to zero: .NET formats it with the positive section and still puts the
/// minus in front. Seen on 2026-11-02 in a structure run whose master was the model of a
/// lensfun profile itself (the Samyang row and the comparison summary). One test per report
/// that composes such a difference; LightReport.Signed keeps "-0.00" on purpose, for the
/// parity with the Python prototype (LightReportTests).
/// </summary>
public class SignedZeroTests
{
    static readonly DateTime When = new(2026, 9, 28, 10, 31, 0);

    /// <summary>A comparison with one row whose master is 0.001 stops shallower than the profile.</summary>
    static VignettingComparison TinyNegative(TempWorkspace ws)
    {
        var sources = LensProfileTestData.Sources(ws);
        LensProfile p = sources.Catalog.ProfilesOf(sources.Catalog.Lenses.First())[0];
        var row = new ProfileDepth(p, cornerStops: 0.701, masterStops: 0.700, cameraCrop: 0, extrapolation: 0);
        return new VignettingComparison(0.700, null, new[] { row }, ComparisonGap.None, null);
    }

    [Fact]
    public void R129_AProfileRowsTinyNegativeDifferenceReadsPlusZero()
    {
        using var ws = new TempWorkspace();
        VignettingComparison c = TinyNegative(ws);

        ProfileRow row = Assert.Single(StructureReport.ProfileRows(c));

        Assert.Equal("+0.00 stopuri", row.Diff);
    }

    [Fact]
    public void R129_TheComparisonSummaryAndTheDepthDetailReadPlusZero()
    {
        using var ws = new TempWorkspace();
        VignettingComparison c = TinyNegative(ws);
        var o = new StructureOutcome(StructureAnalyzer.Analyze(TestImages.From(128, 128, (x, y) => 1000)), c, null, null);

        string summary = StructureReport.ComparisonSummary(c);
        string detail = StructureReport.DepthDetail(o);

        Assert.Contains("+0.00 stopuri fata de profil", summary);
        Assert.Contains("cu +0.00 stopuri fata de profilul", detail);
    }

    /// <summary>The owner's Tokina raw: ID 139, so the Adobe row decides and is the Split tab's reference (R-128).</summary>
    static LoadedFrame TokinaRaw() => new LoadedFrame
    {
        Header = new Dictionary<string, string>(),
        FocalMm = 20,
        FNumber = 2.8,
        Camera = "NIKON D3300",
        LensId = 139,
        LensMinFocalMm = 11,
        LensMaxFocalMm = 20,
        LensMaxAperture = 2.8,
    };

    /// <summary>
    /// The Split tab's difference cells, through the real run: the whole and both parts are the
    /// Adobe Tokina model raised to 0.995, a master a few thousandths of a stop shallower than
    /// its profile, the case seen on 2026-11-02.
    /// </summary>
    [Fact]
    public void R129_ASplitCellJustShallowerThanTheProfileReadsPlusZero()
    {
        const int W = 240, H = 160;
        using var ws = new TempWorkspace();
        var sources = LensProfileTestData.Sources(ws, tokinaOnTwoFormats: true);
        LensChoice tokina = sources.Catalog.Lenses.First(l => l.Source == ProfileSource.Adobe && l.Mount == "NIKON CORPORATION");
        Img model = SyntheticFlatBuilder.Build(LensCatalog.Nearest(sources.Catalog.ProfilesOf(tokina), 20, 2.8), W, H, 1.523).Flat;
        string Flat(string name) => ws.WriteFile(name, FitsTestData.BuildFits(W, H, (x, y) => Math.Pow(model.D[y * W + x], 0.995) * 20000));
        string whole = Flat("whole.fit");
        string[] parts = { Flat("part-1.fit"), Flat("part-2.fit") };
        var query = new StructureQuery { Sources = sources, RawPath = "DSC_0001.NEF", LoadInfo = _ => TokinaRaw() };

        SplitResult r = SplitRun.Execute(whole, parts, query, When, 10, new StringWriter()).Result;

        Assert.True(r.ReferenceByAdobe);
        double diff = SplitRun.Against(r.Whole.Outcome, r.ReferenceProfile).DiffStops;
        Assert.True(diff < 0 && diff > -0.005, $"the case needs a tiny negative difference, got {diff:F4}");
        SplitRow reference = Assert.Single(SplitReport.Rows(r), x => x.IsReference);
        Assert.All(reference.Cells.Take(3), cell => Assert.StartsWith("+0.00 stopuri = ", cell.Value));
    }

    /// <summary>The ends of the map's scale, in the legend and in the mosaic's title.</summary>
    [Fact]
    public void R129_AScaleEndJustBelowTheModelReadsPlusZero()
    {
        var range = new PngWriter.MapRange(PngWriter.Mid - 1e-5, 1.03);

        string legend = RunReport.MapLegend(range);
        string title = SplitReport.PngTitle(When, range);

        Assert.Equal("albastru = +0.0 %   alb = 0 (exact pe model)   rosu = +3.0 %", legend);
        Assert.Contains("ALBASTRU +0.0 %", title);
    }

    /// <summary>The screen-to-raw gap, in whole points in the table and with one decimal in the reading.</summary>
    [Fact]
    public void R129_AScreenJustBelowTheRawReadsPlusZeroPoints()
    {
        ExposureResult r = ExposureAnalyzer.Analyze(TestImages.FlatCfa(128, 128, 550), 1000, null, false);
        ChannelLevels raw = r.Channels[r.BrightestChannel];
        r.Preview = new PreviewLevels
        {
            Channels = new[] { new ChannelLevels { Colour = raw.Colour, CentreMedianPct = raw.CentreMedianPct - 0.04 } },
            Source = "test",
        };

        string reading = ExposureReport.ScreenReading(r);

        Assert.Equal("+0 puncte", ExposureReport.ScreenGap(50.0, 49.6));
        Assert.Contains(": +0.0 puncte, in", reading);
    }

    /// <summary>The local spot already showed zero as zero (ADR 0041); pinned so the shared helper keeps it.</summary>
    [Fact]
    public void R129_ALocalSpotThatRoundsToZeroReadsPlusZero()
    {
        Assert.Equal("+0.00 %", StructureReport.LocalSpotSize(-0.001));
        Assert.Equal("+0.00 %", StructureReport.LocalSpotSize(-0.0));
    }
}
