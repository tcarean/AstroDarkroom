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
/// Whether two paths name one file (R-019): the file each opens decides, not the spelling; a
/// path that names no file is never the same as another.
/// </summary>
public class FileIdentityTests
{
    [Fact]
    public void Same_OneFileInAnySpelling_IsTheSameFile()
    {
        using var ws = new TempWorkspace();
        var stack = ws.WriteFile("Stack.fit", [1, 2, 3]);

        Assert.True(FileIdentity.Same(stack, stack));
        Assert.True(FileIdentity.Same(stack, ws.FilePath("stack.FIT")));
        Assert.True(FileIdentity.Same(stack, @"\\?\" + stack));
        Assert.True(FileIdentity.Same(Path.Combine(ws.Dir, "sub", "..", "Stack.fit"), stack));
    }

    [Fact]
    public void Same_TwoFiles_OrAPathThatNamesNoFile_IsNot()
    {
        using var ws = new TempWorkspace();
        var a = ws.WriteFile("a.fit", [1]);
        var b = ws.WriteFile("b.fit", [1]);
        string missing = ws.FilePath("missing.fit");

        Assert.False(FileIdentity.Same(a, b));
        Assert.False(FileIdentity.Same(a, missing));
        Assert.False(FileIdentity.Same(missing, missing));
        Assert.False(FileIdentity.Same(a, ""));
        Assert.False(FileIdentity.Same(" ", " "));
        Assert.False(FileIdentity.Same(ws.Dir, ws.Dir));
    }

    /// <summary>A file another program holds open, for writing and without sharing deletes, is still told.</summary>
    [Fact]
    public void Same_AFileHeldOpenByAnother_IsStillTold()
    {
        using var ws = new TempWorkspace();
        var a = ws.WriteFile("a.fit", [1]);
        using var held = new FileStream(a, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);

        Assert.True(FileIdentity.Same(a, ws.FilePath("A.FIT")));
    }
}
