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

namespace FocalLenghChecker.Tests;

public class NefFileFinderTests
{
    private static IReadOnlyList<string> NamesFound(TempFolder folder) =>
        new NefFileFinder().Find(folder.Path).Select(Path.GetFileName).ToList()!;

    [Fact]
    public void An_empty_folder_yields_nothing()
    {
        using var folder = new TempFolder();

        Assert.Empty(new NefFileFinder().Find(folder.Path));
    }

    [Fact]
    public void Nested_folders_are_searched()
    {
        using var folder = new TempFolder();

        folder.AddFile("top.NEF");
        folder.AddFile(Path.Combine("one", "middle.NEF"));
        folder.AddFile(Path.Combine("one", "two", "three", "deep.NEF"));

        Assert.Equal(new[] { "deep.NEF", "middle.NEF", "top.NEF" }, NamesFound(folder).OrderBy(n => n));
    }

    [Theory]
    [InlineData("a.NEF")]
    [InlineData("b.nef")]
    [InlineData("c.Nef")]
    [InlineData("d.nEf")]
    public void The_extension_is_matched_whatever_its_case(string name)
    {
        using var folder = new TempFolder();

        folder.AddFile(name);

        Assert.Equal(name, Assert.Single(NamesFound(folder)));
    }

    [Theory]
    [InlineData("photo.jpg")]
    [InlineData("photo.CR2")]
    [InlineData("photo.tif")]
    [InlineData("notes.txt")]
    [InlineData("photo.nef_backup")]
    [InlineData("photo.nefx")]
    [InlineData("NEF")]
    [InlineData("photo.NEF.bak")]
    public void Anything_that_is_not_a_NEF_is_left_alone(string name)
    {
        using var folder = new TempFolder();

        folder.AddFile(name);

        Assert.Empty(new NefFileFinder().Find(folder.Path));
    }

    [Fact]
    public void NEFs_are_picked_out_from_among_other_files()
    {
        using var folder = new TempFolder();

        folder.AddFile("keep.NEF");
        folder.AddFile("skip.jpg");
        folder.AddFile(Path.Combine("sub", "keep-too.nef"));
        folder.AddFile(Path.Combine("sub", "skip.xmp"));

        Assert.Equal(new[] { "keep-too.nef", "keep.NEF" }, NamesFound(folder).OrderBy(n => n));
    }

    [Fact]
    public void Results_come_back_in_a_stable_order()
    {
        using var folder = new TempFolder();

        folder.AddFile("c.NEF");
        folder.AddFile("a.NEF");
        folder.AddFile("b.NEF");

        Assert.Equal(NamesFound(folder), NamesFound(folder));
        Assert.Equal(new[] { "a.NEF", "b.NEF", "c.NEF" }, NamesFound(folder));
    }

    [Fact]
    public void A_blank_folder_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new NefFileFinder().Find("   ").ToList());
    }

    [Theory]
    [InlineData("photo.NEF", true)]
    [InlineData("photo.nef", true)]
    [InlineData("photo.jpg", false)]
    [InlineData("photo", false)]
    public void IsNef_recognises_the_extension_on_its_own(string name, bool expected)
    {
        Assert.Equal(expected, NefFileFinder.IsNef(name));
    }
}
