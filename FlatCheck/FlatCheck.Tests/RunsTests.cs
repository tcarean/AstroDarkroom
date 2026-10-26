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
/// The Runs/ layer is the only thing the console application and the WPF one share.
/// Its whole value is in the promise "both get exactly the same figures and exactly
/// the same text", so exactly that is checked here - not that the chain runs, but that
/// running through it gives the same thing as running beside it.
///
/// Why the golden tests are not enough: they check what the CONSOLE writes. If tomorrow
/// the window called the analyzer directly, instead of Run, the goldens would stay green
/// and the two applications would report different figures on the same file. The tests
/// here are the ones that would turn red.
/// </summary>
public class RunsTests
{
    const int Size = 256;

    static string WriteFlat(TempWorkspace ws, string name = "flat.fit")
        => ws.WriteFile(name, FitsTestData.BuildFits(Size, Size, FitsTestData.Radial(Size, Size)));

    // ---------------------------------------------------------------- structure

    /// <summary>
    /// The figures that come out through Run are the analyzer's, not a second calculation.
    /// </summary>
    [Fact]
    public void StructureRun_ProducesTheSameNumbersAsCallingTheAnalyzerDirectly()
    {
        using var ws = new TempWorkspace();
        string path = WriteFlat(ws);

        var direct = StructureAnalyzer.Analyze(ImageLoader.LoadImage(path));
        var run = StructureRun.Execute(path, new StringWriter());

        Assert.Equal(direct.ResidualPtpPct, run.Result.Structure.ResidualPtpPct, 12);
        Assert.Equal(direct.CornerSpreadPct, run.Result.Structure.CornerSpreadPct, 12);
        Assert.Equal(direct.AzimuthalMaxPct, run.Result.Structure.AzimuthalMaxPct, 12);
        Assert.Equal(direct.TiltPct, run.Result.Structure.TiltPct, 12);
        Assert.Equal(direct.Verdict, run.Result.Structure.Verdict);
    }

    /// <summary>
    /// The text that reaches "Rezultat testare.txt" must be the same as the console's.
    /// The requirement is literal, so the test is literal.
    /// </summary>
    [Fact]
    public void StructureRun_WritesTheSameTextAsTheReportWouldOnItsOwn()
    {
        using var ws = new TempWorkspace();
        string path = WriteFlat(ws);

        var expected = new StringWriter();
        StructureReport.Write(StructureAnalyzer.Analyze(ImageLoader.LoadImage(path)), path, expected);

        var actual = new StringWriter();
        StructureRun.Execute(path, actual);

        Assert.Equal(expected.ToString(), actual.ToString());
    }

    /// <summary>
    /// The map comes back as bytes, and they are exactly the bytes the console variant
    /// would have written to disk - otherwise the window would show another image than the
    /// saved one.
    /// </summary>
    [Fact]
    public void StructureRun_ReturnsTheSamePngBytesThatWouldBeWrittenToDisk()
    {
        using var ws = new TempWorkspace();
        string path = WriteFlat(ws);
        string png = ws.PathTo("map.png");

        var run = StructureRun.Execute(path, new StringWriter());
        PngWriter.WriteResidualMap(png, run.Result.Structure.Residual);

        Assert.Equal(File.ReadAllBytes(png), run.Png);
    }

    [Fact]
    public void StructureRun_ReturnsADecodablePng()
    {
        using var ws = new TempWorkspace();
        var run = StructureRun.Execute(WriteFlat(ws), new StringWriter());

        var img = PngDecoder.Decode(run.Png);

        Assert.Equal(run.Result.Structure.Width, img.Width);
        Assert.Equal(run.Result.Structure.Height, img.Height);
    }

    // ---------------------------------------------------------------- exposure

    [Fact]
    public void ExposureRun_ProducesTheSameNumbersAsTheAnalyzer()
    {
        using var ws = new TempWorkspace();
        string path = ws.WriteFile("brut.fit",
            FitsTestData.BuildFits(64, 64,
                FitsTestData.WithBayerMosaic(FitsTestData.Radial(64, 64, center: 34000))));

        LoadedFrame f = ImageLoader.Load(path);
        var direct = ExposureAnalyzer.Analyze(f.Image, null, null, false, f.Scale);
        var run = ExposureRun.Execute(path, null, null, false, new StringWriter());

        Assert.Equal(direct.CentreMedianPct, run.Result.CentreMedianPct, 12);
        Assert.Equal(direct.Verdict, run.Result.Verdict);
    }

    /// <summary>
    /// The mode measures a level, it has nothing to draw. The window relies on null so as
    /// not to show an empty image panel on the exposure tab.
    /// </summary>
    [Fact]
    public void ExposureRun_ProducesNoMap()
    {
        using var ws = new TempWorkspace();
        string path = ws.WriteFile("brut.fit",
            FitsTestData.BuildFits(64, 64,
                FitsTestData.WithBayerMosaic(FitsTestData.Radial(64, 64, center: 34000))));

        Assert.Null(ExposureRun.Execute(path, null, null, false, new StringWriter()).Png);
    }

    [Fact]
    public void ExposureRun_HonoursTheSkyFlag()
    {
        using var ws = new TempWorkspace();
        string path = ws.WriteFile("sky.fit",
            FitsTestData.BuildFits(64, 64,
                FitsTestData.WithBayerMosaic(FitsTestData.Radial(64, 64, center: 34000))));

        var panel = ExposureRun.Execute(path, null, null, false, new StringWriter());
        var sky = ExposureRun.Execute(path, null, null, true, new StringWriter());

        Assert.True(sky.Result.IsSky);
        Assert.False(panel.Result.IsSky);
        Assert.NotEqual(panel.Result.Verdict, sky.Result.Verdict);
    }

    // ---------------------------------------------------------------- light, one frame

    [Fact]
    public void LightRun_ProducesTheSameNumbersAsTheAnalyzer()
    {
        using var ws = new TempWorkspace();
        string path = ws.WriteFile("light.fit",
            FitsTestData.BuildFits(Size, Size, FitsTestData.Radial(Size, Size)));

        var direct = LightAnalyzer.Analyze(ImageLoader.LoadImage(path));
        var run = LightRun.Execute(path, null, new StringWriter());

        Assert.False(run.Refused);
        Assert.Equal(direct.RadPct, run.Result.RadPct, 12);
        Assert.Equal(direct.PlanePct, run.Result.PlanePct, 12);
        Assert.NotNull(run.Png);
    }

    /// <summary>
    /// The refusal is the absence of a verdict, not a bad verdict: no figure and no map is
    /// produced. The window relies on this so as not to show a half-filled results panel.
    /// </summary>
    [Fact]
    public void LightRun_OnATooWideField_RefusesWithoutProducingNumbersOrAMap()
    {
        using var ws = new TempWorkspace();
        string path = ws.WriteFile("wide.fit", FitsTestData.BuildFits(
            Size, Size, FitsTestData.Radial(Size, Size),
            extraCards: FitsTestData.WcsCards(0.2)));   // 256x256, diag 362 px -> ~72 deg

        var run = LightRun.Execute(path, null, new StringWriter());

        Assert.True(run.Refused);
        Assert.Null(run.Result);
        Assert.Null(run.Png);
        Assert.Equal(FovLevel.TooWide, run.Suitability.Level);
    }

    /// <summary>The refusal explains why, even though it gives no figures.</summary>
    [Fact]
    public void LightRun_OnATooWideField_StillWritesAnExplanation()
    {
        using var ws = new TempWorkspace();
        string path = ws.WriteFile("wide.fit", FitsTestData.BuildFits(
            Size, Size, FitsTestData.Radial(Size, Size),
            extraCards: FitsTestData.WcsCards(0.2)));   // 256x256, diag 362 px -> ~72 deg

        var w = new StringWriter();
        LightRun.Execute(path, null, w);

        Assert.Contains("TESTUL NU SE APLICA", w.ToString());
    }

    /// <summary>--fov tells the truth, it does not force: a declared wide field is still
    /// refused.</summary>
    [Fact]
    public void LightRun_HonoursTheFovOverride()
    {
        using var ws = new TempWorkspace();
        string path = ws.WriteFile("light.fit",
            FitsTestData.BuildFits(Size, Size, FitsTestData.Radial(Size, Size)));

        Assert.False(LightRun.Execute(path, 10.0, new StringWriter()).Refused);
        Assert.True(LightRun.Execute(path, 70.0, new StringWriter()).Refused);
    }

    // ---------------------------------------------------------------- light, series

    static string WriteSeries(TempWorkspace ws, int n)
    {
        for (int i = 0; i < n; i++)
            ws.WriteFile($"pp_light_{i:D5}.fit", FitsTestData.BuildFits(
                128, 128, FitsTestData.Radial(128, 128, k: 0.44 + 0.002 * i)));
        return ws.Dir;
    }

    [Fact]
    public void LightSeriesRun_CombinesEveryMatchingFrame()
    {
        using var ws = new TempWorkspace();
        string dir = WriteSeries(ws, 6);

        var run = LightSeriesRun.Execute(dir, "pp_light_*.fit", null, new StringWriter());

        Assert.False(run.Refused);
        Assert.Equal(6, run.Result.Count);
        Assert.Equal(6, run.Files.Count);
        Assert.NotNull(run.Png);
    }

    /// <summary>
    /// The callback is how the window fills its table. It must come once per frame, in
    /// order, with the same result that also reaches the summary - otherwise the row shown
    /// and the mean reported would come from different calculations.
    /// </summary>
    [Fact]
    public void LightSeriesRun_ReportsEveryFrameOnceInOrderWithTheResultThatIsCombined()
    {
        using var ws = new TempWorkspace();
        string dir = WriteSeries(ws, 5);

        var seen = new List<(int Index, int Total, string Name, double Rad)>();
        var run = LightSeriesRun.Execute(dir, "pp_light_*.fit", null, new StringWriter(),
            onFrame: (i, total, name, r) => seen.Add((i, total, name, r.RadPct)));

        Assert.Equal(5, seen.Count);
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, seen.Select(s => s.Index));
        Assert.All(seen, s => Assert.Equal(5, s.Total));
        Assert.Equal(run.Result.Names, seen.Select(s => s.Name).ToArray());
        Assert.Equal(run.Result.Frames.Select(f => f.RadPct), seen.Select(s => s.Rad));
    }

    /// <summary>
    /// The text written to the TextWriter is identical to the console's, so the window can
    /// save a series' report the same way as the structure one.
    /// </summary>
    [Fact]
    public void LightSeriesRun_WritesTheFullConsoleReport()
    {
        using var ws = new TempWorkspace();
        string dir = WriteSeries(ws, 5);

        var w = new StringWriter();
        var run = LightSeriesRun.Execute(dir, "pp_light_*.fit", null, w);
        string text = w.ToString();

        Assert.Contains("=== TEST END-TO-END (SERIE):", text);
        Assert.Contains("SINTEZA PE SERIE", text);
        Assert.Contains("VERDICT: " + run.Result.Verdict, text);
        // one row for each frame
        foreach (string name in run.Result.Names) Assert.Contains(name, text);
    }

    /// <summary>
    /// Cancellation is checked BETWEEN frames. A token already cancelled must stop before
    /// the first frame, not after all of them - otherwise closing the window would wait two
    /// minutes.
    /// </summary>
    [Fact]
    public void LightSeriesRun_StopsWhenCancelled()
    {
        using var ws = new TempWorkspace();
        string dir = WriteSeries(ws, 6);

        using var cts = new CancellationTokenSource();
        int done = 0;

        Assert.Throws<OperationCanceledException>(() =>
            LightSeriesRun.Execute(dir, "pp_light_*.fit", null, new StringWriter(),
                onFrame: (i, total, name, r) => { done++; cts.Cancel(); },
                ct: cts.Token));

        Assert.Equal(1, done);   // the first frame finished, the second never started
    }

    [Fact]
    public void LightSeriesRun_OnAnEmptyFolder_SaysWhichPatternFoundNothing()
    {
        using var ws = new TempWorkspace();

        var e = Assert.Throws<Exception>(() =>
            LightSeriesRun.Execute(ws.Dir, "pp_light_*.fit", null, new StringWriter()));

        Assert.Contains("pp_light_*.fit", e.Message);
    }

    // ---------------------------------------------------------------- the shared header

    /// <summary>
    /// The two applications write the same header, because they write it from the same
    /// place. If someone rewrote it in WPF, the saved report could stay on an old version.
    /// </summary>
    // ---------------------------------------------------------------- histogram equalisation

    static string WriteCfaFlat(TempWorkspace ws)
        => ws.WriteFile("raw.fit", FitsTestData.BuildFits(Size, Size,
               FitsTestData.WithBayerMosaic(FitsTestData.Radial(Size, Size, center: 34000)),
               extraCards: FitsTestData.BayerCards("RGGB")));

    [Fact]
    public void HistEqRun_ProducesTheSameRastersAsTheAnalyzer()
    {
        using var ws = new TempWorkspace();
        string path = WriteCfaFlat(ws);

        LoadedFrame f = ImageLoader.Load(path);
        var direct = HistEqAnalyzer.Analyze(f.Image, f.Cfa);
        var run = HistEqRun.Execute(path, new StringWriter());

        Assert.Equal(direct.CompositeRgb, run.Result.CompositeRgb);
        Assert.Equal(direct.Mosaic, run.Result.Mosaic);
        Assert.Equal(direct.Channels.Select(c => c.Label), run.Result.Channels.Select(c => c.Label));
        for (int i = 0; i < 4; i++)
            Assert.Equal(direct.Channels[i].Equalised, run.Result.Channels[i].Equalised);
    }

    [Fact]
    public void HistEqRun_ReturnsTheSamePngBytesAsEncodingTheComposite()
    {
        using var ws = new TempWorkspace();
        var run = HistEqRun.Execute(WriteCfaFlat(ws), new StringWriter());

        Assert.Equal(HistEqRun.EncodeComposite(run.Result), run.Png);
    }

    [Fact]
    public void HistEqRun_ReturnsADecodablePngOfTheDisplaySize()
    {
        using var ws = new TempWorkspace();
        var run = HistEqRun.Execute(WriteCfaFlat(ws), new StringWriter());

        var d = PngDecoder.Decode(run.Png);

        Assert.Equal(run.Result.W, d.Width);
        Assert.Equal(run.Result.H, d.Height);
        Assert.All(d.Chunks, c => Assert.True(c.CrcValid));
    }

    [Fact]
    public void HistEqRun_WritesTheReportText()
    {
        using var ws = new TempWorkspace();
        string path = WriteCfaFlat(ws);
        var expected = new StringWriter();
        var actual = new StringWriter();

        var run = HistEqRun.Execute(path, actual);
        HistEqReport.Write(run.Result, path, expected);

        Assert.Equal(expected.ToString(), actual.ToString());
        Assert.Contains("EGALIZARE DE HISTOGRAMA", actual.ToString());
    }

    /// <summary>
    /// The per-view encoders the window uses: greys, all of the composite's size.
    /// </summary>
    [Fact]
    public void HistEqRun_EncodesEveryViewAtTheSameSize()
    {
        using var ws = new TempWorkspace();
        var run = HistEqRun.Execute(WriteCfaFlat(ws), new StringWriter());

        var mosaic = PngDecoder.Decode(HistEqRun.EncodeMosaic(run.Result)!);
        Assert.Equal((run.Result.W, run.Result.H), (mosaic.Width, mosaic.Height));
        for (int i = 0; i < 4; i++)
        {
            var d = PngDecoder.Decode(HistEqRun.EncodeChannel(run.Result, i));
            Assert.Equal((run.Result.W, run.Result.H), (d.Width, d.Height));
            var (r, g, b) = d.At(3, 3);
            Assert.Equal(r, g); Assert.Equal(g, b);
        }
    }

    [Fact]
    public void HistEqRun_OnAMonoFrame_HasNoMosaicToEncode()
    {
        using var ws = new TempWorkspace();
        var run = HistEqRun.Execute(WriteFlat(ws), new StringWriter());

        Assert.Null(HistEqRun.EncodeMosaic(run.Result));
        Assert.Single(run.Result.Channels);
    }

    /// <summary>
    /// With --rainbow the console writes a one-layer view in Siril's false colours: the
    /// Mosaic on a CFA frame, the channel on mono, SHRUNK FIRST and coloured after, so every
    /// pixel of the file is a level of the table applied to the shrunk grey raster. The report
    /// says so, and the plain picture is not what is returned.
    /// </summary>
    [Fact]
    public void R104_TheConsoleColoursTheShrunkMosaicOnCfaAndTheChannelOnMono()
    {
        using var ws = new TempWorkspace();
        var text = new StringWriter();

        var cfa = HistEqRun.Execute(WriteCfaFlat(ws), text, rainbow: true);
        var mono = HistEqRun.Execute(WriteFlat(ws), new StringWriter(), rainbow: true);

        Assert.Equal(HistEqRun.EncodeRainbow(cfa.Result), cfa.Png);
        Assert.NotEqual(HistEqRun.EncodeComposite(cfa.Result), cfa.Png);
        Assert.Contains("culori false", text.ToString());
        Assert.Contains("magenta", text.ToString());

        var d = PngDecoder.Decode(cfa.Png);
        Assert.Equal((cfa.Result.W, cfa.Result.H), (d.Width, d.Height));
        byte[] expected = RainbowLut.Colour(cfa.Result.Mosaic);
        foreach (var (x, y) in new[] { (0, 0), (3, 3), (cfa.Result.W / 2, cfa.Result.H / 2), (cfa.Result.W - 1, cfa.Result.H - 1) })
        {
            int i = (y * cfa.Result.W + x) * 3;
            Assert.Equal((expected[i], expected[i + 1], expected[i + 2]), d.At(x, y));
        }

        var m = PngDecoder.Decode(mono.Png);
        byte[] expectedMono = RainbowLut.Colour(mono.Result.Channels[0].Equalised);
        int c = (mono.Result.H / 2 * mono.Result.W + mono.Result.W / 2) * 3;
        Assert.Equal((expectedMono[c], expectedMono[c + 1], expectedMono[c + 2]), m.At(mono.Result.W / 2, mono.Result.H / 2));
    }

    [Fact]
    public void TheBannerIsTheFirstTwoLinesAndCarriesVersionAndCopyright()
    {
        var w = new StringWriter();
        RunReport.WriteBanner(w);

        var lines = w.ToString().Replace("\r\n", "\n").Split('\n');

        Assert.Equal(VersionInfo.Banner, lines[0]);
        Assert.Equal(VersionInfo.Copyright, lines[1]);
    }

    [Fact]
    public void TheMapSavedLineNamesThePath()
    {
        var w = new StringWriter();
        RunReport.WriteMapSaved(@"C:\a\harta.png", w);

        Assert.Contains(@"harta salvata: C:\a\harta.png", w.ToString());
    }
}
