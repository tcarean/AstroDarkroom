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

using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;
using DarksCheck.Tests;

namespace DarksCheck.Wpf.Tests;

/// <summary>
/// The icon reaches both applications. It is linked in three places: ApplicationIcon in the
/// two csproj files (the exe, so Explorer and the taskbar) and Icon= in MainWindow.xaml (the
/// title bar of the open window). All three compile with a wrong path; a broken link would
/// show only under "dotnet DarksCheck.Wpf.dll".
/// </summary>
[Collection("window")]
public partial class IconTests
{
    private readonly StaFixture _sta;

    public IconTests(StaFixture sta) => _sta = sta;

    /// <summary>The sizes Icon/make_icon.py draws; 20 and 40 are the title bar at 125 % and 250 % DPI.</summary>
    private static readonly int[] Sizes = [16, 20, 24, 32, 40, 48, 64, 256];

    [Fact]
    public void TheWindowCarriesTheIconWithEverySize()
        => _sta.Invoke(() =>
        {
            BitmapFrame icon = Assert.IsAssignableFrom<BitmapFrame>(new MainWindow().Icon);

            int[] sizes = icon.Decoder.Frames.Select(f => f.PixelWidth).OrderBy(n => n).ToArray();
            Assert.Equal(Sizes, sizes);
        });

    [Fact]
    public void TheLargestFrameIsTheAboutWindowsLogo()
        => _sta.Invoke(() =>
        {
            BitmapSource logo = AppIcon.Largest();

            Assert.Equal(256, logo.PixelWidth);
            Assert.True(logo.IsFrozen);
        });

    /// <summary>The console and the window carry the same file, not two copies that part at the first redraw.</summary>
    [Theory]
    [InlineData("DarksCheck.Cli")]
    [InlineData("DarksCheck.Wpf")]
    public void BothExecutablesUseTheSharedIconFile(string project)
    {
        string repo = TestData.FindRepoRoot();
        string dir = Path.Combine(repo, "src", project);
        string csproj = File.ReadAllText(Path.Combine(dir, project + ".csproj"));

        Match m = ApplicationIcon().Match(csproj);
        Assert.True(m.Success, project + ".csproj has no <ApplicationIcon>");

        string icon = Path.GetFullPath(Path.Combine(dir, m.Groups[1].Value.Replace('\\', Path.DirectorySeparatorChar)));
        Assert.Equal(Path.GetFullPath(Path.Combine(repo, "Icon", "darkscheck.ico")), icon);
        Assert.True(File.Exists(icon), "missing " + icon);
    }

    [GeneratedRegex(@"<ApplicationIcon>\s*(.+?)\s*</ApplicationIcon>")]
    private static partial Regex ApplicationIcon();
}
