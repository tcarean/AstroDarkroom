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

    /// <summary>
    /// CR-080: a folder was opened by its path as typed, which Windows refuses from 260 characters on unless
    /// long paths are switched on, so a link that far down the path was taken for a folder that does not exist
    /// yet, and a session that deep had no identity.
    /// </summary>
    [Fact]
    public void R026_ALinkFarDownALongPathIsFollowedAndADeepFolderHasItsIdentity()
    {
        using TempWorkspace ws = new();
        string root = Path.Combine(ws.Dir, "Session");
        Directory.CreateDirectory(Path.Combine(root, "darks"));
        string[] chain = [.. Enumerable.Range(0, 6).Select(i => new string((char)('b' + i), 50))];
        string deep = Path.Combine([ws.Dir, .. chain]);
        Directory.CreateDirectory(deep);

        // a junction made at a short path (mklink takes no long one), then moved to the end of the chain
        string shortParent = Path.Combine(ws.Dir, "a");
        Directory.CreateDirectory(shortParent);
        Junction(Path.Combine(shortParent, "J"), Path.Combine(root, "darks"));
        string moved = Path.Combine(deep, "a");
        Directory.Move(shortParent, moved);
        string link = Path.Combine(moved, "J");
        Assert.True(link.Length > 300, "the link's path is long: " + link.Length);

        // the other way round: the protected folder is the deep one, and the output reaches it through a
        // short link to the top of the chain
        string deepSession = Path.Combine(deep, "DeepSession");
        Directory.CreateDirectory(Path.Combine(deepSession, "darks"));
        string up = Path.Combine(ws.Dir, "Up");
        Junction(up, Path.Combine(ws.Dir, chain[0]));
        string through = Path.Combine([up, .. chain[1..], "DeepSession"]);
        Assert.True(through.Length > 300 && deepSession.Length > 300);
        try
        {
            Assert.True(FolderIdentity.IsOrInside(link, root));
            Assert.True(FolderIdentity.IsOrInside(Path.Combine(link, "reports"), root));
            // a folder that far down which is no link stays outside
            Assert.False(FolderIdentity.IsOrInside(Path.Combine(moved, "reports"), root));

            Assert.True(FolderIdentity.IsOrInside(Path.Combine(through, "reports"), deepSession));
            Assert.Equal(FolderIdentity.Real(deepSession), FolderIdentity.Real(through), ignoreCase: true);
            Assert.False(FolderIdentity.IsOrInside(Path.Combine(up, "reports"), deepSession));
        }
        finally
        {
            Directory.Delete(link);
            Directory.Delete(up);
        }

        Assert.True(Directory.Exists(Path.Combine(root, "darks")));
        Assert.True(Directory.Exists(Path.Combine(deepSession, "darks")));
    }

    /// <summary>CR-080: the form a long path is opened in; a short path, and one already in a device form, stay as they are.</summary>
    [Fact]
    public void R026_ALongPathIsOpenedInTheFormThatTakesIt()
    {
        string tail = string.Concat(Enumerable.Repeat(@"\" + new string('x', 40), 6));
        Assert.Equal(@"E:\S\darks", FolderIdentity.Extended(@"E:\S\darks"));
        Assert.Equal(@"\\?\E:\S" + tail, FolderIdentity.Extended(@"E:\S" + tail));
        Assert.Equal(@"\\?\UNC\server\share" + tail, FolderIdentity.Extended(@"\\server\share" + tail));
        Assert.Equal(@"\\?\E:\S" + tail, FolderIdentity.Extended(@"\\?\E:\S" + tail));
        Assert.Equal(@"\\.\pipe" + tail, FolderIdentity.Extended(@"\\.\pipe" + tail));
        // the limit is Windows' old one for a folder's plain path: 248 characters
        Assert.Equal(@"E:\" + new string('x', 244), FolderIdentity.Extended(@"E:\" + new string('x', 244)));
        Assert.Equal(@"\\?\E:\" + new string('x', 245), FolderIdentity.Extended(@"E:\" + new string('x', 245)));
    }

    /// <summary>
    /// The second review of CR-080: the list of folders nothing is written into, held on its own. A folder is
    /// told from another by what the system says of it, and where the system says nothing (a volume that
    /// cannot name a link's target, a file system without a file index) only the paths in the list are left:
    /// each folder and each of its session subfolders is in it by its own path and by the path it really
    /// has. Neither failure can be made in a test, so the list is what is held.
    /// </summary>
    [Fact]
    public void R026_TheListHoldsEachFolderAndItsSubfoldersByTheirOwnPathsAndTheirRealOnes()
    {
        using SessionFixture s = new SessionFixture().WithDarkSeries(2, i => new SyntheticDark { Seed = 100 + i });
        using SessionFixture cal = new SessionFixture().WithDarkSeries(2, i => new SyntheticDark { Seed = 400 + i }, biases: 2);
        using SessionFixture lib = new SessionFixture().WithDarkSeries(2, i => new SyntheticDark { Seed = 200 + i }, inSubfolder: false);
        using SessionFixture other = new SessionFixture().WithLights(2, i => new SyntheticLight { Seed = 300 + i });
        using SessionFixture partner = new SessionFixture().WithDarkSeries(2, i => new SyntheticDark { Seed = 500 + i });
        using TempWorkspace ws = new();
        using TempWorkspace linked = new();
        string biases = Path.Combine(s.Root, "biases"), libLink = Path.Combine(ws.Dir, "biblioteca"), lightsLink = Path.Combine(ws.Dir, "luminile");
        string linkedDarks = Path.Combine(linked.Dir, "darks");
        Junction(biases, cal.BiasesDir);
        Junction(libLink, lib.Root);
        Junction(lightsLink, other.LightsDir);
        Junction(linkedDarks, partner.DarksDir);
        try
        {
            static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

            // a session: its subfolders by their own paths, a link among them or not
            IReadOnlyList<(string Folder, DarkOutputs.OutFolder Which)> session = DarkOutputs.ReadOnlyFolders(SessionFolder.Open(s.Root), null);
            Assert.Equal(s.Root, session[0].Folder);
            Assert.Contains(session, f => Same(f.Folder, FolderIdentity.Real(s.DarksDir)));
            Assert.Contains(session, f => Same(f.Folder, FolderIdentity.Real(cal.BiasesDir)));
            Assert.All(session, f => Assert.Equal(DarkOutputs.OutFolder.Session, f.Which));

            // the biases stand for no folder around them: the session they were linked from is not in the list
            Assert.DoesNotContain(session, f => Same(f.Folder, FolderIdentity.Real(cal.Root)));

            // a library, a partner and a bare folder of lights reached through a link: the folder each really is
            Assert.NotEqual(FolderIdentity.Real(libLink), libLink, StringComparer.OrdinalIgnoreCase);
            Assert.Contains(DarkOutputs.ReadOnlyFolders(SessionFolder.Open(libLink), null), f => Same(f.Folder, FolderIdentity.Real(lib.Root)) && f.Which == DarkOutputs.OutFolder.Session);
            Assert.Contains(DarkOutputs.ReadOnlyFolders(SessionFolder.Open(s.Root), libLink), f => Same(f.Folder, FolderIdentity.Real(lib.Root)) && f.Which == DarkOutputs.OutFolder.Partner);
            IReadOnlyList<(string Folder, DarkOutputs.OutFolder Which)> chosen = DarkOutputs.ReadOnlyFolders(SessionFolder.Open(s.Root, lightsFrom: lightsLink), null);
            Assert.Contains(chosen, f => Same(f.Folder, FolderIdentity.Real(other.LightsDir)) && f.Which == DarkOutputs.OutFolder.ChosenLights);
            Assert.Contains(chosen, f => Same(f.Folder, FolderIdentity.Real(other.Root)) && f.Which == DarkOutputs.OutFolder.ChosenLights);

            // the folders as they were given come first, so a refusal names the folder that was typed: a
            // session whose darks\ leads into the partner's is refused there as the partner's folder
            IReadOnlyList<(string Folder, DarkOutputs.OutFolder Which)> shared = DarkOutputs.ReadOnlyFolders(SessionFolder.Open(linked.Dir), partner.Root);
            Assert.Equal(DarkOutputs.OutFolder.Partner, shared.First(f => Same(f.Folder, partner.Root)).Which);
            Assert.Equal(1, shared.Count(f => Same(f.Folder, partner.Root)));
        }
        finally
        {
            foreach (string link in new[] { biases, libLink, lightsLink, linkedDarks })
            {
                Directory.Delete(link);
            }
        }
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
