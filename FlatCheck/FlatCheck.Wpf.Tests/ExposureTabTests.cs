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
/// The exposure tab. The behaviour asked for is simple - pick a file, it computes; flip
/// "Sky", it computes again - but the second is also the easy one to forget, and then a
/// sky flat correctly exposed at 22 % would be reported as underexposed.
/// </summary>
public class ExposureTabTests
{
    static ExposureTabViewModel Attached(SessionFixture f)
    {
        var vm = new ExposureTabViewModel();
        vm.Attach(f.Session, CancellationToken.None);
        return vm;
    }

    // ------------------------------------------------------------------ list

    /// <summary>R-084: the list comes from flats/, and holds only camera raws.</summary>
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
    /// Without raws, the tab explains why the mode asks for a raw frame - stacking normalises
    /// exactly the level measured here.
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

    // ------------------------------------------------------------------ computation

    /// <summary>
    /// A pick made while another heavy run holds the gate (another tab's click, say) waits for it
    /// instead of decoding a 24 MP raw beside it (R-081, ADR 0061); the waiting screen says so,
    /// and the measurement follows once the gate opens.
    /// </summary>
    [Fact]
    public async Task R084_APickDuringAHeavyRunWaitsForTheGate()
    {
        using var f = new SessionFixture().WithFlatMaster();
        string path = f.WriteAnalysableRawFlat("flat.fit");
        var gate = new HeavyRunGate();
        IDisposable other = await gate.EnterAsync(CancellationToken.None);
        var vm = new ExposureTabViewModel();
        vm.Attach(f.Session, CancellationToken.None, gate);

        vm.Selected = path;
        Task run = vm.Pending;

        Assert.NotSame(run, await Task.WhenAny(run, Task.Delay(1000)));
        Assert.True(vm.IsBusy);
        Assert.Equal(SessionMessages.WaitingForRunningAnalysis(), vm.BusyMessage);
        Assert.False(vm.HasResult);

        other.Dispose();
        await run;

        Assert.True(vm.HasResult);
        Assert.False(vm.IsBusy);
        Assert.False(gate.IsHeld);
    }

    [Fact]
    public async Task PickingAFileMeasuresIt()
    {
        using var f = new SessionFixture().WithFlatMaster();
        string path = f.WriteAnalysableRawFlat("flat.fit");

        var vm = Attached(f);
        vm.Selected = path;
        await vm.Pending;

        Assert.True(vm.HasResult);
        Assert.NotEmpty(vm.Centre);
        Assert.NotEmpty(vm.Verdict);
        Assert.True(vm.IsRawFlat);
    }

    /// <summary>
    /// R-043: the "Sky" switch changes the thresholds, so the verdict too, on the same
    /// file. The measured level stays identical - only how it is read changes.
    /// </summary>
    [Fact]
    public async Task TheSkySwitchChangesTheVerdictWithoutChangingTheMeasurement()
    {
        using var f = new SessionFixture().WithFlatMaster();
        string path = f.WriteAnalysableRawFlat("flat.fit");

        var vm = Attached(f);
        vm.Selected = path;
        await vm.Pending;
        string panelVerdict = vm.Verdict, level = vm.Centre;

        vm.Sky = true;
        await vm.Pending;

        Assert.Equal(level, vm.Centre);              // the same measurement
        Assert.NotEqual(panelVerdict, vm.Verdict);   // another reading
    }

    /// <summary>The advice for a sky flat says explicitly NOT to push higher.</summary>
    [Fact]
    public async Task TheSkyAdviceWarnsAgainstPushingHigher()
    {
        using var f = new SessionFixture().WithFlatMaster();
        string path = f.WriteAnalysableRawFlat("flat.fit", centre: 20000);

        var vm = Attached(f);
        vm.Sky = true;
        vm.Selected = path;
        await vm.Pending;

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
        await vm.Pending;
        string first = vm.Centre;

        vm.Selected = dim;
        await vm.Pending;

        Assert.NotEqual(first, vm.Centre);
    }

    /// <summary>
    /// A pick's run is the one in Pending, so a test awaits that run and never starts a second
    /// one beside it: two runs of the same tab at once (the other tests attach no gate) let the
    /// first one's figures land after the second one's, which is how
    /// PickingAnotherFileRecomputes flaked on 2026-10-27 (ADR 0077). The Sky switch's run is
    /// kept the same way. The test holds the gate while it looks at the run, so "not yet
    /// landed" rests on no timing.
    /// </summary>
    [Fact]
    public async Task APickKeepsItsRunInPendingUntilItLands()
    {
        using var f = new SessionFixture().WithFlatMaster();
        string path = f.WriteAnalysableRawFlat("flat.fit");
        var gate = new HeavyRunGate();
        IDisposable held = await gate.EnterAsync(CancellationToken.None);
        var vm = new ExposureTabViewModel();
        vm.Attach(f.Session, CancellationToken.None, gate);
        Assert.True(vm.Pending.IsCompleted);     // nothing picked, nothing running

        vm.Selected = path;
        Task pick = vm.Pending;

        Assert.False(pick.IsCompleted);
        Assert.True(vm.IsBusy);
        held.Dispose();
        await pick;
        Assert.True(vm.HasResult);
        Assert.False(vm.IsBusy);

        vm.Sky = true;
        Task sky = vm.Pending;

        Assert.NotSame(pick, sky);
        await sky;
        Assert.True(vm.HasResult);
    }

    /// <summary>
    /// The sanity gate: a file already calibrated or debayered gets no verdict, but the
    /// explanation - including the typical cause, "I opened the NEF in Siril and pressed Save".
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
        await vm.Pending;

        Assert.True(vm.HasResult);       // it ran
        Assert.False(vm.IsRawFlat);      // but the file is not what the mode asks for
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
        await vm.Pending;

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
        await vm.Pending;

        var expected = new StringWriter();
        RunReport.WriteBanner(expected);
        ExposureRun.Execute(path, null, null, false, expected);

        Assert.Equal(expected.ToString(), vm.Report);
    }

    // ------------------------------------------------------------------ histograms

    /// <summary>
    /// One row per Bayer position, with both curves already normalised. Each curve is scaled
    /// to its own peak, so its maximum is 1 - otherwise the central window, which holds 4 % of
    /// the channel's pixels, would be a flat line, and it is the one that decides.
    /// </summary>
    [Fact]
    public async Task PickingAFileFillsOneHistogramRowPerChannel()
    {
        using var f = new SessionFixture().WithFlatMaster();
        string path = f.WriteAnalysableRawFlat("flat.fit");

        var vm = Attached(f);
        vm.Selected = path;
        await vm.Pending;

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
        await vm.Pending;

        Assert.Equal(new[] { "CFA[0,0]", "CFA[0,1]", "CFA[1,0]", "CFA[1,1]" },
                     vm.Histograms.Select(r => r.Label));
        Assert.All(vm.Histograms, r => Assert.Equal("", r.Position));
        Assert.Contains("Nu stiu", vm.ChannelOrder);
    }

    /// <summary>
    /// ...and with it, they get their colours, read off the file and named as such.
    /// </summary>
    [Fact]
    public async Task WithABayerPatternTheRowsAreNamedByColour()
    {
        using var f = new SessionFixture().WithFlatMaster();
        string path = f.WriteAnalysableRawFlat("flat.fit", bayerPat: "RGGB");

        var vm = Attached(f);
        vm.Selected = path;
        await vm.Pending;

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
        await vm.Pending;

        Assert.All(vm.Histograms, r =>
        {
            Assert.Equal(Th.ExpoLo, r.BandLo, 9);
            Assert.Equal(Th.ExpoHi, r.BandHi, 9);
            Assert.Equal(Th.ExpoTooLow, r.TooLow, 9);
        });

        vm.Sky = true;
        await vm.Pending;

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
        await vm.Pending;

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
        await vm.Pending;

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
        await vm.Pending;

        var only = Assert.Single(vm.Histograms);
        Assert.Equal("mono", only.Label);
        Assert.False(only.IsBrightest);
        Assert.Contains("nu e un flat brut", vm.Reading);
    }

    // ------------------------------------------------------------------ the camera's preview

    /// <summary>
    /// A result with a preview, without any real raw: the JPEG is made in the test with the
    /// ImageSharp encoder, decoded with PreviewReader and goes into the analyzer as in a real
    /// run. The preview is much brighter than the raw, as on a real camera.
    /// </summary>
    static ExposureResult ResultWithPreview(bool cfa = true)
    {
        Img img = cfa
            ? FlatCheck.Tests.TestImages.FlatCfa(128, 128, 550)
            : FlatCheck.Tests.TestImages.From(128, 128, FlatCheck.Tests.FitsTestData.Radial(128, 128, center: 550));
        PreviewImage preview = PreviewReader.Decode(FlatCheck.Tests.TestImages.JpegOf(160, 120, (x, y) =>
        {
            // centre brighter than the corners, so it shows that the table takes the CENTRE
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

    /// <summary>A FITS has no preview: the switch is off, the view is on the raw values, and it
    /// says why.</summary>
    [Fact]
    public async Task WithoutAPreviewTheSwitchIsDisabled()
    {
        using var f = new SessionFixture().WithFlatMaster();
        string path = f.WriteAnalysableRawFlat("flat.fit");

        var vm = Attached(f);
        vm.Selected = path;
        await vm.Pending;

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
        Assert.Equal(4, vm.Histograms.Count);          // the raw rows
        Assert.Equal("", vm.SourceNote);               // on the raw values, with a preview available: nothing to explain
        Assert.True(vm.HasComparison);
    }

    /// <summary>
    /// Switching rebuilds the rows from the result held, it reruns nothing: the verdict's
    /// figures stay identical and the tab does not go through the waiting state.
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
    /// The function's most important decision: on the preview there are no bands, no note,
    /// no channel "that decides". All four thresholds are NaN, so the drawing traces no
    /// zone - without any flag in HistogramCanvas.
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

    /// <summary>The table compares centre with centre, not the median of the whole frame.</summary>
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
        Assert.NotEqual($"{prevR.MedianPct:F1} %", vm.Comparison[0].Screen);   // the centre is brighter than the frame
        Assert.StartsWith("+", vm.Comparison[0].Gap);
    }

    /// <summary>
    /// A raw judged mono (a single channel, no colour): a single pair, no exception.
    /// </summary>
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
        // Switching off pulls the choice back to the raw values: there is nothing to show on the
        // preview.
        Assert.False(vm.Source.IsPreviewSelected);
    }

    /// <summary>A new file without a preview, after one with: the switch goes off and the view
    /// returns to the raw values.</summary>
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
        await vm.Pending;
        vm.HistogramGuide.IsExpanded = true;

        vm.Reset();

        Assert.Empty(vm.Histograms);
        Assert.Empty(vm.Reading);
        Assert.Empty(vm.ChannelOrder);
        // The view preference survives, as with the map guide.
        Assert.True(vm.HistogramGuide.IsExpanded);
    }

    [Fact]
    public async Task ResetClearsEverything()
    {
        using var f = new SessionFixture().WithFlatMaster();
        string path = f.WriteAnalysableRawFlat("flat.fit");
        var vm = Attached(f);
        vm.Selected = path;
        await vm.Pending;

        vm.Reset();

        Assert.Empty(vm.Files);
        Assert.Null(vm.Selected);
        Assert.False(vm.HasResult);
        Assert.Empty(vm.Verdict);
    }
}
