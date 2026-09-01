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
/// Versiunea se incrementeaza manual, iar sursa de adevar e <c>&lt;Version&gt;</c> din
/// csproj - <see cref="VersionInfo"/> o citeste din assembly, nu o hardcodeaza. Testele
/// astea verifica plumbing-ul ala, si ca banner-ul chiar apare pe prima linie a fiecarei
/// rulari: altfel o iesire copiata undeva nu mai spune ce versiune a produs-o.
/// </summary>
public class VersionTests
{
    private static string RepoDir([CallerFilePath] string thisFile = "")
        => Path.GetDirectoryName(Path.GetDirectoryName(thisFile))!;

    private static string[] Lines(params string[] args)
    {
        var stdout = new StringWriter();
        CopyWcsCli.Run(args, stdout, new StringWriter());
        return stdout.ToString().Replace("\r\n", "\n").Split('\n');
    }

    private static string FirstLine(params string[] args) => Lines(args)[0];

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
        var csproj = File.ReadAllText(Path.Combine(RepoDir(), "CopyWcs", "CopyWcs.csproj"));

        var m = Regex.Match(csproj, @"<Version>([^<]+)</Version>");

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

    [Fact]
    public void TheBannerIsTheNameAndTheVersion()
    {
        Assert.Equal($"copywcs {VersionInfo.Value}", VersionInfo.Banner);
    }

    [Fact]
    public void EveryRunPrintsTheBannerOnTheFirstLine()
    {
        using var ws = new TempWorkspace();
        var data = FitsTestData.DeterministicData(400);
        var source = ws.WriteFile("source.fit",
            FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards(), data));
        var dest = ws.WriteFile("dest.fit",
            FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards(), data));

        Assert.Equal(VersionInfo.Banner, FirstLine(source, dest, "--dry-run"));
        Assert.Equal(VersionInfo.Banner, FirstLine(source, dest, "-o", ws.FilePath("out.fit")));
        Assert.Equal(VersionInfo.Banner, FirstLine("--help"));
    }

    /// <summary>Si o rulare care esueaza spune ce versiune a esuat.</summary>
    [Fact]
    public void AFailedRunStillPrintsTheBanner()
    {
        using var ws = new TempWorkspace();

        Assert.Equal(VersionInfo.Banner,
            FirstLine(ws.FilePath("nu-exista.fit"), ws.FilePath("nici-asta.fit")));
        Assert.Equal(VersionInfo.Banner, FirstLine("--optiune-inventata"));
        Assert.Equal(VersionInfo.Banner, FirstLine("un-singur-argument"));
    }

    [Fact]
    public void TheBannerIsPrintedExactlyOnce()
    {
        var stdout = new StringWriter();
        CopyWcsCli.Run(new[] { "--help" }, stdout, new StringWriter());

        var count = Regex.Matches(stdout.ToString(), Regex.Escape(VersionInfo.Banner)).Count;
        Assert.Equal(1, count);
    }

    /// <summary>
    /// Nu exista --version: banner-ul de la fiecare rulare il face redundant, si un flag
    /// in plus ar fi inca o cale de iesire de intretinut. Ramane optiune necunoscuta.
    /// </summary>
    [Fact]
    public void ThereIsNoVersionFlag()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => Options.Parse(new[] { "a.fit", "b.fit", "--version" }));

        Assert.Contains("Optiune necunoscuta", ex.Message);
    }
}
