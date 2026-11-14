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

using LightsCheck.Core;

namespace LightsCheck.Tests;

public class SessionFolderTests
{
    [Fact]
    public void R002_RawsWinOverFitsInTheSameFolderAndSortByName()
    {
        using TempWorkspace ws = new();
        foreach (string name in new[] { "DSC_0010.NEF", "DSC_0002.nef", "DSC_0001.NEF", "light_00001.fit", "notes.txt" })
        {
            File.WriteAllBytes(ws.PathTo(name), [0]);
        }

        IReadOnlyList<string> frames = SessionFolder.FramesIn(ws.Dir);
        Assert.Equal(["DSC_0001.NEF", "DSC_0002.nef", "DSC_0010.NEF"], frames.Select(f => Path.GetFileName(f)).ToArray());
    }

    [Fact]
    public void R002_FitsAreTheFramesWhenThereIsNoRaw()
    {
        using TempWorkspace ws = new();
        foreach (string name in new[] { "b.fits", "a.fit", "c.fts", "x.png" })
        {
            File.WriteAllBytes(ws.PathTo(name), [0]);
        }

        Assert.Equal(["a.fit", "b.fits", "c.fts"], SessionFolder.FramesIn(ws.Dir).Select(f => Path.GetFileName(f)).ToArray());
    }

    /// <summary>darks/ is listed like biases/ and never read: a folder of junk bytes counts, and no file is opened (R-042).</summary>
    [Fact]
    public void R042_DarksAreCountedNeverRead()
    {
        using SessionFixture s = new SessionFixture().WithLights(2, biases: 1, darks: 3);
        File.WriteAllBytes(Path.Combine(s.Root, "darks", "dark_0004.fit"), [1, 2, 3]);   // not a FITS: counted all the same

        SessionFolder f = SessionFolder.Open(s.Root);

        Assert.Equal(Path.Combine(s.Root, "darks"), f.DarksDir);
        Assert.Equal(["dark_0001.fit", "dark_0002.fit", "dark_0003.fit", "dark_0004.fit"], f.Darks.Select(Path.GetFileName));
        Assert.Equal(2, f.Lights.Count);
    }

    [Fact]
    public void R042_WithoutDarksTheFolderIsNullAndTheListEmpty()
    {
        using SessionFixture s = new SessionFixture().WithLights(2);
        Directory.CreateDirectory(Path.Combine(s.Root, "darks"));   // empty: as good as absent

        SessionFolder f = SessionFolder.Open(s.Root);

        Assert.Null(f.DarksDir);
        Assert.Empty(f.Darks);
    }

    /// <summary>A session root holds result.fit and friends: an empty lights/ must not fall back to them.</summary>
    [Fact]
    public void R002_AnEmptyLightsSubfolderIsAnErrorNotAFallbackToTheRoot()
    {
        using SessionFixture s = new SessionFixture().WithLights(2, inSubfolder: false);
        Directory.CreateDirectory(s.LightsDir);
        InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => SessionFolder.Open(s.Root));
        Assert.Contains(s.LightsDir, e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void R002_WithoutALightsSubfolderTheFolderItselfHoldsTheLights()
    {
        using SessionFixture s = new SessionFixture().WithLights(2, inSubfolder: false);
        SessionFolder f = SessionFolder.Open(s.Root);
        Assert.Equal(s.Root, f.LightsDir);
        Assert.Equal(2, f.Lights.Count);
        Assert.Null(f.BiasesDir);
    }

    /// <summary>
    /// Without lights/ the folder also holds what the stacking wrote: a FITS whose header counts
    /// more than one stacked frame, in Siril's keyword or the common one, is not a light (R-002).
    /// </summary>
    [Theory]
    [InlineData("STACKCNT")]
    [InlineData("NCOMBINE")]
    public void R002_AStackBesideFitsLightsInTheFolderItselfIsNotALight(string keyword)
    {
        using SessionFixture s = new SessionFixture().WithLights(2, inSubfolder: false);
        File.WriteAllBytes(Path.Combine(s.Root, "result.fit"), Stack(keyword, "50"));

        SessionFolder f = SessionFolder.Open(s.Root);

        Assert.Equal(s.Root, f.LightsDir);
        Assert.Equal(["DSC_0001.fit", "DSC_0002.fit"], f.Lights.Select(Path.GetFileName));
    }

    /// <summary>
    /// Only a header that says so takes a FITS out of the lights: one that cannot be read stays,
    /// so its row can say the error, and so does a frame that counts itself alone (R-002).
    /// </summary>
    [Fact]
    public void R002_AFitsThatCannotBeReadOrCountsOneFrameStaysALight()
    {
        using SessionFixture s = new SessionFixture().WithLights(2, inSubfolder: false);
        File.WriteAllBytes(Path.Combine(s.Root, "broken.fit"), [1, 2, 3]);
        File.WriteAllBytes(Path.Combine(s.Root, "single.fit"), Stack("STACKCNT", "1"));

        SessionFolder f = SessionFolder.Open(s.Root);

        Assert.Equal(["broken.fit", "DSC_0001.fit", "DSC_0002.fit", "single.fit"], f.Lights.Select(Path.GetFileName));
    }

    /// <summary>A folder whose only FITS is a stack holds no light, and says so as any folder without frames does (R-002).</summary>
    [Fact]
    public void R002_AFolderWithOnlyAStackHasNoFrame()
    {
        using TempWorkspace ws = new();
        File.WriteAllBytes(ws.PathTo("result.fit"), Stack("STACKCNT", "82"));

        InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => SessionFolder.Open(ws.Dir));
        Assert.Contains(ws.Dir, e.Message, StringComparison.Ordinal);
    }

    /// <summary>A small FITS with a light's cards and one more, the count of stacked frames.</summary>
    private static byte[] Stack(string keyword, string count) =>
        FitsTestData.BuildFits(32, 24, (x, y) => 300, bitpix: 16, extraCards: [.. SessionFixture.Cards(SessionFixture.FirstStart, 120.2), FitsTestData.Card(keyword, count)]);

    [Fact]
    public void R002_BiasesAreOptionalAndListedWhenPresent()
    {
        using SessionFixture s = new SessionFixture().WithLights(1, biases: 4);
        SessionFolder f = SessionFolder.Open(s.Root);
        Assert.Equal(Path.Combine(s.Root, "biases"), f.BiasesDir);
        Assert.Equal(4, f.Biases.Count);
    }

    [Fact]
    public void R006_PausesAreMeasuredFromTheEndOfThePreviousExposure()
    {
        FrameInfo[] frames =
        [
            Frame(1, SessionFixture.FirstStart, 120),
            Frame(2, SessionFixture.FirstStart.AddSeconds(150), 120),
            Frame(3, SessionFixture.FirstStart.AddSeconds(271), 120),
            new FrameInfo(4, "x", null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, "stricat"),
            Frame(5, SessionFixture.FirstStart.AddSeconds(600), 60),
        ];
        Cadence c = CadenceAnalyzer.Analyze(frames);
        Assert.Equal([null, 30, 1, null, null], c.PauseSeconds);
        Assert.Equal(SessionFixture.FirstStart, c.Start);
        Assert.Equal(SessionFixture.FirstStart.AddSeconds(660), c.End);
        Assert.Equal(660, c.SpanSeconds);
        Assert.Equal(420, c.IntegrationSeconds);
        Assert.Equal(4, c.TimedFrames);
        Assert.Equal(100.0 * 420 / 660, c.DutyPct, 9);
    }

    [Fact]
    public void R006_NoTimedFrameMeansNoIntervalAndZeroDuty()
    {
        Cadence c = CadenceAnalyzer.Analyze([new FrameInfo(1, "x", null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null)]);
        Assert.Null(c.Start);
        Assert.Equal(0, c.DutyPct);
        Assert.Equal([null], c.PauseSeconds);
    }

    /// <summary>
    /// The review's case (CR-010 of 2026-11-13): the camera's counter passed from 9999 to 0001
    /// during the night, so by name the frame taken first is listed last. The cadence names the
    /// first pair of neighbours in the list whose times go back (R-089); the order and the
    /// figures stay as the names give them.
    /// </summary>
    [Fact]
    public void R089_AFrameTakenBeforeTheOneListedBeforeItIsFound()
    {
        DateTime t = SessionFixture.FirstStart;
        FrameInfo[] frames =
        [
            Frame(1, t.AddSeconds(150), 120),
            Frame(2, t.AddSeconds(300), 120),
            Frame(9999, t, 120),
        ];

        Cadence c = CadenceAnalyzer.Analyze(frames);

        Assert.Equal((1, 2), c.Backwards);
        Assert.Equal([null, 30, -420], c.PauseSeconds);
    }

    /// <summary>
    /// Frames in the order of their times have no such pair, equal times included (a raw's time
    /// is in whole seconds); a frame without a time between two does not hide the pair, and one
    /// without an exposure still has its place in time (R-089).
    /// </summary>
    [Fact]
    public void R089_TheTimesAreComparedBetweenNeighboursWithATime()
    {
        DateTime t = SessionFixture.FirstStart;
        FrameInfo untimed = new(2, "x", null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, "stricat");

        Assert.Null(CadenceAnalyzer.Analyze([Frame(1, t, 120), Frame(2, t.AddSeconds(150), 120), Frame(3, t.AddSeconds(300), 120)]).Backwards);
        Assert.Null(CadenceAnalyzer.Analyze([Frame(1, t, 0.25), Frame(2, t, 0.25)]).Backwards);
        Assert.Null(CadenceAnalyzer.Analyze([untimed]).Backwards);
        Assert.Equal((0, 2), CadenceAnalyzer.Analyze([Frame(1, t.AddSeconds(150), 120), untimed, Frame(3, t, 120)]).Backwards);
        Assert.Equal((0, 1), CadenceAnalyzer.Analyze([Frame(1, t.AddSeconds(150), 120), Frame(2, t, 120) with { ExposureSeconds = null }]).Backwards);
        // The first pair is named, not every one.
        Assert.Equal((0, 1), CadenceAnalyzer.Analyze([Frame(1, t.AddSeconds(300), 120), Frame(2, t.AddSeconds(150), 120), Frame(3, t, 120)]).Backwards);
    }

    private static FrameInfo Frame(int index, DateTime t, double exposure) =>
        new(index, $"DSC_{index:0000}.NEF", "NIKON D3300", 135, 2.8, exposure, 1600, t, null, 202, 6016, 4016, 74, "Nikon F", null, null, null, null, null);
}
