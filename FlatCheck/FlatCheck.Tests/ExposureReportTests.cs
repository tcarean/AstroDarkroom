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
/// The wording that goes with the exposure histograms. It lives in Report/ like every
/// other sentence the tool says, even though only the window shows it - a view-model
/// formats, it does not decide what to say.
///
/// The console text itself is pinned by the golden files; what is checked here is the
/// part they cannot see, plus the one console line that changes when the file names its
/// mosaic order.
/// </summary>
public class ExposureReportTests
{
    const int N = 128;
    const double Sat = 1000;

    static ExposureResult AtLevel(double pct, bool sky = false, string? pattern = null)
        => ExposureAnalyzer.Analyze(TestImages.FlatCfa(N, N, Sat * pct / 100.0), Sat, null, sky,
                                    null, pattern == null ? null : CfaPattern.Parse(pattern, "BAYERPAT"));

    /// <summary>Saturates pixels inside the exact window the analyzer reads (see ExposureAnalyzerTests).</summary>
    static ExposureResult Clipped()
    {
        var img = TestImages.FlatCfa(N, N, Sat * 0.55);
        int half = N / 2, lo = (int)(half * 0.4), hi = (int)(half * 0.6);
        int done = 0;
        for (int y = lo; y < hi && done < 5; y++)
            for (int x = lo; x < hi && done < 5; x++, done++)
                img.Set(y * 2, x * 2 + 1, Sat);
        return ExposureAnalyzer.Analyze(img, Sat, null, false);
    }

    static ExposureResult Vignetted()
        => ExposureAnalyzer.Analyze(
            TestImages.From(256, 256, FitsTestData.WithBayerMosaic(
                FitsTestData.Radial(256, 256, center: 34000))), null, null, false);

    // ---------------------------------------------------------------- the band label

    [Fact]
    public void TheBandLabelNamesTheLadderInUse()
    {
        Assert.Equal("tinta 50-65 %", ExposureReport.BandLabel(ExposureBands.For(false)));
        Assert.Equal("tinta sky 15-50 %", ExposureReport.BandLabel(ExposureBands.For(true)));
    }

    // ---------------------------------------------------------------- the reading

    /// <summary>
    /// Names the channel that decides, says where it sits, and says how much headroom is
    /// left. The headroom is a ratio - log2(100/p99) - so it is scale-invariant like every
    /// other figure in the tool.
    /// </summary>
    [Fact]
    public void TheReadingNamesTheDecidingChannelAndTheHeadroom()
    {
        string s = ExposureReport.HistogramReading(AtLevel(55, pattern: "RGGB"));

        Assert.Contains("G1", s);
        Assert.Contains("in tinta 50-65 %", s);
        Assert.Contains("stopuri", s);
    }

    [Fact]
    public void TheReadingSaysHowFarBelowTheTargetItIs()
    {
        string s = ExposureReport.HistogramReading(AtLevel(42));

        Assert.Contains("42.0 %", s);
        Assert.Contains("sub tinta", s);
        Assert.Contains("8.0 puncte", s);
    }

    [Fact]
    public void TheReadingSaysHowFarAboveTheTargetItIs()
    {
        string s = ExposureReport.HistogramReading(AtLevel(75));

        Assert.Contains("peste tinta", s);
        Assert.Contains("10.0 puncte", s);
    }

    /// <summary>Clipping overrides the median, and the reading has to say so in those words.</summary>
    [Fact]
    public void WhenTheCentreClipsTheReadingSaysThatOverridesTheMedian()
    {
        string s = ExposureReport.HistogramReading(Clipped());

        Assert.Contains("saturatia", s);
        Assert.Contains("PREA SUS", s);
        Assert.DoesNotContain("pana la saturatie", s);   // no headroom left to report
    }

    /// <summary>
    /// The card's whole argument in one sentence: the gap between the two curves is
    /// vignetting, not underexposure. Someone who reads the wide curve as "too dark" and
    /// exposes more will clip the centre.
    /// </summary>
    [Fact]
    public void OnAVignettedFrameTheReadingExplainsTheWideCurve()
    {
        string s = ExposureReport.HistogramReading(Vignetted());

        Assert.Contains("vignetarea, nu subexpunere", s);
        Assert.Contains("clipezi centrul", s);
    }

    /// <summary>Without a known order the reading says the colour is unknown instead of picking one.</summary>
    [Fact]
    public void WithoutAKnownOrderTheReadingSaysTheColourIsUnknown()
    {
        string s = ExposureReport.HistogramReading(AtLevel(55));

        Assert.Contains("CFA[0,1]", s);
        Assert.Contains("nu stiu ce culoare are", s);
    }

    [Fact]
    public void TheSkyReadingUsesTheSkyBand()
    {
        string s = ExposureReport.HistogramReading(AtLevel(22, sky: true));

        Assert.Contains("in tinta sky 15-50 %", s);
    }

    /// <summary>
    /// A file that is not a raw flat gets one sentence and no exposure claims: there is
    /// nothing there that means an exposure, and the numbers must not look like they do.
    /// </summary>
    [Fact]
    public void ANonRawFileGetsOneSentenceAndNoExposureClaim()
    {
        var r = ExposureAnalyzer.Analyze(
            TestImages.From(N, N, FitsTestData.Radial(N, N, center: 30000)), null, null, false);

        string s = ExposureReport.HistogramReading(r);

        Assert.False(r.IsRaw);
        Assert.Contains("nu e un flat brut", s);
        Assert.DoesNotContain("tinta", s);
        Assert.DoesNotContain("stopuri", s);
    }

    // ---------------------------------------------------------------- channel notes

    [Fact]
    public void TheBrightestChannelIsNotedAsTheOneThatDecides()
    {
        var r = AtLevel(55);

        string note = ExposureReport.ChannelNote(r, r.Channels[r.BrightestChannel]);

        Assert.Contains("decide", note);
    }

    [Fact]
    public void AChannelThatClipsSaysSo()
    {
        var r = ExposureAnalyzer.Analyze(TestImages.FlatCfa(N, N, 2000), Sat, null, false);

        Assert.Contains("clipping", ExposureReport.ChannelNote(r, r.Channels[r.BrightestChannel]));
    }

    [Fact]
    public void AnOrdinaryChannelHasNoNote()
    {
        var r = AtLevel(55);
        var quiet = r.Channels.First(c => !c.IsBrightest);

        Assert.Equal("", ExposureReport.ChannelNote(r, quiet));
    }

    /// <summary>On a mono frame there is nothing to be brightest *than*, so no note.</summary>
    [Fact]
    public void TheSingleMonoChannelIsNotCalledTheBrightest()
    {
        var r = ExposureAnalyzer.Analyze(
            TestImages.From(N, N, FitsTestData.Radial(N, N, center: 30000)), null, null, false);

        Assert.Equal("", ExposureReport.ChannelNote(r, r.Channels[0]));
    }

    // ---------------------------------------------------------------- the console line

    /// <summary>
    /// With the order known, the console line carries the colours - and keeps the position
    /// beside them, so the line can still be tied back to the mosaic.
    /// </summary>
    [Fact]
    public void TheConsoleLineNamesTheChannelsWhenTheOrderIsKnown()
    {
        var w = new StringWriter();

        ExposureReport.Write(AtLevel(55, pattern: "RGGB"), "flat.fit", w);

        string s = w.ToString();
        Assert.Contains("ordinea R/G/B din BAYERPAT", s);
        Assert.Contains("R  (CFA[0,0])", s);
        Assert.Contains("G1 (CFA[0,1])", s);
        Assert.Contains("G2 (CFA[1,0])", s);
        Assert.Contains("B  (CFA[1,1])", s);
    }

    /// <summary>
    /// Without it, the line is exactly the one every golden file already carries. This is
    /// the reason no golden had to be re-approved for this feature.
    /// </summary>
    [Fact]
    public void WithoutTheOrderTheConsoleLineIsUnchanged()
    {
        var w = new StringWriter();

        ExposureReport.Write(AtLevel(55), "flat.fit", w);

        string s = w.ToString();
        Assert.Contains("informativ; nu stiu care e R/G/B", s);
        Assert.Contains("        CFA[0,0]:", s);
        Assert.DoesNotContain("(CFA[0,0])", s);
    }
}
