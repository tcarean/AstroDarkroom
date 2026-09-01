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

namespace FlatCheck.Tests;

/// <summary>
/// Versiunea se incrementeaza manual. Pe partea de C# sursa de adevar e <c>&lt;Version&gt;</c>
/// din csproj - <see cref="VersionInfo"/> o citeste din assembly, nu o hardcodeaza - dar
/// flatcheck.py isi tine propria copie, si aia poate ramane in urma. Testele astea le
/// leaga, si verifica si ca banner-ul chiar apare pe prima linie a fiecarei rulari.
/// </summary>
public class VersionTests
{
    static string RepoDir([CallerFilePath] string thisFile = "")
        => Path.GetDirectoryName(Path.GetDirectoryName(thisFile))!;

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
        string csproj = File.ReadAllText(
            Path.Combine(RepoDir(), "FlatCheck", "FlatCheck.csproj"));

        var m = Regex.Match(csproj, @"<Version>([^<]+)</Version>");

        Assert.True(m.Success, "csproj-ul nu are <Version>");
        Assert.Equal(m.Groups[1].Value.Trim(), VersionInfo.Value);
    }

    /// <summary>Nu trebuie sa ramana "1.0.1.0" sau "1.0.1+abc123" din assembly.</summary>
    [Fact]
    public void TheVersionIsNotTheFourPartOrSourceTaggedForm()
    {
        Assert.DoesNotContain("+", VersionInfo.Value);
        Assert.Equal(3, VersionInfo.Value.Split('.').Length);
    }

    /// <summary>
    /// Copyright-ul vine din &lt;Copyright&gt; din csproj, la fel cum versiunea vine din
    /// &lt;Version&gt;. Daca cineva il scoate, ramane fallback-ul din cod - si aici se vede.
    /// </summary>
    [Fact]
    public void TheCopyrightComesFromTheCsproj()
    {
        string csproj = File.ReadAllText(
            Path.Combine(RepoDir(), "FlatCheck", "FlatCheck.csproj"));

        var m = Regex.Match(csproj, @"<Copyright>([^<]+)</Copyright>");

        Assert.True(m.Success, "csproj-ul nu are <Copyright>");
        Assert.Equal(m.Groups[1].Value.Trim(), VersionInfo.Copyright);
    }

    /// <summary>Python-ul tipareste aceeasi notita - crosscheck.py compara linie cu linie.</summary>
    [Fact]
    public void PythonReportsTheSameCopyrightAsTheCSharp()
    {
        string py = File.ReadAllText(Path.Combine(RepoDir(), "flatcheck.py"));

        var m = Regex.Match(py, @"^__copyright__\s*=\s*""([^""]+)""", RegexOptions.Multiline);

        Assert.True(m.Success, "flatcheck.py nu are __copyright__");
        Assert.Equal(VersionInfo.Copyright, m.Groups[1].Value);
    }

    [Fact]
    public void PythonReportsTheSameVersionAsTheCSharp()
    {
        string py = File.ReadAllText(Path.Combine(RepoDir(), "flatcheck.py"));

        var m = Regex.Match(py, @"^__version__\s*=\s*""([^""]+)""", RegexOptions.Multiline);

        Assert.True(m.Success, "flatcheck.py nu are __version__");
        Assert.Equal(VersionInfo.Value, m.Groups[1].Value);
    }

    // ------------------------------------------------------------ banner-ul

    [Fact]
    public void TheBannerIsTheNameAndTheVersion()
    {
        Assert.Equal($"flatcheck {VersionInfo.Value}", VersionInfo.Banner);
    }

    static string[] Lines(params string[] args)
    {
        var stdout = new StringWriter();
        FlatCheckCli.Run(args, stdout, new StringWriter());
        return stdout.ToString().Replace("\r\n", "\n").Split('\n');
    }

    static string FirstLine(params string[] args) => Lines(args)[0];

    static string SecondLine(params string[] args) => Lines(args)[1];

    [Fact]
    public void EveryModePrintsTheBannerOnTheFirstLine()
    {
        using var ws = new TempWorkspace();
        string flat = ws.WriteFile("f.fit",
            FitsTestData.BuildFits(128, 128, FitsTestData.Radial(128, 128)));
        string png = ws.PathTo("out.png");

        Assert.Equal(VersionInfo.Banner, FirstLine(flat, "-o", png));
        Assert.Equal(VersionInfo.Banner, FirstLine(flat, "--ratio", flat, "-o", png));
        Assert.Equal(VersionInfo.Banner, FirstLine(flat, "--light", "-o", png));
        Assert.Equal(VersionInfo.Banner, FirstLine(flat, "--expo"));
    }

    /// <summary>
    /// GPLv3 vrea ca notita de copyright sa se vada la fiecare rulare, deci sta pe linia
    /// a doua peste tot unde sta si banner-ul pe prima - inclusiv la --help si la erori.
    /// </summary>
    [Fact]
    public void EveryModePrintsTheCopyrightOnTheSecondLine()
    {
        using var ws = new TempWorkspace();
        string flat = ws.WriteFile("f.fit",
            FitsTestData.BuildFits(128, 128, FitsTestData.Radial(128, 128)));
        string png = ws.PathTo("out.png");

        Assert.Equal(VersionInfo.Copyright, SecondLine(flat, "-o", png));
        Assert.Equal(VersionInfo.Copyright, SecondLine(flat, "--ratio", flat, "-o", png));
        Assert.Equal(VersionInfo.Copyright, SecondLine(flat, "--light", "-o", png));
        Assert.Equal(VersionInfo.Copyright, SecondLine(flat, "--expo"));
        Assert.Equal(VersionInfo.Copyright, SecondLine("--help"));
        Assert.Equal(VersionInfo.Copyright, SecondLine(ws.PathTo("nu-exista.fit")));
    }

    [Fact]
    public void TheCopyrightNamesTheAuthorAndTheYear()
    {
        Assert.Equal("Copyright (C) 2026 Tudor Carean", VersionInfo.Copyright);
    }

    /// <summary>
    /// GPLv3 sectiunea 5(d): un program care afiseaza notite legale trebuie sa afiseze si
    /// lipsa garantiei. Nu se poate pune sub banner - ar ingrosa fiecare raport - deci sta
    /// in --help, si de acolo trimite la textul complet.
    /// </summary>
    [Fact]
    public void HelpCarriesTheWarrantyDisclaimerAndPointsAtTheLicense()
    {
        string help = string.Join("\n", Lines("--help"));

        Assert.Contains("FARA NICIO GARANTIE", help);
        Assert.Contains("GPLv3", help);
        Assert.Contains("LICENSE", help);
        Assert.Contains("gnu.org/licenses/gpl-3.0.html", help);
    }

    [Fact]
    public void HelpAlsoStartsWithTheBanner()
    {
        Assert.Equal(VersionInfo.Banner, FirstLine("--help"));
        Assert.Equal(VersionInfo.Banner, FirstLine());
    }

    /// <summary>Si o rulare care esueaza spune ce versiune a esuat.</summary>
    [Fact]
    public void AFailedRunStillPrintsTheBanner()
    {
        using var ws = new TempWorkspace();

        Assert.Equal(VersionInfo.Banner, FirstLine(ws.PathTo("nu-exista.fit")));
        Assert.Equal(VersionInfo.Banner, FirstLine("--optiune-inventata"));
    }

    /// <summary>Banner-ul nu se dubleaza in --help, unde era tiparit si de WriteUsage.</summary>
    [Fact]
    public void TheBannerIsPrintedExactlyOnce()
    {
        var stdout = new StringWriter();
        FlatCheckCli.Run(new[] { "--help" }, stdout, new StringWriter());

        int count = Regex.Matches(stdout.ToString(), Regex.Escape(VersionInfo.Banner)).Count;
        Assert.Equal(1, count);
    }

    /// <summary>--version a disparut: acum e doar un argument necunoscut.</summary>
    [Fact]
    public void TheVersionFlagIsGone()
    {
        Assert.Contains("optiune necunoscuta", Options.Parse(new[] { "f.fit", "--version" }).Error);
    }
}
