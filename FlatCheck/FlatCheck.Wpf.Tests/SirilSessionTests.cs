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

using FlatCheck.Tests;

namespace FlatCheck.Wpf.Tests;

/// <summary>
/// Deducing the folder structure. It is the only part of the WPF application that has real
/// rules - the rest is layout - so it is also the part that deserves the most tests.
/// </summary>
public class SirilSessionTests
{
    /// <summary>
    /// A session folder like the one the Siril scripts produce. The master is written in
    /// <paramref name="masterIn"/>: "process" (the scripts from the repository) or "masters"
    /// (the scripts from the 1.4 installation).
    /// </summary>
    static SirilSession Session(TempWorkspace ws, bool process = true, bool master = true,
                                bool flats = true, string masterIn = "process")
    {
        if (process) Directory.CreateDirectory(Path.Combine(ws.Dir, "process"));
        if (flats) Directory.CreateDirectory(Path.Combine(ws.Dir, "flats"));
        if (master) WriteMaster(ws, masterIn);
        return new SirilSession(ws.Dir);
    }

    static string WriteMaster(TempWorkspace ws, string folder, double k = 0.45)
    {
        Directory.CreateDirectory(Path.Combine(ws.Dir, folder));
        string path = Path.Combine(ws.Dir, folder, SirilSession.FlatMasterName);
        File.WriteAllBytes(path, FitsTestData.BuildFits(32, 32, FitsTestData.Radial(32, 32, k: k)));
        return path;
    }

    // ------------------------------------------------------------------ validation

    [Fact]
    public void ACompleteSessionFolderPasses()
    {
        using var ws = new TempWorkspace();
        Assert.Equal(SessionProblem.None, Session(ws).Check());
    }

    [Fact]
    public void AMissingFolderIsItsOwnProblem()
    {
        var s = new SirilSession(Path.Combine(Path.GetTempPath(), "nu-exista-" + Guid.NewGuid()));
        Assert.Equal(SessionProblem.NoFolder, s.Check());
    }

    /// <summary>
    /// Without process/ no script was run. It is a different mistake from "I ran it, but the
    /// master was not produced", and that is why it is a different code.
    /// </summary>
    [Fact]
    public void AFolderWithoutProcessIsDistinctFromOneWithoutTheMaster()
    {
        using var a = new TempWorkspace();
        using var b = new TempWorkspace();

        Assert.Equal(SessionProblem.NoProcessDir, Session(a, process: false, master: false).Check());
        Assert.Equal(SessionProblem.NoFlatMaster, Session(b, master: false).Check());
    }

    /// <summary>R-080: mode 1's input is taken by convention, not searched for by
    /// content.</summary>
    [Fact]
    public void TheFlatMasterIsTakenByConventionFromProcess()
    {
        using var ws = new TempWorkspace();
        var s = Session(ws);

        Assert.Equal(Path.Combine(ws.Dir, "process", "pp_flat_stacked.fit"), s.FlatMasterPath);
        Assert.False(s.FindFlatMaster().Ambiguous);
    }

    // ------------------------------------------------------------- masters/ or process/

    /// <summary>
    /// The second convention: Siril 1.4's OSC_Preprocessing.ssf writes the master in
    /// masters/, not in process/. The Smida session in the archive is like that, and the
    /// application used to refuse it.
    /// </summary>
    [Fact]
    public void AMasterInMastersIsFoundAndPassesTheCheck()
    {
        using var ws = new TempWorkspace();
        var s = Session(ws, masterIn: "masters");

        Assert.Equal(SessionProblem.None, s.Check());
        Assert.Equal(Path.Combine(ws.Dir, "masters", "pp_flat_stacked.fit"), s.FlatMasterPath);
        Assert.False(s.FindFlatMaster().Ambiguous);
    }

    /// <summary>
    /// The only thing required is the master. A folder with masters/ and without process/ -
    /// the frames deleted to make room - runs the structure; the lights series is simply
    /// empty.
    /// </summary>
    [Fact]
    public void R080_ProcessIsNotRequiredWhenTheMasterIsInMasters()
    {
        using var ws = new TempWorkspace();
        var s = Session(ws, process: false, masterIn: "masters");

        Assert.Equal(SessionProblem.None, s.Check());
        Assert.Empty(s.LightSeries());
    }

    /// <summary>
    /// In both places means two preprocessing runs. The NEWER one is taken - what the last
    /// one produced - and the other is named, so that the tab can warn.
    /// </summary>
    [Fact]
    public void R080_WithAMasterInBothPlacesTheNewerWinsAndTheOtherIsNamed()
    {
        using var ws = new TempWorkspace();
        var s = Session(ws, masterIn: "process");
        string inProcess = s.FlatMasterPath;
        string inMasters = WriteMaster(ws, "masters", k: 0.5);

        File.SetLastWriteTimeUtc(inProcess, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(inMasters, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        var newerInMasters = s.FindFlatMaster();
        Assert.Equal(inMasters, newerInMasters.Path);
        Assert.Equal(inProcess, newerInMasters.Other);
        Assert.True(newerInMasters.Ambiguous);

        File.SetLastWriteTimeUtc(inProcess, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
        var newerInProcess = s.FindFlatMaster();
        Assert.Equal(inProcess, newerInProcess.Path);
        Assert.Equal(inMasters, newerInProcess.Other);

        // and the histogram tab puts first the same master that Structura measures
        Assert.Equal(inProcess, s.HistEqCandidates()[0].Path);
    }

    [Fact]
    public void R080_WithNoMasterAnywhereThePathIsNullAndBothPlacesAreNamed()
    {
        using var ws = new TempWorkspace();
        var s = Session(ws, master: false);

        Assert.Null(s.FlatMasterPath);
        Assert.Equal(SessionProblem.NoFlatMaster, s.Check());
        Assert.Equal(new[] { Path.Combine(ws.Dir, "masters", "pp_flat_stacked.fit"),
                             Path.Combine(ws.Dir, "process", "pp_flat_stacked.fit") },
                     s.FlatMasterCandidates);
        Assert.Contains("masters", SessionMessages.Explain(s, SessionProblem.NoFlatMaster));
        Assert.Contains("process", SessionMessages.Explain(s, SessionProblem.NoFlatMaster));
    }

    /// <summary>"Nothing was run" means both folders are missing, not only process/.</summary>
    [Fact]
    public void NoProcessDirMeansNeitherFolderExists()
    {
        using var a = new TempWorkspace();
        using var b = new TempWorkspace();

        Assert.Equal(SessionProblem.NoProcessDir, Session(a, process: false, master: false).Check());
        Directory.CreateDirectory(Path.Combine(b.Dir, "masters"));
        Assert.Equal(SessionProblem.NoFlatMaster, Session(b, process: false, master: false).Check());
    }

    /// <summary>The report is saved in the session folder, not in process/.</summary>
    [Fact]
    public void TheReportGoesInTheChosenFolderNotInProcess()
    {
        using var ws = new TempWorkspace();
        var s = Session(ws);

        Assert.Equal(Path.Combine(ws.Dir, "Test result.txt"), s.ResultTextPath);
        Assert.Equal(Path.Combine(ws.Dir, "flatcheck.png"), s.ResidualPngPath);
    }

    // ------------------------------------------------------------------ the lights series

    [Fact]
    public void TheLightSeriesUsesTheSameDefaultPatternAsTheConsole()
    {
        using var ws = new TempWorkspace();
        var s = Session(ws);
        string proc = s.ProcessDir;

        File.WriteAllText(Path.Combine(proc, "pp_light_00001.fit"), "x");
        File.WriteAllText(Path.Combine(proc, "pp_light_00002.fit"), "x");

        Assert.Equal(2, s.LightSeries().Count);
    }

    /// <summary>
    /// The registered frames are excluded because the pattern is anchored at the start. It
    /// is not a detail: registration transforms the frame geometrically, so it moves the
    /// flat error relative to the centre around which the radial model is fitted.
    /// </summary>
    [Fact]
    public void R083_RegisteredFramesAreExcludedFromTheSeries()
    {
        using var ws = new TempWorkspace();
        var s = Session(ws);

        File.WriteAllText(Path.Combine(s.ProcessDir, "pp_light_00001.fit"), "x");
        File.WriteAllText(Path.Combine(s.ProcessDir, "r_pp_light_00001.fit"), "x");

        Assert.Equal(new[] { "pp_light_00001.fit" },
                     s.LightSeries().Select(Path.GetFileName).ToArray());
    }

    /// <summary>
    /// The matching is in-process, so ".fit" does not swallow ".fits" as in Win32.
    /// </summary>
    [Fact]
    public void TheSeriesDoesNotSwallowLongerExtensions()
    {
        using var ws = new TempWorkspace();
        var s = Session(ws);

        File.WriteAllText(Path.Combine(s.ProcessDir, "pp_light_00001.fit"), "x");
        File.WriteAllText(Path.Combine(s.ProcessDir, "pp_light_00002.fits"), "x");

        Assert.Single(s.LightSeries());
    }

    [Fact]
    public void AMissingProcessFolderGivesAnEmptySeriesRatherThanThrowing()
    {
        using var ws = new TempWorkspace();
        var s = Session(ws, process: false, master: false);

        Assert.Empty(s.LightSeries());
    }

    // ------------------------------------------------------- what a flat master is

    /// <summary>
    /// A session's overall master IS a flat master, so it is a valid input - and in the
    /// normal flow it really IS the input: the two orientations are two sittings, so two
    /// folders, and pp_flat_stacked.fit is taken from each.
    ///
    /// There was an exclusion of it, until the tab was rewritten. It was not a domain rule:
    /// it was a PRESELECTION fix from a time when the tab filled a list by itself from
    /// process/ and, the sort being ordinal, "pp_flat_stacked.fit" fell after
    /// "pp_flat_A/B.fit" and landed on set B's preselection. Without the list, the reason
    /// went away.
    /// </summary>
    [Fact]
    public void TheOverallFlatMasterIsAValidInput()
    {
        using var ws = new TempWorkspace();
        var s = Session(ws);

        Assert.True(SirilSession.IsFlatMasterName(s.FlatMasterPath));
    }

    /// <summary>The pattern matching is case-insensitive.</summary>
    [Fact]
    public void TheNameCheckIgnoresCase()
    {
        Assert.True(SirilSession.IsFlatMasterName(@"C:\undeva\PP_FLAT_STACKED.FIT"));
        Assert.True(SirilSession.IsFlatMasterName(@"C:\undeva\Pp_Flat_Stacked_A.Fit"));
    }

    [Theory]
    [InlineData("pp_flat_stacked.fit", true)]
    [InlineData("pp_flat_stacked_A.fit", true)]
    [InlineData(@"C:\altundeva\pp_flat_stacked_B.fit", true)]
    [InlineData("flat_stacked.fit", false)]      // uncalibrated: without the pp_ prefix
    [InlineData("pp_light_00001.fit", false)]
    [InlineData("pp_flat_stacked.fits", false)]  // a different extension
    [InlineData("pp_flat_stacked.tif", false)]
    public void AFileIsAFlatMasterOnlyIfItsNameSaysSo(string path, bool expected)
        => Assert.Equal(expected, SirilSession.IsFlatMasterName(path));

    /// <summary>
    /// The individual calibrated frames sit in the same process/, with the same prefix, and
    /// look the same - but they are not masters. The pattern was "pp_flat*.fit" and caught
    /// them, so the Ratio tab's dialog even offered them. A pair of individual frames gives a
    /// confident and false result: a frame's noise is of the same order as the signal sought.
    /// </summary>
    [Theory]
    [InlineData("pp_flat_00001.fit")]
    [InlineData("pp_flat_00042.fit")]
    [InlineData("pp_flat_.fit")]
    public void R082_AnIndividualCalibratedFlatIsNotAFlatMaster(string name)
        => Assert.False(SirilSession.IsFlatMasterName(@"C:\sesiune\process\" + name));

    [Fact]
    public void AnEmptyPathIsNotAFlatMaster()
    {
        Assert.False(SirilSession.IsFlatMasterName(null));
        Assert.False(SirilSession.IsFlatMasterName(""));
    }

    // ------------------------------------------------------------------ raw flats

    /// <summary>
    /// Camera raws only. A FITS in flats/ can be anything - including one already
    /// debayered, which the mode would refuse anyway - so it is not offered.
    /// </summary>
    [Fact]
    public void OnlyCameraRawFilesAreOfferedForTheExposureCheck()
    {
        using var ws = new TempWorkspace();
        var s = Session(ws);

        foreach (string n in new[] { "a.NEF", "b.cr2", "c.dng", "d.fit", "e.jpg", "f.txt" })
            File.WriteAllText(Path.Combine(s.FlatsDir, n), "x");

        Assert.Equal(new[] { "a.NEF", "b.cr2", "c.dng" },
                     s.RawFlats().Select(Path.GetFileName).ToArray());
    }

    /// <summary>
    /// The extensions come from RawReader, not from a list written in the WPF application.
    /// The test pins exactly that: if the reader learns a new camera, the list shows it.
    /// </summary>
    [Fact]
    public void TheOfferedExtensionsAreTheOnesTheReaderAccepts()
    {
        using var ws = new TempWorkspace();
        var s = Session(ws);

        foreach (string ext in RawReader.RawExtensions)
            File.WriteAllText(Path.Combine(s.FlatsDir, "flat" + ext), "x");

        Assert.Equal(RawReader.RawExtensions.Count, s.RawFlats().Count);
    }

    [Fact]
    public void AMissingFlatsFolderGivesAnEmptyListRatherThanThrowing()
    {
        using var ws = new TempWorkspace();
        Assert.Empty(Session(ws, flats: false).RawFlats());
    }

    [Fact]
    public void RawFlatsAreSortedOrdinallyByName()
    {
        using var ws = new TempWorkspace();
        var s = Session(ws);

        foreach (string n in new[] { "DSC_0003.NEF", "DSC_0001.NEF", "DSC_0002.NEF" })
            File.WriteAllText(Path.Combine(s.FlatsDir, n), "x");

        Assert.Equal(new[] { "DSC_0001.NEF", "DSC_0002.NEF", "DSC_0003.NEF" },
                     s.RawFlats().Select(Path.GetFileName).ToArray());
    }

    // ------------------------------------------------------------------ the histogram tab

    /// <summary>For the equalisation a converted FITS is as good as the raw; the rest are
    /// not.</summary>
    [Fact]
    public void FlatFramesListRawAndFitsAndNothingElse_InOrdinalOrder()
    {
        using var ws = new TempWorkspace();
        var s = Session(ws);

        foreach (string n in new[] { "b.fit", "a.NEF", "c.FITS", "d.jpg", "e.txt", "A.fts" })
            File.WriteAllText(Path.Combine(s.FlatsDir, n), "x");

        Assert.Equal(new[] { "A.fts", "a.NEF", "b.fit", "c.FITS" },
                     s.FlatFrames().Select(Path.GetFileName).ToArray());

        using var bare = new TempWorkspace();
        Assert.Empty(Session(bare, flats: false).FlatFrames());
    }

    [Fact]
    public void R085_TheHistEqCandidatesPutTheMasterFirst()
    {
        using var ws = new TempWorkspace();
        var s = Session(ws);
        foreach (string n in new[] { "DSC_0002.NEF", "DSC_0001.NEF" })
            File.WriteAllText(Path.Combine(s.FlatsDir, n), "x");

        var c = s.HistEqCandidates();

        Assert.Equal(3, c.Count);
        Assert.Equal(HistEqKind.Master, c[0].Kind);
        Assert.Equal(s.FlatMasterPath, c[0].Path);
        Assert.Equal(new[] { "DSC_0001.NEF", "DSC_0002.NEF" }, c.Skip(1).Select(x => Path.GetFileName(x.Path)));
        Assert.All(c.Skip(1), x => Assert.Equal(HistEqKind.Frame, x.Kind));
    }

    [Fact]
    public void WithoutTheMasterFileTheCandidatesAreJustTheFrames()
    {
        using var ws = new TempWorkspace();
        var s = Session(ws, master: false);
        File.WriteAllText(Path.Combine(s.FlatsDir, "a.NEF"), "x");

        var c = s.HistEqCandidates();

        Assert.Single(c);
        Assert.Equal(HistEqKind.Frame, c[0].Kind);
    }

    [Fact]
    public void WithNeitherMasterNorFramesTheCandidatesAreEmpty()
    {
        using var ws = new TempWorkspace();
        Assert.Empty(Session(ws, master: false, flats: false).HistEqCandidates());
    }

    /// <summary>
    /// Only the master by convention: a second stack in process/ is the Ratio tab's
    /// business, and here it would appear as "one more master", which it is not.
    /// </summary>
    [Fact]
    public void AnotherStackInProcessIsNotACandidate()
    {
        using var ws = new TempWorkspace();
        var s = Session(ws);
        File.WriteAllText(Path.Combine(s.ProcessDir, "pp_flat_stacked_0deg.fit"), "x");

        var c = s.HistEqCandidates();

        Assert.Single(c);
        Assert.Equal(s.FlatMasterPath, c[0].Path);
    }

    /// <summary>
    /// The smoothed flat written by the Netezire tab sits beside the master and is listed
    /// after it, as a third kind of row - not master, not frame.
    /// </summary>
    [Fact]
    public void R085_ASmoothedMasterIsListedRightAfterTheMaster()
    {
        using var ws = new TempWorkspace();
        var s = Session(ws);
        File.Copy(s.FlatMasterPath, s.SmoothedMasterPath);
        foreach (string n in new[] { "DSC_0002.NEF", "DSC_0001.NEF" })
            File.WriteAllText(Path.Combine(s.FlatsDir, n), "x");

        var c = s.HistEqCandidates();

        Assert.Equal(new[] { HistEqKind.Master, HistEqKind.Smoothed, HistEqKind.Frame, HistEqKind.Frame },
                     c.Select(x => x.Kind));
        Assert.Equal(s.SmoothedMasterPath, c[1].Path);
        Assert.Equal("pp_flat_stacked_smoothed.fit", Path.GetFileName(c[1].Path));
    }

    /// <summary>
    /// With masters in both places, the smoothed file beside the OLDER one is the smoothing
    /// of a master other than the one on the screen, and it does not appear. Only the exact
    /// path beside the chosen one.
    /// </summary>
    [Fact]
    public void R085_ASmoothedFileBesideTheOlderMasterIsNotListed()
    {
        using var ws = new TempWorkspace();
        var s = Session(ws, masterIn: "process");
        string inProcess = s.FlatMasterPath;
        string inMasters = WriteMaster(ws, "masters", k: 0.5);
        File.SetLastWriteTimeUtc(inProcess, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(inMasters, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        File.Copy(inProcess, SirilSession.SmoothedPathBeside(inProcess));

        var c = s.HistEqCandidates();

        Assert.Equal(inMasters, s.FlatMasterPath);
        Assert.Single(c);
        Assert.Equal(HistEqKind.Master, c[0].Kind);

        // ...and it appears as soon as it is beside the chosen one
        File.Copy(inMasters, s.SmoothedMasterPath);
        Assert.Equal(new[] { HistEqKind.Master, HistEqKind.Smoothed }, s.HistEqCandidates().Select(x => x.Kind));
    }

    /// <summary>Without a master there is no "beside the master": a stray _smoothed is not listed
    /// and does not throw.</summary>
    [Fact]
    public void WithoutAMasterASmoothedFileIsNotListed()
    {
        using var ws = new TempWorkspace();
        var s = Session(ws, master: false);
        File.WriteAllText(Path.Combine(s.ProcessDir, "pp_flat_stacked_smoothed.fit"), "x");
        File.WriteAllText(Path.Combine(s.FlatsDir, "a.NEF"), "x");

        Assert.Null(s.SmoothedMasterPath);
        var c = s.HistEqCandidates();
        Assert.Single(c);
        Assert.Equal(HistEqKind.Frame, c[0].Kind);
    }
}
