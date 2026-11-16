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
    public void R002_APentaxPefIsARawFrame()
    {
        // taken from FlatCheck 2026-10-26 (ADR 0023): a K-70 session was refused as FITS files that do not begin with SIMPLE
        using TempWorkspace ws = new();
        foreach (string name in new[] { "IMGP0002.PEF", "IMGP0001.pef", "dark_00001.fit" })
        {
            File.WriteAllBytes(ws.PathTo(name), [0]);
        }

        IReadOnlyList<string> frames = SessionFolder.FramesIn(ws.Dir);
        Assert.Equal(["IMGP0001.pef", "IMGP0002.PEF"], frames.Select(f => Path.GetFileName(f)).ToArray());
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

    /// <summary>
    /// CR-001: a card a Mac has touched holds `._DSC_0034.NEF` beside every file; they sorted first
    /// and took the darks' places under the frame cap.
    /// </summary>
    [Fact]
    public void R002_SidecarAndHiddenFilesAreNotFrames()
    {
        using TempWorkspace ws = new();
        foreach (string name in new[] { "DSC_0002.NEF", "._DSC_0002.NEF", "DSC_0001.NEF", "._DSC_0001.NEF", ".trashed.nef", "hidden.NEF" })
        {
            File.WriteAllBytes(ws.PathTo(name), [0]);
        }

        File.SetAttributes(ws.PathTo("hidden.NEF"), File.GetAttributes(ws.PathTo("hidden.NEF")) | FileAttributes.Hidden);

        Assert.Equal(["DSC_0001.NEF", "DSC_0002.NEF"], SessionFolder.FramesIn(ws.Dir).Select(f => Path.GetFileName(f)).ToArray());
    }

    /// <summary>The same for FITS: sidecars alone do not make a folder of frames, and raws beside them still win.</summary>
    [Fact]
    public void R002_SidecarFitsAreNotFramesEither()
    {
        using TempWorkspace ws = new();
        foreach (string name in new[] { "._dark_00001.fit", "dark_00001.fit", "._dark_00002.fit", "dark_00002.fit" })
        {
            File.WriteAllBytes(ws.PathTo(name), [0]);
        }

        Assert.Equal(["dark_00001.fit", "dark_00002.fit"], SessionFolder.FramesIn(ws.Dir).Select(f => Path.GetFileName(f)).ToArray());

        using TempWorkspace only = new();
        File.WriteAllBytes(only.PathTo("._dark_00001.fit"), [0]);
        Assert.Empty(SessionFolder.FramesIn(only.Dir));
    }

    /// <summary>
    /// CR-001 on a whole session: sidecars beside every frame of every group leave the three lists as
    /// they were. The frame cap picks from these lists, so a file that is not listed takes no dark's place.
    /// </summary>
    [Fact]
    public void R002_SidecarsBesideTheDarksLeaveTheSessionAsItIs()
    {
        using SessionFixture s = new SessionFixture().WithDarks(3, biases: 2, lights: 2);
        foreach (string dir in new[] { s.DarksDir, s.BiasesDir, s.LightsDir })
        {
            foreach (string frame in Directory.GetFiles(dir))
            {
                File.WriteAllBytes(Path.Combine(dir, "._" + Path.GetFileName(frame)), [0, 5, 22, 7]);
            }
        }

        SessionFolder session = SessionFolder.Open(s.Root);
        Assert.Equal(3, session.Darks.Count);
        Assert.Equal(2, session.Biases.Count);
        Assert.Equal(2, session.Lights.Count);
        Assert.DoesNotContain(session.Darks.Concat(session.Biases).Concat(session.Lights), f => Path.GetFileName(f).StartsWith('.'));
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

    /// <summary>A lights-only session opens only when allowed (the run with a partner, the inventory); a lights folder without frames stays an error.</summary>
    [Fact]
    public void R002_ALightsOnlySessionOpensOnlyWhenAllowed()
    {
        using SessionFixture s = new SessionFixture().WithLights(2, i => new SyntheticLight { Seed = 300 + i }).WithBiases(2);
        InvalidOperationException refused = Assert.Throws<InvalidOperationException>(() => SessionFolder.Open(s.Root));
        Assert.Contains("are lights\\ dar nu are darks\\", refused.Message, StringComparison.Ordinal);

        SessionFolder f = SessionFolder.Open(s.Root, allowLightsOnly: true);
        Assert.False(f.HasDarks);
        Assert.Empty(f.Darks);
        Assert.Equal(2, f.Lights.Count);
        Assert.Equal(2, f.Biases.Count);
        Assert.Equal(s.Root, f.Root);

        using TempWorkspace empty = new();
        Directory.CreateDirectory(Path.Combine(empty.Dir, "lights"));
        Assert.Contains("nu are darks", Assert.Throws<InvalidOperationException>(() => SessionFolder.Open(empty.Dir, allowLightsOnly: true)).Message, StringComparison.Ordinal);
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
            new FrameInfo(4, "x", null, null, null, null, null, null, null, null, null, null, null, null, null, "stricat"),
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

    /// <summary>
    /// CR-013: the frames are read in the order of their names as if it were the order they were taken in; the
    /// first place where the times go back is found, so the session lines can say it.
    /// </summary>
    [Fact]
    public void R125_AFrameTakenBeforeTheOneListedBeforeItIsFound()
    {
        DateTime t = SessionFixture.FirstStart;
        // the counter passed 9999 during the series: by name the two frames taken last come first
        FrameInfo[] wrapped = [Frame(1, t.AddSeconds(240), 120), Frame(2, t.AddSeconds(360), 120), Frame(3, t, 120), Frame(4, t.AddSeconds(120), 120)];
        Assert.Equal((1, 2), CadenceAnalyzer.Analyze(wrapped).Backwards);
        Assert.Equal((1, 2), CadenceAnalyzer.FirstBackwards(wrapped));

        // names in the order of the times: nothing; equal times are no disagreement (a raw's time is in whole seconds)
        Assert.Null(CadenceAnalyzer.Analyze([Frame(1, t, 120), Frame(2, t.AddSeconds(120), 120), Frame(3, t.AddSeconds(240), 120)]).Backwards);
        Assert.Null(CadenceAnalyzer.Analyze([Frame(1, t, 0.0002), Frame(2, t, 0.0002), Frame(3, t.AddSeconds(1), 0.0002)]).Backwards);
        Assert.Null(CadenceAnalyzer.Analyze([]).Backwards);
        Assert.Null(CadenceAnalyzer.Analyze([Frame(1, t, 120)]).Backwards);

        // frames without a time between the two do not count: the pair is the two with a time
        FrameInfo untimed = new(2, "x", null, null, null, null, null, null, null, null, null, null, null, null, null, "stricat");
        Assert.Equal((0, 2), CadenceAnalyzer.Analyze([Frame(1, t.AddSeconds(240), 120), untimed, Frame(3, t, 120)]).Backwards);
        Assert.Null(CadenceAnalyzer.Analyze([Frame(1, t, 120), untimed, Frame(3, t.AddSeconds(240), 120)]).Backwards);

        // a frame without an exposure still has its time
        FrameInfo noExposure = Frame(2, t, 120) with { ExposureSeconds = null };
        Assert.Equal((0, 1), CadenceAnalyzer.Analyze([Frame(1, t.AddSeconds(240), 120), noExposure]).Backwards);
    }

    [Fact]
    public void R125_TheSessionLinesSayWhenTheNamesAreNotInTheOrderTheFramesWereTaken()
    {
        // four darks whose names are in the order they were taken, and three lights: no line
        using SessionFixture ok = new SessionFixture().WithDarks(4, lights: 3);
        Assert.DoesNotContain(InventoryReport.HeaderRows(InventoryRun.Run(ok.Root)), r => r.Label == "ATENTIE");

        // DSC_9998 and DSC_9999 were taken before DSC_0001 .. DSC_0004 and are listed after them
        using SessionFixture s = new SessionFixture().WithDarks(4, lights: 3);
        DateTime first = SessionFixture.FirstStart;
        File.WriteAllBytes(Path.Combine(s.DarksDir, "DSC_9998.fit"), SessionFixture.Frame(first.AddSeconds(-240), 119.2, 400));
        File.WriteAllBytes(Path.Combine(s.DarksDir, "DSC_9999.fit"), SessionFixture.Frame(first.AddSeconds(-120), 119.2, 400));
        // and one light named without leading zeros, taken last and listed second
        File.WriteAllBytes(Path.Combine(s.LightsDir, "light_00011.fit"), SessionFixture.Frame(first.AddSeconds(-3000), 119.2, 400));
        Inventory inv = InventoryRun.Run(s.Root);
        Assert.Equal((3, 4), inv.DarksCadence.Backwards);
        Assert.Equal((1, 2), inv.LightsCadence.Backwards);
        IReadOnlyList<ReportRow> rows = InventoryReport.HeaderRows(inv);
        int darksInterval = rows.ToList().FindIndex(r => r.Label == "interval darks"), lightsInterval = rows.ToList().FindIndex(r => r.Label == "interval lights");
        Assert.Equal("ATENTIE", rows[darksInterval + 1].Label);
        Assert.Equal(
            "numele fisierelor din darks nu sunt in ordinea in care au fost facute cadrele: DSC_0004.fit e trecut inaintea lui DSC_9998.fit, dar a fost facut dupa el. "
            + "Pauzele, incalzirea aparatului in timpul seriei (verificarea 4) si cadrele de aruncat urmeaza numele, asa ca pot fi gresite",
            rows[darksInterval + 1].Value);
        Assert.Equal("ATENTIE", rows[lightsInterval + 1].Label);
        Assert.Equal(
            "numele fisierelor din lights nu sunt in ordinea in care au fost facute cadrele: light_00011.fit e trecut inaintea lui light_0002.fit, dar a fost facut dupa el. "
            + "Pauzele si \"primul\" si \"ultimul\" light din verificarea 2 urmeaza numele, asa ca pot fi gresite",
            rows[lightsInterval + 1].Value);
        // the order of the frames, their numbers and the errors are as before
        Assert.Equal(["DSC_0001.fit", "DSC_0002.fit", "DSC_0003.fit", "DSC_0004.fit", "DSC_9998.fit", "DSC_9999.fit"], inv.Darks.Select(f => f.FileName));
        Assert.False(inv.HasErrors);
        using (Ui.Use(UiLanguage.English))
        {
            IReadOnlyList<ReportRow> en = InventoryReport.HeaderRows(inv);
            Assert.Equal(
                "the darks' file names are not in the order the frames were taken: DSC_0004.fit is listed before DSC_9998.fit and was taken after it. "
                + "The pauses, the camera's warming during the series (check 4) and the frames to discard follow the names, so they may be wrong",
                en.Single(r => r.Label == "WARNING" && r.Value.StartsWith("the darks'", StringComparison.Ordinal)).Value);
            Assert.Equal(
                "the lights' file names are not in the order the frames were taken: light_00011.fit is listed before light_0002.fit and was taken after it. "
                + "The pauses and \"the first\" and \"the last\" light of check 2 follow the names, so they may be wrong",
                en.Single(r => r.Label == "WARNING" && r.Value.StartsWith("the lights'", StringComparison.Ordinal)).Value);
        }
    }

    [Fact]
    public void R006_NoTimedFrameMeansNoIntervalAndZeroDuty()
    {
        Cadence c = CadenceAnalyzer.Analyze([new FrameInfo(1, "x", null, null, null, null, null, null, null, null, null, null, null, null, null, null)]);
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
            Frame(4, SessionFixture.FirstStart, 60), new FrameInfo(5, "x", null, null, null, null, null, null, null, null, null, null, null, null, null, "stricat"),
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
        FrameInfo[] lightsNoIso = [new FrameInfo(1, "x", "NIKON D3300", null, null, 120.2, null, SessionFixture.FirstStart, null, null, null, null, null, null, null, null)];
        Assert.Equal(Verdict.Unmeasured, MatchAnalyzer.Analyze(darks, bias, lightsNoIso).DarksVsLights);
        Assert.Equal(Verdict.Ok, MatchAnalyzer.Analyze(darks, bias, []).Bias);
        Assert.Equal(Verdict.Warning, MatchAnalyzer.Analyze(darks, longBias, []).Bias);
        Assert.Equal(Verdict.Unmeasured, MatchAnalyzer.Analyze(darks, [], []).Bias);
    }

    private static FrameInfo Frame(int index, DateTime t, double exposure) =>
        new(index, $"DSC_{index:0000}.NEF", "NIKON D3300", 135, 2.8, exposure, 1600, t, null, 74, "Nikon F", null, null, null, null, null);
}
