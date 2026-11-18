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

namespace CopyWcs.Tests;

/// <summary>
/// The tests that move the process's working folder, which every test of the assembly shares:
/// xunit runs a collection that disables parallelization alone, after the parallel ones.
/// </summary>
[CollectionDefinition("working folder", DisableParallelization = true)]
public sealed class WorkingFolderGroup;

/// <summary>
/// A copy given relative paths while the working folder moves under it, as a file dialog of the
/// window may move it during a copy (CR-006 of the review of 2026-11-16): the write goes on with
/// the files its paths named when it began (R-012).
/// </summary>
[Collection("working folder")]
public class WorkingFolderTests
{
    /// <summary>The names of the files in a folder, sorted, so a test sees every file a run left there.</summary>
    private static string[] FilesIn(string folder)
        => Directory.GetFiles(folder).Select(f => Path.GetFileName(f)).Order(StringComparer.Ordinal).ToArray();

    /// <summary>
    /// The working folder moves to another night's folder, which holds a <c>dest.fit</c> too, as
    /// the in-place write begins: the destination the run read is the one written, from its own
    /// data, and the other night's file keeps every byte (the review of 2026-11-18: the temporary
    /// file was made in the first folder and renamed over the second's <c>dest.fit</c>).
    /// </summary>
    [Fact]
    public void R012_Run_TheWorkingFolderMovingDuringTheWrite_MovesNeitherTheFileWrittenNorItsData()
    {
        using var ws = new TempWorkspace();
        string first = Directory.CreateDirectory(ws.FilePath("first")).FullName;
        string other = Directory.CreateDirectory(ws.FilePath("other")).FullName;
        var data = FitsTestData.DeterministicData(400);
        File.WriteAllBytes(Path.Combine(first, "source.fit"), FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards(), data));
        File.WriteAllBytes(Path.Combine(first, "dest.fit"), FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards(), data));
        var otherNight = FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards(), FitsTestData.DeterministicData(600));
        File.WriteAllBytes(Path.Combine(other, "dest.fit"), otherNight);

        string before = Environment.CurrentDirectory;
        WcsCopyOutcome outcome;
        try
        {
            Environment.CurrentDirectory = first;
            using var log = LogCapture.Start();
            log.At("write ", () => Environment.CurrentDirectory = other);

            outcome = WcsCopy.Run(new WcsCopyRequest("source.fit", "dest.fit", null, IncludeOptical: true, Force: false, DryRun: false));
        }
        finally
        {
            Environment.CurrentDirectory = before;
        }

        Assert.Equal(WcsCopyKind.Copied, outcome.Kind);
        Assert.Equal(otherNight, File.ReadAllBytes(Path.Combine(other, "dest.fit")));
        Assert.Equal(["dest.fit"], FilesIn(other));
        var written = Path.Combine(first, "dest.fit");
        Assert.True(FitsHeader.ReadPrimary(written).Contains("CTYPE1"));
        Assert.Equal(data, File.ReadAllBytes(written)[^data.Length..]);
        Assert.Equal(["dest.fit", "source.fit"], FilesIn(first));
    }
}
