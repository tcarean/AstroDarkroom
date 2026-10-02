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

using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace AstroPhotoAnalyzer.Tests;

/// <summary>
/// Versiunea se incrementeaza manual. Sursa de adevar e <c>&lt;Version&gt;</c> din
/// Directory.Build.props - <see cref="VersionInfo"/> o citeste din assembly, nu o
/// hardcodeaza. Testele astea verifica plumbing-ul si ca banner-ul chiar apare pe prima
/// linie a fiecarei rulari.
/// </summary>
public class VersionTests
{
    static string RepoDir([CallerFilePath] string thisFile = "")
        => Path.GetDirectoryName(Path.GetDirectoryName(thisFile))!;

    static string BuildProps()
        => File.ReadAllText(Path.Combine(RepoDir(), "Directory.Build.props"));

    [Fact]
    public void R120_VersionIsAThreePartNumber()
    {
        Assert.Matches(@"^\d+\.\d+\.\d+$", VersionInfo.Value);
    }

    [Fact]
    public void R120_TheVersionComesFromDirectoryBuildProps()
    {
        var m = Regex.Match(BuildProps(), @"<Version>([^<]+)</Version>");

        Assert.True(m.Success, "Directory.Build.props nu are <Version>");
        Assert.Equal(m.Groups[1].Value.Trim(), VersionInfo.Value);
    }

    /// <summary>
    /// Toate assembly-urile poarta aceeasi versiune. VersionInfo o citeste din Core, deci
    /// un &lt;Version&gt; ramas intr-un csproj ar trece neobservat de testul de mai sus si
    /// ar da un exe cu alta versiune in proprietatile fisierului decat cea tiparita.
    /// </summary>
    [Theory]
    [InlineData("AstroPhotoAnalyzer")]
    [InlineData("AstroPhotoAnalyzer.Core")]
    [InlineData("AstroPhotoAnalyzer.Wpf")]
    public void R120_NoProjectOverridesTheSharedVersionOrCopyright(string project)
    {
        string path = Path.Combine(RepoDir(), project, project + ".csproj");
        if (!File.Exists(path)) return;   // proiectul WPF apare abia in etapa 5

        string csproj = File.ReadAllText(path);

        Assert.DoesNotContain("<Version>", csproj);
        Assert.DoesNotContain("<Copyright>", csproj);
    }

    /// <summary>Nu trebuie sa ramana "1.0.0.0" sau "1.0.0+abc123" din assembly.</summary>
    [Fact]
    public void R120_TheVersionIsNotTheFourPartOrSourceTaggedForm()
    {
        Assert.DoesNotContain("+", VersionInfo.Value);
        Assert.Equal(3, VersionInfo.Value.Split('.').Length);
    }

    [Fact]
    public void R120_TheCopyrightComesFromDirectoryBuildProps()
    {
        var m = Regex.Match(BuildProps(), @"<Copyright>([^<]+)</Copyright>");

        Assert.True(m.Success, "Directory.Build.props nu are <Copyright>");
        Assert.Equal(m.Groups[1].Value.Trim(), VersionInfo.Copyright);
    }

    [Fact]
    public void R120_TheCopyrightNamesTheAuthorAndTheYear()
    {
        Assert.Equal("Copyright (C) 2026 Tudor Carean", VersionInfo.Copyright);
    }

    // ------------------------------------------------------------ banner-ul

    [Fact]
    public void R001_TheBannerIsTheNameAndTheVersion()
    {
        Assert.Equal($"AstroPhotoAnalyzer {VersionInfo.Value}", VersionInfo.Banner);
    }

    static string[] Lines(params string[] args)
    {
        var stdout = new StringWriter();
        AnalyzerCli.Run(args, stdout, new StringWriter());
        return stdout.ToString().Replace("\r\n", "\n").Split('\n');
    }

    static string FirstLine(params string[] args) => Lines(args)[0];

    static string SecondLine(params string[] args) => Lines(args)[1];

    [Fact]
    public void R001_HelpStartsWithTheBanner()
    {
        Assert.Equal(VersionInfo.Banner, FirstLine("--help"));
        Assert.Equal(VersionInfo.Banner, FirstLine());
    }

    /// <summary>
    /// GPLv3 vrea ca notita de copyright sa se vada la fiecare rulare, deci sta pe linia
    /// a doua peste tot unde sta si banner-ul pe prima - inclusiv la --help si la erori.
    /// </summary>
    [Fact]
    public void R001_EveryRunPrintsTheCopyrightOnTheSecondLine()
    {
        using var ws = new TempWorkspace();

        Assert.Equal(VersionInfo.Copyright, SecondLine("--help"));
        Assert.Equal(VersionInfo.Copyright, SecondLine(ws.PathTo("nu-exista.tif")));
        Assert.Equal(VersionInfo.Copyright, SecondLine("--optiune-inventata"));
    }

    /// <summary>Si o rulare care esueaza spune ce versiune a esuat.</summary>
    [Fact]
    public void R001_AFailedRunStillPrintsTheBanner()
    {
        using var ws = new TempWorkspace();

        Assert.Equal(VersionInfo.Banner, FirstLine(ws.PathTo("nu-exista.tif")));
        Assert.Equal(VersionInfo.Banner, FirstLine("--optiune-inventata"));
    }

    /// <summary>
    /// GPLv3 sectiunea 5(d): un program care afiseaza notite legale trebuie sa afiseze si
    /// lipsa garantiei. Nu se poate pune sub banner - ar ingrosa fiecare raport - deci sta
    /// in --help, si de acolo trimite la textul complet.
    /// </summary>
    [Fact]
    public void R004_HelpCarriesTheWarrantyDisclaimerAndPointsAtTheLicense()
    {
        string help = string.Join("\n", Lines("--help"));

        Assert.Contains("FARA NICIO GARANTIE", help);
        Assert.Contains("GPLv3", help);
        Assert.Contains("LICENSE", help);
        Assert.Contains("gnu.org/licenses/gpl-3.0.html", help);
    }

    [Fact]
    public void R001_TheBannerIsPrintedExactlyOnce()
    {
        var stdout = new StringWriter();
        AnalyzerCli.Run(new[] { "--help" }, stdout, new StringWriter());

        int count = Regex.Matches(stdout.ToString(), Regex.Escape(VersionInfo.Banner)).Count;
        Assert.Equal(1, count);
    }
}
