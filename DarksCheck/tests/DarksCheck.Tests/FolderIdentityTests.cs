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

using System.Diagnostics;
using DarksCheck.Core;

namespace DarksCheck.Tests;

/// <summary>
/// CR-019: the output folder is held against the session by the folders themselves, not by how
/// their paths are spelled.
/// </summary>
public class FolderIdentityTests
{
    /// <summary>
    /// Makes <paramref name="link"/> a junction to <paramref name="target"/>, as <c>mklink /J</c>
    /// does: no privilege is needed, and the test fails loudly when the volume cannot hold one.
    /// </summary>
    internal static void Junction(string link, string target)
    {
        ProcessStartInfo cmd = new("cmd.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        cmd.ArgumentList.Add("/c");
        cmd.ArgumentList.Add("mklink");
        cmd.ArgumentList.Add("/J");
        cmd.ArgumentList.Add(link);
        cmd.ArgumentList.Add(target);
        using Process p = Process.Start(cmd)!;
        string output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
        p.WaitForExit();
        Assert.True(p.ExitCode == 0 && Directory.Exists(link), "mklink /J failed: " + output);
    }

    [Fact]
    public void R026_AFolderIsHeldAgainstAnotherByItsFullPathWhateverItsSpelling()
    {
        using TempWorkspace ws = new();
        string root = Path.Combine(ws.Dir, "Session");
        Directory.CreateDirectory(Path.Combine(root, "darks"));

        Assert.True(FolderIdentity.IsOrInside(root, root));
        Assert.True(FolderIdentity.IsOrInside(root + Path.DirectorySeparatorChar, root));
        Assert.True(FolderIdentity.IsOrInside(root.ToUpperInvariant(), root));
        Assert.True(FolderIdentity.IsOrInside(Path.Combine(root, "darks"), root));
        Assert.True(FolderIdentity.IsOrInside(Path.Combine(root, "darks", "..", "new", "deeper"), root));
        // a folder that does not exist yet is told by its path
        Assert.True(FolderIdentity.IsOrInside(Path.Combine(root, "reports"), root));
        // the \\?\ prefix, which Path.GetFullPath leaves as it is
        Assert.True(FolderIdentity.IsOrInside(@"\\?\" + Path.Combine(root, "reports"), root));
        Assert.True(FolderIdentity.IsOrInside(Path.Combine(root, "reports"), @"\\?\" + root));

        // beside it, above it, and a name that only starts like it
        Assert.False(FolderIdentity.IsOrInside(ws.Dir, root));
        Assert.False(FolderIdentity.IsOrInside(Path.Combine(ws.Dir, "Other"), root));
        Assert.False(FolderIdentity.IsOrInside(root + "-reports", root));
        // a root that does not exist names no folder beyond its path
        Assert.False(FolderIdentity.IsOrInside(ws.Dir, Path.Combine(ws.Dir, "missing")));
        Assert.True(FolderIdentity.IsOrInside(Path.Combine(ws.Dir, "missing", "x"), Path.Combine(ws.Dir, "missing")));
    }

    [Fact]
    public void R026_AJunctionToAFolderIsThatFolder()
    {
        using TempWorkspace ws = new();
        string root = Path.Combine(ws.Dir, "Session");
        Directory.CreateDirectory(Path.Combine(root, "darks"));
        string link = Path.Combine(ws.Dir, "OtherName");
        Junction(link, root);
        try
        {
            // the link itself, a folder inside it that exists, and one that does not exist yet
            Assert.True(FolderIdentity.IsOrInside(link, root));
            Assert.True(FolderIdentity.IsOrInside(Path.Combine(link, "darks"), root));
            Assert.True(FolderIdentity.IsOrInside(Path.Combine(link, "reports", "today"), root));
            // and the other way round: the session opened through the link, the output by its own name
            Assert.True(FolderIdentity.IsOrInside(Path.Combine(root, "reports"), link));
            // the folder that holds both is neither
            Assert.False(FolderIdentity.IsOrInside(Path.Combine(ws.Dir, "reports"), root));
            Assert.False(FolderIdentity.IsOrInside(Path.Combine(ws.Dir, "reports"), link));
        }
        finally
        {
            // the link alone, never what it points at
            Directory.Delete(link);
        }

        Assert.True(Directory.Exists(Path.Combine(root, "darks")));
    }

    /// <summary>
    /// The review of the branch: a junction that leads to a folder inside the session has that folder's
    /// identity, not the session's, and its typed parent is not the session either; only the path the system
    /// resolves it to shows that it lies inside.
    /// </summary>
    [Fact]
    public void R026_AJunctionToAFolderInsideIsInsideToo()
    {
        using TempWorkspace ws = new();
        string root = Path.Combine(ws.Dir, "Session");
        Directory.CreateDirectory(Path.Combine(root, "darks"));
        Directory.CreateDirectory(Path.Combine(root, "reports"));
        Directory.CreateDirectory(Path.Combine(ws.Dir, "Other"));
        string toReports = Path.Combine(ws.Dir, "ToReports"), toDarks = Path.Combine(ws.Dir, "ToDarks"), toOther = Path.Combine(ws.Dir, "ToOther");
        Junction(toReports, Path.Combine(root, "reports"));
        Junction(toDarks, Path.Combine(root, "darks"));
        Junction(toOther, Path.Combine(ws.Dir, "Other"));
        try
        {
            Assert.True(FolderIdentity.IsOrInside(toReports, root));
            Assert.True(FolderIdentity.IsOrInside(Path.Combine(toReports, "today"), root));
            Assert.True(FolderIdentity.IsOrInside(Path.Combine(toReports, "today", "deeper"), root));
            Assert.True(FolderIdentity.IsOrInside(toDarks, root));
            // a junction to a folder beside the session stays outside
            Assert.False(FolderIdentity.IsOrInside(toOther, root));
            Assert.False(FolderIdentity.IsOrInside(Path.Combine(toOther, "today"), root));

            // the folder a path really names, for the folder around it
            // (held against the real path of the folder itself: the temp folder may have a short name on the way)
            Assert.Equal(FolderIdentity.Real(Path.Combine(root, "darks")), FolderIdentity.Real(toDarks), ignoreCase: true);
            Assert.Equal(FolderIdentity.Real(root), DarkOutputs.Around(FolderIdentity.Real(toDarks), "darks"), ignoreCase: true);
            Assert.DoesNotContain(@"\\?\", FolderIdentity.Real(toDarks), StringComparison.Ordinal);
            // a folder that does not exist is its own full path
            Assert.Equal(Path.Combine(ws.Dir, "missing"), FolderIdentity.Real(Path.Combine(ws.Dir, "missing")));
        }
        finally
        {
            Directory.Delete(toReports);
            Directory.Delete(toDarks);
            Directory.Delete(toOther);
        }

        Assert.True(Directory.Exists(Path.Combine(root, "darks")));
        Assert.Empty(Directory.GetFileSystemEntries(Path.Combine(root, "reports")));
    }

    [Fact]
    public void R026_AFolderNamedDarksOrLightsStandsForTheFolderAroundIt()
    {
        Assert.Equal(@"E:\lib\X", DarkOutputs.Around(@"E:\lib\X\darks", "darks"));
        Assert.Equal(@"E:\lib\X", DarkOutputs.Around(@"E:\lib\X\DARKS\", "darks"));
        Assert.Equal(@"E:\S", DarkOutputs.Around(@"E:\S\lights", "lights"));
        // another name stands for nothing
        Assert.Null(DarkOutputs.Around(@"E:\lib\X", "darks"));
        Assert.Null(DarkOutputs.Around(@"E:\lib\darks-old", "darks"));
        // at a drive's root the folder around it is the whole drive: not taken
        Assert.Null(DarkOutputs.Around(@"E:\darks", "darks"));
    }
}
