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

// IconTests.cs - the icon gets where it should, in both applications.
//
// The icon is wired in three places: <ApplicationIcon> in the two csproj files (the exe, so
// Explorer and pinning to the taskbar) and Icon= in MainWindow.xaml (the title bar and the
// open window's taskbar entry). All three compile even with a wrong path to a resource, and
// without Icon= the window still shows the right icon when started from its exe - so a
// broken link would show only under "dotnet FlatCheck.Wpf.dll". Hence the tests.

using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;

namespace FlatCheck.Wpf.Tests;

[Collection("fereastra")]
public class IconTests
{
    readonly StaFixture _sta;

    public IconTests(StaFixture sta) => _sta = sta;

    static string RepoDir([CallerFilePath] string thisFile = "")
        => Path.GetDirectoryName(Path.GetDirectoryName(thisFile))!;

    /// <summary>
    /// The sizes Icon/make_icon.py draws separately. 20 and 40 are the title bar's at 125%
    /// and 250% DPI; if they are missing, Windows scales another one and it comes out blurred.
    /// </summary>
    static readonly int[] Sizes = { 16, 20, 24, 32, 40, 48, 64, 256 };

    [Fact]
    public void TheWindowCarriesTheIconWithEverySize()
        => _sta.Invoke(() =>
        {
            var icon = Assert.IsAssignableFrom<BitmapFrame>(new MainWindow().Icon);

            int[] sizes = icon.Decoder.Frames.Select(f => f.PixelWidth).OrderBy(n => n).ToArray();
            Assert.Equal(Sizes, sizes);
        });

    /// <summary>
    /// The console and the window carry the same file, not two copies that would drift apart
    /// at the first change to the drawing.
    /// </summary>
    [Theory]
    [InlineData("FlatCheck")]
    [InlineData("FlatCheck.Wpf")]
    public void BothExecutablesUseTheSharedIconFile(string project)
    {
        string dir = Path.Combine(RepoDir(), project);
        string csproj = File.ReadAllText(Path.Combine(dir, project + ".csproj"));

        Match m = Regex.Match(csproj, @"<ApplicationIcon>\s*(.+?)\s*</ApplicationIcon>");
        Assert.True(m.Success, project + ".csproj nu are <ApplicationIcon>");

        string icon = Path.GetFullPath(Path.Combine(dir, m.Groups[1].Value.Replace('\\', Path.DirectorySeparatorChar)));
        Assert.Equal(Path.GetFullPath(Path.Combine(RepoDir(), "Icon", "flatcheck.ico")), icon);
        Assert.True(File.Exists(icon), "lipseste " + icon);
    }
}
