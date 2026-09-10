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

namespace FlatCheck.Wpf.Tests;

/// <summary>
/// Tabul de expunere. Comportamentul cerut e simplu - alegi un fisier, se calculeaza;
/// comuti "Sky", se recalculeaza - dar al doilea e si cel usor de uitat, si atunci un
/// sky flat corect expus la 22 % ar fi raportat ca subexpus.
/// </summary>
public class ExposureTabTests
{
    static ExposureTabViewModel Attached(SessionFixture f)
    {
        var vm = new ExposureTabViewModel();
        vm.Attach(f.Session, CancellationToken.None);
        return vm;
    }

    // ------------------------------------------------------------------ lista

    /// <summary>Cerinta 8: lista vine din flats/, si contine doar raw de aparat.</summary>
    [Fact]
    public void TheListComesFromFlatsAndHoldsOnlyCameraRaws()
    {
        using var f = new SessionFixture().WithFlatMaster()
            .WithRawFlats("DSC_0001.NEF", "DSC_0002.NEF", "note.txt", "convertit.fit");

        var vm = Attached(f);

        Assert.Equal(2, vm.Files.Count);
        Assert.All(vm.Files, p => Assert.True(RawReader.IsRawPath(p)));
    }

    /// <summary>
    /// Fara raw-uri, tabul explica de ce modul cere un cadru brut - stivuirea
    /// normalizeaza exact nivelul care se masoara aici.
    /// </summary>
    [Fact]
    public void WithoutRawFlatsItExplainsWhyAMasterWouldNotDo()
    {
        using var f = new SessionFixture().WithFlatMaster().WithFlats();

        var vm = Attached(f);

        Assert.True(vm.HasError);
        Assert.Contains("BRUT", vm.ErrorText);
        Assert.Contains("normalizeaza", vm.ErrorText);
        Assert.Contains(".nef", vm.ErrorText);
    }

    [Fact]
    public void NothingIsSelectedUntilTheUserPicks()
    {
        using var f = new SessionFixture().WithFlatMaster().WithRawFlats("a.NEF");

        var vm = Attached(f);

        Assert.Null(vm.Selected);
        Assert.False(vm.HasResult);
    }

    // ------------------------------------------------------------------ calcul

    [Fact]
    public async Task PickingAFileMeasuresIt()
    {
        using var f = new SessionFixture().WithFlatMaster();
        string path = f.WriteAnalysableRawFlat("flat.fit");

        var vm = Attached(f);
        vm.Selected = path;
        await vm.RecomputeAsync();

        Assert.True(vm.HasResult);
        Assert.NotEmpty(vm.Centre);
        Assert.NotEmpty(vm.Verdict);
        Assert.True(vm.IsRawFlat);
    }

    /// <summary>
    /// CERINTA 8: comutatorul "Sky" schimba pragurile, deci si verdictul, pe acelasi
    /// fisier. Nivelul masurat ramane identic - se schimba doar cum se citeste.
    /// </summary>
    [Fact]
    public async Task TheSkySwitchChangesTheVerdictWithoutChangingTheMeasurement()
    {
        using var f = new SessionFixture().WithFlatMaster();
        string path = f.WriteAnalysableRawFlat("flat.fit");

        var vm = Attached(f);
        vm.Selected = path;
        await vm.RecomputeAsync();
        string panelVerdict = vm.Verdict, level = vm.Centre;

        vm.Sky = true;
        await vm.RecomputeAsync();

        Assert.Equal(level, vm.Centre);              // aceeasi masuratoare
        Assert.NotEqual(panelVerdict, vm.Verdict);   // alta citire
    }

    /// <summary>Sfatul pentru sky flat spune explicit sa NU se forteze mai sus.</summary>
    [Fact]
    public async Task TheSkyAdviceWarnsAgainstPushingHigher()
    {
        using var f = new SessionFixture().WithFlatMaster();
        string path = f.WriteAnalysableRawFlat("flat.fit", centre: 20000);

        var vm = Attached(f);
        vm.Sky = true;
        vm.Selected = path;
        await vm.RecomputeAsync();

        Assert.Contains("sky flat", vm.Advice, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PickingAnotherFileRecomputes()
    {
        using var f = new SessionFixture().WithFlatMaster();
        string bright = f.WriteAnalysableRawFlat("bright.fit", centre: 34000);
        string dim = f.WriteAnalysableRawFlat("dim.fit", centre: 12000);

        var vm = Attached(f);
        vm.Selected = bright;
        await vm.RecomputeAsync();
        string first = vm.Centre;

        vm.Selected = dim;
        await vm.RecomputeAsync();

        Assert.NotEqual(first, vm.Centre);
    }

    /// <summary>
    /// Gate-ul de sanitate: un fisier deja calibrat sau debayerizat nu primeste verdict,
    /// ci explicatia - inclusiv cauza tipica, "am deschis NEF-ul in Siril si am dat Save".
    /// </summary>
    [Fact]
    public async Task ADebayeredFileGetsAnExplanationInsteadOfAVerdict()
    {
        using var f = new SessionFixture().WithFlatMaster().WithFlats();
        string path = Path.Combine(f.FlatsDir, "debayerizat.fit");
        File.WriteAllBytes(path, FlatCheck.Tests.FitsTestData.BuildFits(
            128, 128, FlatCheck.Tests.FitsTestData.Radial(128, 128, center: 34000)));

        var vm = Attached(f);
        vm.Selected = path;
        await vm.RecomputeAsync();

        Assert.True(vm.HasResult);       // a rulat
        Assert.False(vm.IsRawFlat);      // dar fisierul nu e ce cere modul
        Assert.Equal(Stats.NotApplicable, vm.Verdict);
        Assert.Contains("DEBAYERIZAT", vm.Advice);
        Assert.Contains("Conversion", vm.Advice);
    }

    [Fact]
    public async Task ACorruptFileBecomesAnExplanationNotACrash()
    {
        using var f = new SessionFixture().WithFlatMaster().WithFlats();
        string path = Path.Combine(f.FlatsDir, "stricat.NEF");
        File.WriteAllText(path, "nu e un raw");

        var vm = Attached(f);
        vm.Selected = path;
        await vm.RecomputeAsync();

        Assert.False(vm.HasResult);
        Assert.True(vm.HasError);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task TheReportIsTheConsoleText()
    {
        using var f = new SessionFixture().WithFlatMaster();
        string path = f.WriteAnalysableRawFlat("flat.fit");

        var vm = Attached(f);
        vm.Selected = path;
        await vm.RecomputeAsync();

        var expected = new StringWriter();
        RunReport.WriteBanner(expected);
        ExposureRun.Execute(path, null, null, false, expected);

        Assert.Equal(expected.ToString(), vm.Report);
    }

    // ------------------------------------------------------------------ histograme

    /// <summary>
    /// Un rand pe pozitie Bayer, cu ambele curbe gata normalizate. Fiecare curba e scalata
    /// la propriul varf, deci maximul ei e 1 - altfel fereastra centrala, care are 4 % din
    /// pixelii canalului, ar fi o linie plata, si tocmai ea decide.
    /// </summary>
    [Fact]
    public async Task PickingAFileFillsOneHistogramRowPerChannel()
    {
        using var f = new SessionFixture().WithFlatMaster();
        string path = f.WriteAnalysableRawFlat("flat.fit");

        var vm = Attached(f);
        vm.Selected = path;
        await vm.RecomputeAsync();

        Assert.Equal(4, vm.Histograms.Count);
        Assert.Single(vm.Histograms.Where(r => r.IsBrightest));
        foreach (var row in vm.Histograms)
        {
            Assert.Equal(200, row.Whole.Length);
            Assert.Equal(200, row.CentreCurve.Length);
            Assert.Equal(1.0, row.Whole.Max(), 9);
            Assert.Equal(1.0, row.CentreCurve.Max(), 9);
            Assert.NotNull(row.Colour);
        }
    }

    /// <summary>
    /// Without a BAYERPAT card the rows stay positions, and the tab says so rather than
    /// picking an order - it differs between cameras.
    /// </summary>
    [Fact]
    public async Task WithoutABayerPatternTheRowsAreLabelledByPosition()
    {
        using var f = new SessionFixture().WithFlatMaster();
        string path = f.WriteAnalysableRawFlat("flat.fit");

        var vm = Attached(f);
        vm.Selected = path;
        await vm.RecomputeAsync();

        Assert.Equal(new[] { "CFA[0,0]", "CFA[0,1]", "CFA[1,0]", "CFA[1,1]" },
                     vm.Histograms.Select(r => r.Label));
        Assert.All(vm.Histograms, r => Assert.Equal("", r.Position));
        Assert.Contains("Nu stiu", vm.ChannelOrder);
    }

    /// <summary>...and with it, they get their colours, read off the file and named as such.</summary>
    [Fact]
    public async Task WithABayerPatternTheRowsAreNamedByColour()
    {
        using var f = new SessionFixture().WithFlatMaster();
        string path = f.WriteAnalysableRawFlat("flat.fit", bayerPat: "RGGB");

        var vm = Attached(f);
        vm.Selected = path;
        await vm.RecomputeAsync();

        Assert.Equal(new[] { "R", "G1", "G2", "B" }, vm.Histograms.Select(r => r.Label));
        Assert.Equal("CFA[0,1]", vm.Histograms[1].Position);
        Assert.Equal("G1", vm.Histograms.First(r => r.IsBrightest).Label);
        Assert.Contains("RGGB", vm.ChannelOrder);
        Assert.Contains("BAYERPAT", vm.ChannelOrder);
    }

    /// <summary>
    /// The shaded band is the one that votes, so flipping "Sky" has to move it on every
    /// row. On the sky ladder there is no low shoulder, which the drawing must skip
    /// rather than paint at zero width - hence NaN.
    /// </summary>
    [Fact]
    public async Task TheSkySwitchMovesTheBandOnEveryRow()
    {
        using var f = new SessionFixture().WithFlatMaster();
        string path = f.WriteAnalysableRawFlat("flat.fit");

        var vm = Attached(f);
        vm.Selected = path;
        await vm.RecomputeAsync();

        Assert.All(vm.Histograms, r =>
        {
            Assert.Equal(Th.ExpoLo, r.BandLo, 9);
            Assert.Equal(Th.ExpoHi, r.BandHi, 9);
            Assert.Equal(Th.ExpoTooLow, r.TooLow, 9);
        });

        vm.Sky = true;
        await vm.RecomputeAsync();

        Assert.All(vm.Histograms, r =>
        {
            Assert.Equal(Th.ExpoSkyLo, r.BandLo, 9);
            Assert.Equal(Th.ExpoSkyHi, r.BandHi, 9);
            Assert.True(double.IsNaN(r.TooLow), "umarul de jos nu exista pe scara de cer");
            Assert.Contains("sky", r.BandLabel);
        });
    }

    /// <summary>The marker is the decision figure, not a second measurement of it.</summary>
    [Fact]
    public async Task TheMarkerOnTheBrightestRowIsTheVerdictFigure()
    {
        using var f = new SessionFixture().WithFlatMaster();
        string path = f.WriteAnalysableRawFlat("flat.fit");

        var vm = Attached(f);
        vm.Selected = path;
        await vm.RecomputeAsync();

        var bright = vm.Histograms.First(r => r.IsBrightest);
        Assert.Equal(vm.Centre, $"{bright.Marker:F1} %");
        Assert.Equal(bright.Centre, vm.Centre);
    }

    [Fact]
    public async Task TheReadingNamesTheChannelThatDecides()
    {
        using var f = new SessionFixture().WithFlatMaster();
        string path = f.WriteAnalysableRawFlat("flat.fit", bayerPat: "RGGB");

        var vm = Attached(f);
        vm.Selected = path;
        await vm.RecomputeAsync();

        Assert.NotEmpty(vm.Reading);
        Assert.Contains("G1", vm.Reading);
    }

    /// <summary>A debayered file has no mosaic left, so it gets one unnamed row.</summary>
    [Fact]
    public async Task ADebayeredFileGetsOneMonoRow()
    {
        using var f = new SessionFixture().WithFlatMaster().WithFlats();
        string path = Path.Combine(f.FlatsDir, "debayerizat.fit");
        File.WriteAllBytes(path, FlatCheck.Tests.FitsTestData.BuildFits(
            128, 128, FlatCheck.Tests.FitsTestData.Radial(128, 128, center: 34000)));

        var vm = Attached(f);
        vm.Selected = path;
        await vm.RecomputeAsync();

        var only = Assert.Single(vm.Histograms);
        Assert.Equal("mono", only.Label);
        Assert.False(only.IsBrightest);
        Assert.Contains("nu e un flat brut", vm.Reading);
    }

    // ------------------------------------------------------------------ previzualizarea aparatului

    /// <summary>
    /// Un rezultat cu previzualizare, fara niciun raw adevarat: JPEG-ul se face in test
    /// cu encoderul ImageSharp, se decodeaza cu PreviewReader si intra in analizor ca
    /// intr-o rulare reala. Previzualizarea e mult mai luminoasa decat raw-ul, ca pe un
    /// aparat adevarat.
    /// </summary>
    static ExposureResult ResultWithPreview(bool cfa = true)
    {
        Img img = cfa
            ? FlatCheck.Tests.TestImages.FlatCfa(128, 128, 550)
            : FlatCheck.Tests.TestImages.From(128, 128, FlatCheck.Tests.FitsTestData.Radial(128, 128, center: 550));
        PreviewImage preview = PreviewReader.Decode(FlatCheck.Tests.TestImages.JpegOf(160, 120, (x, y) =>
        {
            // centru mai luminos decat colturile, ca sa se vada ca tabelul ia CENTRUL
            double d = Math.Sqrt((x - 80) * (x - 80) + (y - 60) * (y - 60)) / 100.0;
            byte v = (byte)(230 - 60 * d);
            return ((byte)(v - 40), v, (byte)Math.Min(255, v + 15));
        }));
        return ExposureAnalyzer.Analyze(img, 1000, null, false, null,
                                        cfa ? CfaPattern.Parse("RGGB", "BAYERPAT") : null, preview);
    }

    static ExposureTabViewModel Loaded(ExposureResult r)
    {
        var vm = new ExposureTabViewModel();
        vm.Populate(r);
        return vm;
    }

    /// <summary>Un FITS n-are previzualizare: comutatorul e stins, vederea e pe brute, si se spune de ce.</summary>
    [Fact]
    public async Task WithoutAPreviewTheSwitchIsDisabled()
    {
        using var f = new SessionFixture().WithFlatMaster();
        string path = f.WriteAnalysableRawFlat("flat.fit");

        var vm = Attached(f);
        vm.Selected = path;
        await vm.RecomputeAsync();

        Assert.False(vm.Source.IsEnabled);
        Assert.False(vm.Source.IsPreviewSelected);
        Assert.Empty(vm.Comparison);
        Assert.False(vm.HasComparison);
        Assert.Contains("nu poarta o previzualizare", vm.SourceNote);
        Assert.Contains("Banda verde", vm.Caption);
    }

    [Fact]
    public void WithAPreviewTheSwitchIsEnabledAndStartsOnRaw()
    {
        var vm = Loaded(ResultWithPreview());

        Assert.True(vm.Source.IsEnabled);
        Assert.False(vm.Source.IsPreviewSelected);
        Assert.Equal(4, vm.Histograms.Count);          // randurile raw
        Assert.Equal("", vm.SourceNote);               // pe brute, cu previzualizare disponibila: nimic de explicat
        Assert.True(vm.HasComparison);
    }

    /// <summary>
    /// Comutarea reface randurile din rezultatul tinut, nu reruleaza nimic: cifrele
    /// verdictului raman identice si tabul nu trece prin starea de asteptare.
    /// </summary>
    [Fact]
    public void TheSwitchDoesNotRerunTheAnalysis()
    {
        var vm = Loaded(ResultWithPreview());
        string centre = vm.Centre, verdict = vm.Verdict;

        vm.Source.Preview.IsSelected = true;

        Assert.True(vm.Source.IsPreviewSelected);
        Assert.Equal(3, vm.Histograms.Count);
        Assert.Equal(centre, vm.Centre);
        Assert.Equal(verdict, vm.Verdict);
        Assert.False(vm.IsBusy);
        Assert.Contains("ecranul aparatului arata", vm.SourceNote);
        Assert.Contains("FARA banda tinta", vm.Caption);

        vm.Source.Raw.IsSelected = true;
        Assert.Equal(4, vm.Histograms.Count);
        Assert.Equal("", vm.SourceNote);
    }

    /// <summary>
    /// Decizia cea mai importanta a functiei: pe previzualizare nu exista benzi, nota,
    /// sau canal "care decide". Toate cele patru praguri sunt NaN, deci desenul nu
    /// traseaza nicio zona - fara niciun steag in HistogramCanvas.
    /// </summary>
    [Fact]
    public void ThePreviewRowsCarryNoBands()
    {
        var vm = Loaded(ResultWithPreview());
        vm.Source.Preview.IsSelected = true;

        Assert.Equal(new[] { "R", "G", "B" }, vm.Histograms.Select(r => r.Label));
        Assert.All(vm.Histograms, r =>
        {
            Assert.True(double.IsNaN(r.BandLo));
            Assert.True(double.IsNaN(r.BandHi));
            Assert.True(double.IsNaN(r.TooLow));
            Assert.True(double.IsNaN(r.TooHigh));
            Assert.Null(r.BandLabel);
            Assert.False(r.IsBrightest);
            Assert.Equal("", r.Position);
            Assert.Equal(1.0, r.Whole.Max(), 9);
        });
    }

    /// <summary>Tabelul compara centru cu centru, nu mediana intregului cadru.</summary>
    [Fact]
    public void TheComparisonPairsCentreWithCentre()
    {
        ExposureResult r = ResultWithPreview();
        var vm = Loaded(r);

        Assert.Equal(3, vm.Comparison.Count);
        Assert.Equal(new[] { "R", "G (media G1, G2)", "B" }, vm.Comparison.Select(c => c.Channel));

        ChannelLevels rawR = r.Channels.Single(c => c.Colour == 'R');
        ChannelLevels prevR = r.Preview.Channels.Single(c => c.Colour == 'R');
        Assert.Equal($"{rawR.CentreMedianPct:F1} %", vm.Comparison[0].Raw);
        Assert.Equal($"{prevR.CentreMedianPct:F1} %", vm.Comparison[0].Screen);
        Assert.NotEqual($"{prevR.MedianPct:F1} %", vm.Comparison[0].Screen);   // centrul e mai luminos decat cadrul
        Assert.StartsWith("+", vm.Comparison[0].Gap);
    }

    /// <summary>Un raw judecat mono (un singur canal, fara culoare): o singura pereche, fara exceptie.</summary>
    [Fact]
    public void AMonoRawGetsOneComparisonRow()
    {
        var vm = Loaded(ResultWithPreview(cfa: false));

        var row = Assert.Single(vm.Comparison);
        Assert.Contains("cel mai luminos", row.Channel);
        vm.Source.Preview.IsSelected = true;
        Assert.Equal(3, vm.Histograms.Count);
    }

    [Fact]
    public void ResetKeepsTheChosenSourceButDisablesIt()
    {
        var vm = Loaded(ResultWithPreview());
        vm.Source.Preview.IsSelected = true;

        vm.Reset();

        Assert.Empty(vm.Histograms);
        Assert.Empty(vm.Comparison);
        Assert.Equal("", vm.SourceNote);
        Assert.False(vm.Source.IsEnabled);
        // Stingerea trage alegerea inapoi pe brute: n-are ce arata pe previzualizare.
        Assert.False(vm.Source.IsPreviewSelected);
    }

    /// <summary>Un fisier nou fara previzualizare, dupa unul cu: comutatorul se stinge si vederea revine pe brute.</summary>
    [Fact]
    public void ANewFileWithoutAPreviewFallsBackToRaw()
    {
        var vm = Loaded(ResultWithPreview());
        vm.Source.Preview.IsSelected = true;

        vm.Populate(ExposureAnalyzer.Analyze(FlatCheck.Tests.TestImages.FlatCfa(128, 128, 550), 1000, null, false));

        Assert.False(vm.Source.IsEnabled);
        Assert.False(vm.Source.IsPreviewSelected);
        Assert.Equal(4, vm.Histograms.Count);
        Assert.Contains("nu poarta o previzualizare", vm.SourceNote);
    }

    [Fact]
    public async Task ResetClearsTheHistogramsButKeepsTheGuideOpen()
    {
        using var f = new SessionFixture().WithFlatMaster();
        string path = f.WriteAnalysableRawFlat("flat.fit");
        var vm = Attached(f);
        vm.Selected = path;
        await vm.RecomputeAsync();
        vm.HistogramGuide.IsExpanded = true;

        vm.Reset();

        Assert.Empty(vm.Histograms);
        Assert.Empty(vm.Reading);
        Assert.Empty(vm.ChannelOrder);
        // Preferinta de vizualizare supravietuieste, ca la ghidul hartii.
        Assert.True(vm.HistogramGuide.IsExpanded);
    }

    [Fact]
    public async Task ResetClearsEverything()
    {
        using var f = new SessionFixture().WithFlatMaster();
        string path = f.WriteAnalysableRawFlat("flat.fit");
        var vm = Attached(f);
        vm.Selected = path;
        await vm.RecomputeAsync();

        vm.Reset();

        Assert.Empty(vm.Files);
        Assert.Null(vm.Selected);
        Assert.False(vm.HasResult);
        Assert.Empty(vm.Verdict);
    }
}
