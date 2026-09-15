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

// IconTests.cs - icoana ajunge acolo unde trebuie, la ambele aplicatii.
//
// Icoana e legata in trei locuri: <ApplicationIcon> in cele doua csproj-uri (exe-ul, deci
// Explorer si fixarea in taskbar) si Icon= in MainWindow.xaml (bara de titlu si taskbar-ul
// ferestrei deschise). Toate trei compileaza si cu o cale gresita catre o resursa, iar
// fara Icon= fereastra arata totusi icoana corecta cand e pornita din exe-ul ei - deci o
// legatura rupta s-ar vedea abia sub "dotnet FlatCheck.Wpf.dll". De aici testele.

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
    /// Dimensiunile desenate separat de Icon/make_icon.py. 20 si 40 sunt cele din bara de
    /// titlu la 125% si 250% DPI; daca lipsesc, Windows scaleaza alta si iese neclar.
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
    /// Consola si fereastra poarta acelasi fisier, nu doua copii care sa se desparta la
    /// prima modificare a desenului.
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
