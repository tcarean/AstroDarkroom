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
            new FrameInfo(4, "x", null, null, null, null, null, null, null, null, null, null, null, "stricat"),
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
        Cadence c = CadenceAnalyzer.Analyze([new FrameInfo(1, "x", null, null, null, null, null, null, null, null, null, null, null, null)]);
        Assert.Null(c.Start);
        Assert.Equal(0, c.DutyPct);
        Assert.Equal([null], c.PauseSeconds);
    }

    private static FrameInfo Frame(int index, DateTime t, double exposure) =>
        new(index, $"DSC_{index:0000}.NEF", "NIKON D3300", 135, 2.8, exposure, 1600, t, null, 74, null, null, null, null);
}
