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

using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using ImageVisualCompare.Wpf;

namespace ImageVisualCompare.Tests;

/// <summary>
/// Versiunea se incrementeaza manual. Sursa de adevar e <c>&lt;Version&gt;</c> din csproj -
/// <see cref="VersionInfo"/> o citeste din assembly, nu o hardcodeaza. Testele astea verifica
/// plumbing-ul, si ca notitele cerute de GPLv3 chiar exista in textele pe care le arata
/// aplicatia.
/// </summary>
public class VersionTests
{
    private static string RepoDir([CallerFilePath] string thisFile = "") =>
        Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(thisFile)))!;

    private static string AppCsproj() => File.ReadAllText(Path.Combine(
        RepoDir(), "src", "ImageVisualCompare.Wpf", "ImageVisualCompare.Wpf.csproj"));

    [Fact]
    public void VersionIsAThreePartNumber()
    {
        Assert.Matches(@"^\d+\.\d+\.\d+$", VersionInfo.Value);
    }

    /// <summary>
    /// Verifica plumbing-ul csproj -> assembly -> VersionInfo. Daca cineva scoate
    /// &lt;Version&gt; sau MSBuild inceteaza sa mai propage InformationalVersion, aici se vede.
    /// </summary>
    [Fact]
    public void TheVersionComesFromTheCsproj()
    {
        Match m = Regex.Match(AppCsproj(), @"<Version>([^<]+)</Version>");

        Assert.True(m.Success, "csproj-ul nu are <Version>");
        Assert.Equal(m.Groups[1].Value.Trim(), VersionInfo.Value);
    }

    /// <summary>Nu trebuie sa ramana "1.0.0.0" sau "1.0.0+abc123" din assembly.</summary>
    [Fact]
    public void TheVersionIsNotTheFourPartOrSourceTaggedForm()
    {
        Assert.DoesNotContain("+", VersionInfo.Value);
        Assert.Equal(3, VersionInfo.Value.Split('.').Length);
    }

    /// <summary>
    /// Copyright-ul vine din &lt;Copyright&gt;, la fel cum versiunea vine din &lt;Version&gt;.
    /// Daca cineva il scoate, ramane fallback-ul din cod - si aici se vede.
    /// </summary>
    [Fact]
    public void TheCopyrightComesFromTheCsproj()
    {
        Match m = Regex.Match(AppCsproj(), @"<Copyright>([^<]+)</Copyright>");

        Assert.True(m.Success, "csproj-ul nu are <Copyright>");
        Assert.Equal(m.Groups[1].Value.Trim(), VersionInfo.Copyright);
    }

    [Fact]
    public void TheCopyrightNamesTheAuthorAndTheYear()
    {
        Assert.Equal("Copyright (C) 2026 Tudor Carean", VersionInfo.Copyright);
    }

    [Fact]
    public void TheCsprojDeclaresTheLicence()
    {
        Assert.Contains("<PackageLicenseExpression>GPL-3.0-or-later</PackageLicenseExpression>", AppCsproj());
    }

    [Fact]
    public void TheBannerIsTheNameAndTheVersion()
    {
        Assert.Equal($"Image Visual Compare {VersionInfo.Value}", VersionInfo.Banner);
    }

    [Fact]
    public void AboutStartsWithTheBannerAndTheCopyright()
    {
        string[] lines = VersionInfo.About.Replace("\r\n", "\n").Split('\n');

        Assert.Equal(VersionInfo.Banner, lines[0]);
        Assert.Equal(VersionInfo.Copyright, lines[1]);
    }

    /// <summary>
    /// GPLv3 sectiunea 5(d): un program cu interfata interactiva trebuie sa afiseze si lipsa
    /// garantiei, nu doar copyright-ul, si sa spuna unde e licenta completa.
    /// </summary>
    [Fact]
    public void AboutCarriesTheWarrantyDisclaimerAndPointsAtTheLicence()
    {
        Assert.Contains("ABSOLUTELY NO WARRANTY", VersionInfo.About);
        Assert.Contains("GNU General Public License", VersionInfo.About);
        Assert.Contains("LICENSE", VersionInfo.About);
        Assert.Contains("gnu.org/licenses/gpl-3.0.html", VersionInfo.About);
    }
}
