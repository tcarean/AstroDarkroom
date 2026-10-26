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

using System.Globalization;

namespace CopyWcs.Wpf.Tests;

/// <summary>
/// The window's command line (R-034): <c>--lang</c> read as the console reads it, English
/// when absent or not understood, and the first two existing files among the other arguments
/// as the source and the destination; and the number culture pinned by hand, which the
/// console gets from InvariantGlobalization.
/// </summary>
public class AppTests
{
    [Theory]
    [InlineData(new[] { "--lang", "ro" }, false, null)]
    [InlineData(new[] { "--LANG=RO", "a.fit" }, false, null)]
    [InlineData(new[] { "--lang=en" }, true, null)]
    [InlineData(new[] { "--lang=ro", "--lang", "en" }, true, null)]
    [InlineData(new[] { "a.fit", "b.fit" }, true, null)]
    [InlineData(new string[0], true, null)]
    [InlineData(new[] { "--lang", "fr" }, true, "fr")]
    [InlineData(new[] { "a.fit", "--lang" }, true, "")]
    public void R034_TheWindowTakesItsLanguageFromLang(string[] args, bool english, string? bad)
    {
        UiLanguage lang = App.LanguageFromArgs(args, out string? badValue);

        Assert.Equal(english ? UiLanguage.English : UiLanguage.Romanian, lang);
        Assert.Equal(bad, badValue);
    }

    [Fact]
    public void R034_WithoutArgumentsTheWindowIsEnglish()
    {
        Assert.Equal(UiLanguage.English, App.LanguageFromArgs(null, out string? badValue));
        Assert.Null(badValue);
        Assert.Equal((null, null), App.FilesFromArgs(null));
        Assert.Equal((null, null), App.FilesFromArgs([]));
    }

    [Fact]
    public void R034_TheFirstTwoExistingFilesAreTheSourceAndTheDestination()
    {
        using WindowFixture f = new();
        string missing = f.FilePath("missing.fit");

        Assert.Equal((f.Source, f.Dest), App.FilesFromArgs([f.Source, f.Dest]));
        Assert.Equal((f.Source, f.Dest), App.FilesFromArgs(["--lang", "ro", f.Source, "--lang=en", f.Dest]));
        Assert.Equal((f.Dest, null), App.FilesFromArgs([f.Dest]));
        Assert.Equal((f.Source, null), App.FilesFromArgs([missing, "", "  ", f.Source, f.Dir]));
        // the spaced form takes the next argument as its value, even an existing file
        Assert.Equal((f.Dest, null), App.FilesFromArgs(["--lang", f.Source, f.Dest]));
        // a third file is ignored
        Assert.Equal((f.Source, f.Dest), App.FilesFromArgs([f.Source, f.Dest, f.Source]));
    }

    /// <summary>The facts under the boxes (`100 x 80`) must read as the console's whatever the Windows culture.</summary>
    [Fact]
    public void ForcingInvariantNumbersPinsTheCurrentAndTheDefaultCulture()
    {
        CultureInfo before = CultureInfo.CurrentCulture;
        CultureInfo? beforeDefault = CultureInfo.DefaultThreadCurrentCulture;
        try
        {
            CultureInfo ro = new("ro-RO");
            CultureInfo.DefaultThreadCurrentCulture = ro;
            CultureInfo.CurrentCulture = ro;
            Assert.Equal("0,24", 0.24.ToString("F2", CultureInfo.CurrentCulture));

            App.ForceInvariantNumbers();

            Assert.Equal("0.24", 0.24.ToString("F2", CultureInfo.CurrentCulture));
            Assert.Equal(CultureInfo.InvariantCulture, CultureInfo.DefaultThreadCurrentCulture);
        }
        finally
        {
            CultureInfo.DefaultThreadCurrentCulture = beforeDefault;
            CultureInfo.CurrentCulture = before;
        }
    }
}
