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

using System.Threading;

namespace FlatCheck.Tests;

/// <summary>
/// Stratul Runs/ e singurul lucru pe care il impart aplicatia de consola si cea WPF.
/// Toata valoarea lui e in promisiunea "amandoua obtin exact aceleasi cifre si exact
/// acelasi text", deci exact aia se verifica aici - nu ca lantul ruleaza, ci ca ruland
/// prin el iese acelasi lucru ca ruland pe langa el.
///
/// De ce nu ajung testele golden: alea verifica ce scrie CONSOLA. Daca maine fereastra
/// ar cheama analizorul direct, in loc de Run, golden-urile ar ramane verzi si cele doua
/// aplicatii ar raporta cifre diferite pe acelasi fisier. Testele de aici sunt cele care
/// s-ar face rosii.
/// </summary>
public class RunsTests
{
    const int Size = 256;

    static string WriteFlat(TempWorkspace ws, string name = "flat.fit")
        => ws.WriteFile(name, FitsTestData.BuildFits(Size, Size, FitsTestData.Radial(Size, Size)));

    // ---------------------------------------------------------------- structura

    /// <summary>
    /// Cifrele care ies prin Run sunt cele ale analizorului, nu o a doua socoteala.
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
    /// Textul care ajunge in "Rezultat testare.txt" trebuie sa fie acelasi cu al
    /// consolei. Cerinta e literala, deci testul e literal.
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
    /// Harta se intoarce ca octeti, si sunt exact octetii pe care i-ar fi scris pe disc
    /// varianta de consola - altfel fereastra ar arata alta imagine decat cea salvata.
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

    // ---------------------------------------------------------------- raport A/B

    [Fact]
    public void RatioRun_ProducesTheSameNumbersAsTheAnalyzer()
    {
        using var ws = new TempWorkspace();
        string a = WriteFlat(ws, "a.fit");
        string b = ws.WriteFile("b.fit",
            FitsTestData.BuildFits(Size, Size,
                FitsTestData.WithLinearGradient(FitsTestData.Radial(Size, Size), Size, 0.12)));

        var direct = RatioAnalyzer.Analyze(ImageLoader.LoadImage(a), ImageLoader.LoadImage(b));
        var run = RatioRun.Execute(a, b, new StringWriter());

        Assert.Equal(direct.WorldPtpPct, run.Result.WorldPtpPct, 12);
        Assert.Equal(direct.WorldErrPct, run.Result.WorldErrPct, 12);
        Assert.Equal(direct.Sensor.CornerSpreadPct, run.Result.Sensor.CornerSpreadPct, 12);
        Assert.Equal(direct.Verdict, run.Result.Verdict);

        // Doua harti: reziduul partii fixe pe aparat, si raportul A/B.
        Assert.NotNull(run.Png);
        Assert.NotNull(run.SecondaryPng);
        Assert.NotEqual(run.Png, run.SecondaryPng);
    }

    /// <summary>
    /// Provenienta vine din headerele FITS, nu din nume: e singurul loc din raport in
    /// care se vede daca cele doua stive chiar pot fi din doua orientari diferite.
    /// </summary>
    [Fact]
    public void RatioRun_ReadsTheStackProvenanceFromTheHeaders()
    {
        using var ws = new TempWorkspace();
        string a = WriteFlat(ws, "a.fit");
        string b = WriteFlat(ws, "b.fit");

        var run = RatioRun.Execute(a, b, new StringWriter());

        // Fixture-urile n-au STACKCNT/DATE-OBS, iar lipsa lor nu e o eroare.
        Assert.Equal(0, run.Result.StackCountA);
        Assert.Null(run.Result.DateObsA);
        Assert.Contains("necunoscut", RatioReport.Provenance(run.Result.StackCountA, run.Result.DateObsA));
    }

    // ---------------------------------------------------------------- expunere

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
    /// Modul masoara un nivel, n-are ce desena. Fereastra se bazeaza pe null ca sa nu
    /// arate un panou de imagine gol la tabul de expunere.
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

    // ---------------------------------------------------------------- light, un cadru

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
    /// Refuzul e absenta unui verdict, nu un verdict prost: nu se produce nicio cifra si
    /// nicio harta. Fereastra se bazeaza pe asta ca sa nu afiseze un panou de rezultate
    /// pe jumatate populat.
    /// </summary>
    [Fact]
    public void LightRun_OnATooWideField_RefusesWithoutProducingNumbersOrAMap()
    {
        using var ws = new TempWorkspace();
        string path = ws.WriteFile("wide.fit", FitsTestData.BuildFits(
            Size, Size, FitsTestData.Radial(Size, Size),
            extraCards: FitsTestData.WcsCards(0.2)));   // 256x256, diag 362 px -> ~72 gr

        var run = LightRun.Execute(path, null, new StringWriter());

        Assert.True(run.Refused);
        Assert.Null(run.Result);
        Assert.Null(run.Png);
        Assert.Equal(FovLevel.TooWide, run.Suitability.Level);
    }

    /// <summary>Refuzul explica de ce, chiar daca nu da cifre.</summary>
    [Fact]
    public void LightRun_OnATooWideField_StillWritesAnExplanation()
    {
        using var ws = new TempWorkspace();
        string path = ws.WriteFile("wide.fit", FitsTestData.BuildFits(
            Size, Size, FitsTestData.Radial(Size, Size),
            extraCards: FitsTestData.WcsCards(0.2)));   // 256x256, diag 362 px -> ~72 gr

        var w = new StringWriter();
        LightRun.Execute(path, null, w);

        Assert.Contains("TESTUL NU SE APLICA", w.ToString());
    }

    /// <summary>--fov spune adevarul, nu forteaza: un camp larg declarat e tot refuzat.</summary>
    [Fact]
    public void LightRun_HonoursTheFovOverride()
    {
        using var ws = new TempWorkspace();
        string path = ws.WriteFile("light.fit",
            FitsTestData.BuildFits(Size, Size, FitsTestData.Radial(Size, Size)));

        Assert.False(LightRun.Execute(path, 10.0, new StringWriter()).Refused);
        Assert.True(LightRun.Execute(path, 70.0, new StringWriter()).Refused);
    }

    // ---------------------------------------------------------------- light, serie

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
    /// Callback-ul e cum isi umple fereastra tabelul. Trebuie sa vina o data per cadru,
    /// in ordine, cu acelasi rezultat care ajunge si in sinteza - altfel randul afisat
    /// si media raportata ar fi din calcule diferite.
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
    /// Textul scris in TextWriter e identic cu cel al consolei, deci fereastra poate
    /// salva raportul unei serii la fel ca pe cel de structura.
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
        // cate un rand pentru fiecare cadru
        foreach (string name in run.Result.Names) Assert.Contains(name, text);
    }

    /// <summary>
    /// Anularea se verifica INTRE cadre. Un token deja anulat trebuie sa opreasca inainte
    /// de primul cadru, nu dupa toate - altfel inchiderea ferestrei ar astepta doua minute.
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

        Assert.Equal(1, done);   // primul cadru s-a terminat, al doilea n-a mai pornit
    }

    [Fact]
    public void LightSeriesRun_OnAnEmptyFolder_SaysWhichPatternFoundNothing()
    {
        using var ws = new TempWorkspace();

        var e = Assert.Throws<Exception>(() =>
            LightSeriesRun.Execute(ws.Dir, "pp_light_*.fit", null, new StringWriter()));

        Assert.Contains("pp_light_*.fit", e.Message);
    }

    // ---------------------------------------------------------------- antetul comun

    /// <summary>
    /// Cele doua aplicatii scriu acelasi antet, fiindca il scriu din acelasi loc. Daca
    /// cineva l-ar rescrie in WPF, raportul salvat ar putea ramane pe o versiune veche.
    /// </summary>
    // ---------------------------------------------------------------- egalizare de histograma

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
        Assert.Null(run.SecondaryPng);
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

    /// <summary>The per-view encoders the window uses: greys, all of the composite's size.</summary>
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
