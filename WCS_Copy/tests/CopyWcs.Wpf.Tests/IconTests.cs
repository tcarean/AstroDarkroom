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
using CopyWcs.Tests;

namespace CopyWcs.Wpf.Tests;

/// <summary>
/// The icon reaches both applications (R-037). It is linked in three places: ApplicationIcon
/// in the two csproj files (the exe, so Explorer and the taskbar) and Icon= in the windows'
/// XAML (the title bar of the open window). All three compile with a wrong path; a broken link
/// would show only under "dotnet CopyWcs.Wpf.dll". After DarksCheck's (ADR 0007).
/// </summary>
[Collection("window")]
public partial class IconTests
{
    private readonly StaFixture _sta;

    public IconTests(StaFixture sta) => _sta = sta;

    /// <summary>The sizes Icon/make_icon.py draws; 20 and 40 are the title bar at 125 % and 250 % DPI.</summary>
    private static readonly int[] Sizes = [16, 20, 24, 32, 40, 48, 64, 256];

    [Fact]
    public void R037_TheWindowCarriesTheIconWithEverySize()
        => _sta.Invoke(() =>
        {
            BitmapFrame icon = Assert.IsAssignableFrom<BitmapFrame>(new MainWindow().Icon);

            int[] sizes = icon.Decoder.Frames.Select(f => f.PixelWidth).OrderBy(n => n).ToArray();
            Assert.Equal(Sizes, sizes);
            Assert.IsAssignableFrom<BitmapFrame>(new AboutWindow().Icon);
        });

    [Fact]
    public void R037_TheLargestFrameIsTheAboutWindowsLogo()
        => _sta.Invoke(() =>
        {
            BitmapSource logo = AppIcon.Largest();

            Assert.Equal(256, logo.PixelWidth);
            Assert.True(logo.IsFrozen);
        });

    /// <summary>The script and the file agree on the sizes, so a redraw cannot silently drop one.</summary>
    [Fact]
    public void R037_TheScriptDrawsTheSizesTheFileHolds()
    {
        string script = File.ReadAllText(Path.Combine(TestData.FindRepoRoot(), "Icon", "make_icon.py"));
        Match m = SizesLine().Match(script);
        Assert.True(m.Success, "make_icon.py has no SIZES = [...] line");
        int[] drawn = m.Groups[1].Value.Split(',').Select(s => int.Parse(s.Trim(), System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        Assert.Equal(Sizes, drawn);
    }

    /// <summary>The console and the window carry the same file, not two copies that part at the first redraw.</summary>
    [Theory]
    [InlineData("CopyWcs.Cli")]
    [InlineData("CopyWcs.Wpf")]
    public void R037_BothExecutablesUseTheSharedIconFile(string project)
    {
        string repo = TestData.FindRepoRoot();
        string dir = Path.Combine(repo, "src", project);
        string csproj = File.ReadAllText(Path.Combine(dir, project + ".csproj"));

        Match m = ApplicationIcon().Match(csproj);
        Assert.True(m.Success, project + ".csproj has no <ApplicationIcon>");

        string icon = Path.GetFullPath(Path.Combine(dir, m.Groups[1].Value.Replace('\\', Path.DirectorySeparatorChar)));
        Assert.Equal(Path.GetFullPath(Path.Combine(repo, "Icon", "copywcs.ico")), icon);
        Assert.True(File.Exists(icon), "missing " + icon);
    }

    [GeneratedRegex(@"<ApplicationIcon>\s*(.+?)\s*</ApplicationIcon>")]
    private static partial Regex ApplicationIcon();

    [GeneratedRegex(@"^SIZES\s*=\s*\[([0-9,\s]+)\]", RegexOptions.Multiline)]
    private static partial Regex SizesLine();
}
