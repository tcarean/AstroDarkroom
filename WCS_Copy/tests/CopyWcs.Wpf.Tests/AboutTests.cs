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

namespace CopyWcs.Wpf.Tests;

/// <summary>
/// The About window shows what the console shows, not a copy of it: the version and the
/// copyright are VersionInfo's and LicenseInfo's, the warranty is the five lines of --help
/// joined (R-035). If one of these links broke, the window could say something else than
/// --help about the same licence. After DarksCheck's (ADR 0007).
/// </summary>
public class AboutTests
{
    [Fact]
    public void R035_TheBannerAndCopyrightAreTheOnesEveryRunPrints()
    {
        AboutViewModel vm = new();

        Assert.Equal(VersionInfo.Banner, vm.Banner);
        Assert.Equal(LicenseInfo.Copyright, vm.Copyright);
        Assert.Contains(VersionInfo.Value, vm.Banner, StringComparison.Ordinal);
        Assert.StartsWith("copywcs ", vm.Banner, StringComparison.Ordinal);
    }

    /// <summary>GPLv3 5(d): the warranty notice and the pointer to the licence, the --help lines joined into one paragraph, in each language.</summary>
    [Theory]
    [InlineData(false, "ABSOLUTELY NO WARRANTY")]
    [InlineData(true, "FARA NICIO GARANTIE")]
    public void R035_TheWarrantyIsTheConsolesHelpTextJoinedIntoAParagraph(bool romanian, string words)
    {
        using IDisposable _ = Ui.Use(romanian ? UiLanguage.Romanian : UiLanguage.English);
        AboutViewModel vm = new();

        Assert.Equal(string.Join(" ", LicenseInfo.WarrantyLines), vm.Warranty);
        Assert.Contains(words, vm.Warranty, StringComparison.Ordinal);
        Assert.Contains("GPLv3", vm.Warranty, StringComparison.Ordinal);
        Assert.Contains("LICENSE", vm.Warranty, StringComparison.Ordinal);
        Assert.Equal(LicenseInfo.LicenseUrl, vm.LicenseUrl);
        Assert.Equal("https://www.gnu.org/licenses/gpl-3.0.html", vm.LicenseUrl);
        Assert.Contains("<" + vm.LicenseUrl + ">", vm.Warranty, StringComparison.Ordinal);

        // the console prints exactly those five lines under its usage
        using StringWriter stdout = new();
        Assert.Equal(0, CopyWcsCli.Run(["--help"], stdout, new StringWriter()));
        string[] printed = stdout.ToString().ReplaceLineEndings("\n").TrimEnd('\n').Split('\n');
        Assert.Equal(5, LicenseInfo.WarrantyLines.Count);
        Assert.Equal(LicenseInfo.WarrantyLines, printed[^5..]);
    }

    [Theory]
    [InlineData(false, "About CopyWcs", "Close")]
    [InlineData(true, "Despre CopyWcs", "Inchide")]
    public void R035_EveryHeadingIsWrittenInTheLanguage(bool romanian, string title, string close)
    {
        using IDisposable _ = Ui.Use(romanian ? UiLanguage.Romanian : UiLanguage.English);
        AboutViewModel vm = new();

        Assert.Equal(title, vm.Title);
        Assert.False(string.IsNullOrWhiteSpace(vm.Tagline));
        Assert.False(string.IsNullOrWhiteSpace(vm.LicenceHeading));
        Assert.False(string.IsNullOrWhiteSpace(vm.LicenseLinkLabel));
        Assert.Equal(close, new TExtension { En = "Close", Ro = "Inchide" }.ProvideValue(null!));
    }
}
