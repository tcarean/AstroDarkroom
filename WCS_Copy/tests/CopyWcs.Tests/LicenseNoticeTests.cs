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

namespace CopyWcs.Tests;

/// <summary>
/// GPLv3 vrea ca notita de copyright sa se vada la fiecare rulare, si ca un program care
/// afiseaza notite legale sa afiseze si lipsa garantiei (sectiunea 5(d)). Testele astea
/// tin cele doua promisiuni: a doua linie a oricarei rulari e copyright-ul, iar --help
/// poarta disclaimerul si trimite la textul complet. Si leaga sursa de adevar: sirul vine
/// din &lt;Copyright&gt; din csproj, nu dintr-un literal ramas prin cod.
/// </summary>
public class LicenseNoticeTests
{
    private static string RepoDir([CallerFilePath] string thisFile = "")
        => Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(thisFile)))!;

    private static string[] Lines(params string[] args)
    {
        var stdout = new StringWriter();
        CopyWcsCli.Run(args, stdout, new StringWriter());
        return stdout.ToString().Replace("\r\n", "\n").Split('\n');
    }

    private static string SecondLine(params string[] args) => Lines(args)[1];

    /// <summary>
    /// Verifica plumbing-ul csproj -> assembly -> LicenseInfo. Daca cineva scoate
    /// &lt;Copyright&gt;, ramane fallback-ul din cod - si aici se vede.
    /// </summary>
    [Fact]
    public void TheCopyrightComesFromTheBuildProps()
    {
        var props = File.ReadAllText(Path.Combine(RepoDir(), "Directory.Build.props"));

        var m = Regex.Match(props, @"<Copyright>([^<]+)</Copyright>");

        Assert.True(m.Success, "Directory.Build.props has no <Copyright>");
        Assert.Equal(m.Groups[1].Value.Trim(), LicenseInfo.Copyright);
    }

    [Fact]
    public void TheCopyrightNamesTheAuthorAndTheYear()
    {
        Assert.Equal("Copyright (C) 2026 Tudor Carean", LicenseInfo.Copyright);
    }

    [Fact]
    public void EveryRunPrintsTheCopyrightOnTheSecondLine()
    {
        using var ws = new TempWorkspace();
        var data = FitsTestData.DeterministicData(400);
        var source = ws.WriteFile("source.fit",
            FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards(), data));
        var dest = ws.WriteFile("dest.fit",
            FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards(), data));

        Assert.Equal(LicenseInfo.Copyright, SecondLine(source, dest, "--dry-run"));
        Assert.Equal(LicenseInfo.Copyright, SecondLine(source, dest, "-o", ws.FilePath("out.fit")));
        Assert.Equal(LicenseInfo.Copyright, SecondLine("--help"));
    }

    /// <summary>Si o rulare care esueaza spune a cui e unealta care a esuat.</summary>
    [Fact]
    public void AFailedRunStillPrintsTheCopyright()
    {
        using var ws = new TempWorkspace();

        Assert.Equal(LicenseInfo.Copyright,
            SecondLine(ws.FilePath("nu-exista.fit"), ws.FilePath("nici-asta.fit")));
        Assert.Equal(LicenseInfo.Copyright, SecondLine("--optiune-inventata"));
        Assert.Equal(LicenseInfo.Copyright, SecondLine("un-singur-argument"));
    }

    [Fact]
    public void TheCopyrightIsPrintedExactlyOnce()
    {
        var stdout = new StringWriter();
        CopyWcsCli.Run(["--help"], stdout, new StringWriter());

        var count = Regex.Matches(stdout.ToString(), Regex.Escape(LicenseInfo.Copyright)).Count;
        Assert.Equal(1, count);
    }

    /// <summary>
    /// GPLv3 sectiunea 5(d): un program care afiseaza notite legale trebuie sa afiseze si
    /// lipsa garantiei. Nu se poate pune sub banner - ar ingrosa fiecare rulare - deci
    /// sta in --help, si de acolo trimite la textul complet.
    /// </summary>
    [Fact]
    public void HelpCarriesTheWarrantyDisclaimerAndPointsAtTheLicense()
    {
        var help = string.Join("\n", Lines("--help"));

        Assert.Contains("FARA NICIO GARANTIE", help);
        Assert.Contains("GPLv3", help);
        Assert.Contains("LICENSE", help);
        Assert.Contains("gnu.org/licenses/gpl-3.0.html", help);
    }

    /// <summary>Disclaimerul apare si cand usage-ul e tiparit din cauza unei erori.</summary>
    [Fact]
    public void UsagePrintedAfterABadArgumentAlsoCarriesTheDisclaimer()
    {
        var help = string.Join("\n", Lines("un-singur-argument"));

        Assert.Contains("FARA NICIO GARANTIE", help);
    }
}
