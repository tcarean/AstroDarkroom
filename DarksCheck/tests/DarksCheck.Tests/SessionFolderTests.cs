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

using DarksCheck.Core;

namespace DarksCheck.Tests;

public class SessionFolderTests
{
    [Fact]
    public void R002_RawsWinOverFitsInTheSameFolderAndSortByName()
    {
        using TempWorkspace ws = new();
        foreach (string name in new[] { "DSC_0010.NEF", "DSC_0002.nef", "DSC_0001.NEF", "dark_00001.fit", "notes.txt" })
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

    /// <summary>A session root holds result.fit and friends: an empty darks/ must not fall back to them.</summary>
    [Fact]
    public void R002_AnEmptyDarksSubfolderIsAnErrorNotAFallbackToTheRoot()
    {
        using SessionFixture s = new SessionFixture().WithDarks(2, inSubfolder: false);
        Directory.CreateDirectory(s.DarksDir);
        InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => SessionFolder.Open(s.Root));
        Assert.Contains(s.DarksDir, e.Message, StringComparison.Ordinal);
    }

    /// <summary>The same for a session with biases/ or flats/ and no darks/: the FITS at its root are results, not darks.</summary>
    [Theory]
    [InlineData("biases")]
    [InlineData("flats")]
    [InlineData("lights")]
    public void R002_ASessionSubfolderWithoutDarksIsAnError(string subfolder)
    {
        using SessionFixture s = new SessionFixture().WithDarks(2, inSubfolder: false);
        Directory.CreateDirectory(Path.Combine(s.Root, subfolder));
        InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => SessionFolder.Open(s.Root));
        Assert.Contains(subfolder + "\\ dar nu are darks\\", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void R002_WithoutASessionSubfolderTheFolderItselfHoldsTheDarks()
    {
        using SessionFixture s = new SessionFixture().WithDarks(2, inSubfolder: false);
        SessionFolder f = SessionFolder.Open(s.Root);
        Assert.Equal(s.Root, f.DarksDir);
        Assert.Equal(2, f.Darks.Count);
        Assert.Null(f.BiasesDir);
        Assert.Null(f.LightsDir);
    }

    [Fact]
    public void R002_BiasesAndLightsAreOptionalAndListedWhenPresent()
    {
        using SessionFixture s = new SessionFixture().WithDarks(1, biases: 4, lights: 3);
        SessionFolder f = SessionFolder.Open(s.Root);
        Assert.Equal(Path.Combine(s.Root, "biases"), f.BiasesDir);
        Assert.Equal(4, f.Biases.Count);
        Assert.Equal(Path.Combine(s.Root, "lights"), f.LightsDir);
        Assert.Equal(3, f.Lights.Count);
    }

    [Fact]
    public void R002_AnEmptyBiasesOrLightsSubfolderCountsAsAbsent()
    {
        using SessionFixture s = new SessionFixture().WithDarks(1);
        Directory.CreateDirectory(s.BiasesDir);
        Directory.CreateDirectory(s.LightsDir);
        SessionFolder f = SessionFolder.Open(s.Root);
        Assert.Null(f.BiasesDir);
        Assert.Empty(f.Biases);
        Assert.Null(f.LightsDir);
        Assert.Empty(f.Lights);
    }

    [Fact]
    public void R006_PausesAreMeasuredFromTheEndOfThePreviousExposure()
    {
        FrameInfo[] frames =
        [
            Frame(1, SessionFixture.FirstStart, 120),
            Frame(2, SessionFixture.FirstStart.AddSeconds(150), 120),
            Frame(3, SessionFixture.FirstStart.AddSeconds(271), 120),
            new FrameInfo(4, "x", null, null, null, null, null, null, null, null, null, null, null, "stricat"),
            Frame(5, SessionFixture.FirstStart.AddSeconds(600), 60),
        ];
        Cadence c = CadenceAnalyzer.Analyze(frames);
        Assert.Equal([null, 30, 1, null, null], c.PauseSeconds);
        Assert.Equal(0, CadenceAnalyzer.Analyze([Frame(1, SessionFixture.FirstStart, 120.2), Frame(2, SessionFixture.FirstStart.AddSeconds(120), 120.2)]).PauseSeconds[1]);
        Assert.Equal(SessionFixture.FirstStart, c.Start);
        Assert.Equal(SessionFixture.FirstStart.AddSeconds(660), c.End);
        Assert.Equal(660, c.SpanSeconds);
        Assert.Equal(420, c.IntegrationSeconds);
        Assert.Equal(4, c.TimedFrames);
        Assert.Equal(100.0 * 420 / 660, c.DutyPct, 9);
        // The intervals between consecutive timed starts: 150 and 121 s; frame 5 follows a hole.
        Assert.Equal(135.5, c.MedianIntervalSeconds);
        Assert.Equal([new LongPause(2, 30)], c.LongPauses);
    }

    [Fact]
    public void R006_NoTimedFrameMeansNoIntervalAndZeroDuty()
    {
        Cadence c = CadenceAnalyzer.Analyze([new FrameInfo(1, "x", null, null, null, null, null, null, null, null, null, null, null, null)]);
        Assert.Null(c.Start);
        Assert.Equal(0, c.DutyPct);
        Assert.Equal([null], c.PauseSeconds);
        Assert.Null(c.MedianIntervalSeconds);
        Assert.Empty(c.LongPauses);
    }

    [Fact]
    public void R007_TheDominantSettingsAreTheMostFrequentAndExposuresWithinTwoPercentAreOne()
    {
        FrameInfo[] frames =
        [
            Frame(1, SessionFixture.FirstStart, 119.2), Frame(2, SessionFixture.FirstStart, 118.5), Frame(3, SessionFixture.FirstStart, 119.2),
            Frame(4, SessionFixture.FirstStart, 60), new FrameInfo(5, "x", null, null, null, null, null, null, null, null, null, null, null, "stricat"),
        ];
        GroupSettings g = MatchAnalyzer.Settings(frames)!;
        Assert.Equal(119.2, g.ExposureSeconds);
        Assert.Equal(1600, g.Iso);
        Assert.Equal(3, g.Frames);
        Assert.Equal(4, g.Readable);
        Assert.False(g.Uniform);
        Assert.Null(MatchAnalyzer.Settings([]));
    }

    /// <summary>The cluster is named by its most frequent exact exposure, not by the frame that opened it.</summary>
    [Fact]
    public void R007_TheOddFrameFirstDoesNotNameTheGroup()
    {
        FrameInfo[] frames = [Frame(1, SessionFixture.FirstStart, 118.5), Frame(2, SessionFixture.FirstStart, 119.2), Frame(3, SessionFixture.FirstStart, 119.2)];
        GroupSettings g = MatchAnalyzer.Settings(frames)!;
        Assert.Equal(119.2, g.ExposureSeconds);
        Assert.Equal(3, g.Frames);
        Assert.True(g.Uniform);
    }

    [Fact]
    public void R007_TheVerdictsFollowTheSettings()
    {
        FrameInfo[] darks = [Frame(1, SessionFixture.FirstStart, 119.2)];
        FrameInfo[] lightsSame = [Frame(1, SessionFixture.FirstStart, 120.2)];
        FrameInfo[] lightsOther = [Frame(1, SessionFixture.FirstStart, 60)];
        FrameInfo[] bias = [Frame(1, SessionFixture.FirstStart, 0.0002)];
        FrameInfo[] longBias = [Frame(1, SessionFixture.FirstStart, 1.5)];
        Assert.Equal(Verdict.Ok, MatchAnalyzer.Analyze(darks, bias, lightsSame).DarksVsLights);
        Assert.Equal(Verdict.Warning, MatchAnalyzer.Analyze(darks, bias, lightsOther).DarksVsLights);
        Assert.Equal(Verdict.Unmeasured, MatchAnalyzer.Analyze(darks, bias, []).DarksVsLights);
        FrameInfo[] lightsNoIso = [new FrameInfo(1, "x", "NIKON D3300", null, null, 120.2, null, SessionFixture.FirstStart, null, null, null, null, null, null)];
        Assert.Equal(Verdict.Unmeasured, MatchAnalyzer.Analyze(darks, bias, lightsNoIso).DarksVsLights);
        Assert.Equal(Verdict.Ok, MatchAnalyzer.Analyze(darks, bias, []).Bias);
        Assert.Equal(Verdict.Warning, MatchAnalyzer.Analyze(darks, longBias, []).Bias);
        Assert.Equal(Verdict.Unmeasured, MatchAnalyzer.Analyze(darks, [], []).Bias);
    }

    private static FrameInfo Frame(int index, DateTime t, double exposure) =>
        new(index, $"DSC_{index:0000}.NEF", "NIKON D3300", 135, 2.8, exposure, 1600, t, null, 74, null, null, null, null);
}
