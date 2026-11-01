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

using System.Text.Json;
using System.Text.RegularExpressions;
using LightsCheck.Core;

namespace LightsCheck.Tests;

/// <summary>
/// The list of third-party components cannot lag behind what the build ships (R-010, R-075,
/// ADRs 0003 and 0030): a package linked without an entry, or a native library without a row,
/// would ship undisplayed, and LGPL-2.1 §6 wants LibRaw's notice where the program shows its
/// own copyright. The tests bind the list to the Core csproj in both directions, each native
/// library the core's packages bring for Windows to one row, each entry to a heading of
/// THIRD-PARTY-NOTICES.md, and the lead above the list in "About" to the list's length.
/// </summary>
public partial class ThirdPartyTests
{
    private static string[] CorePackages()
    {
        string csproj = File.ReadAllText(Path.Combine(TestData.FindRepoRoot(), "src", "LightsCheck.Core", "LightsCheck.Core.csproj"));
        return PackageReference().Matches(csproj).Select(m => m.Groups[1].Value).ToArray();
    }

    /// <summary>
    /// The native libraries the core's packages bring for Windows, each file name with the
    /// package that brings it, from the core's restore (<c>obj/project.assets.json</c>, the
    /// <c>win-x64</c> native assets): what both forms of the tool ship beside, or inside, the exe.
    /// </summary>
    private static Dictionary<string, string> NativeLibraries()
    {
        string assets = Path.Combine(TestData.FindRepoRoot(), "src", "LightsCheck.Core", "obj", "project.assets.json");
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(assets));
        Dictionary<string, string> dlls = new(StringComparer.OrdinalIgnoreCase);
        foreach (JsonProperty target in doc.RootElement.GetProperty("targets").EnumerateObject())
        {
            foreach (JsonProperty package in target.Value.EnumerateObject())
            {
                if (!package.Value.TryGetProperty("runtimeTargets", out JsonElement assetsOfPackage))
                {
                    continue;
                }

                foreach (JsonProperty asset in assetsOfPackage.EnumerateObject())
                {
                    if (asset.Value.GetProperty("assetType").GetString() == "native" && asset.Value.GetProperty("rid").GetString() == "win-x64")
                    {
                        dlls[Path.GetFileName(asset.Name)] = package.Name.Split('/')[0];
                    }
                }
            }
        }

        return dlls;
    }

    [Fact]
    public void R010_EveryLinkedPackageIsClaimed_AndOnlyANativePackageByMoreThanOneComponent()
    {
        string[] linked = CorePackages();
        Assert.NotEmpty(linked);
        string[] claimed = ThirdParty.Components.SelectMany(c => c.Packages).ToArray();

        foreach (string p in linked)
        {
            Assert.True(claimed.Contains(p), $"package {p} of LightsCheck.Core.csproj is claimed by no component");
        }

        foreach (string p in claimed)
        {
            Assert.True(linked.Contains(p), $"ThirdParty.Components claims {p}, which is not in LightsCheck.Core.csproj");
        }

        // A package claimed twice must bring native libraries for Windows, one per claimant, each
        // named in its claimant's role: Sdcb.LibRaw.runtime.win64's four DLLs are four components.
        Dictionary<string, string> natives = NativeLibraries();
        foreach (IGrouping<string, ThirdPartyComponent> shared in ThirdParty.Components
                     .SelectMany(c => c.Packages.Select(p => (Package: p, Component: c)))
                     .GroupBy(x => x.Package, x => x.Component)
                     .Where(g => g.Count() > 1))
        {
            foreach (ThirdPartyComponent c in shared)
            {
                Assert.True(
                    natives.Any(n => n.Value == shared.Key && c.Role.Contains(n.Key, StringComparison.Ordinal)),
                    $"{c.Name} shares {shared.Key} with another component without naming one of its native libraries in its role");
            }
        }
    }

    [Fact]
    public void R010_EachNativeLibraryForWindowsIsNamedByExactlyOneComponent_ThatClaimsItsPackage_InBothLanguages()
    {
        Dictionary<string, string> natives = NativeLibraries();
        Assert.NotEmpty(natives);
        foreach (UiLanguage language in new[] { UiLanguage.English, UiLanguage.Romanian })
        {
            using (Ui.Use(language))
            {
                foreach ((string dll, string package) in natives)
                {
                    ThirdPartyComponent[] naming = ThirdParty.Components.Where(c => c.Role.Contains(dll, StringComparison.Ordinal)).ToArray();
                    Assert.True(naming.Length == 1, $"{language}: {dll}, from {package}, is named in the role of {naming.Length} components, not one");
                    Assert.Contains(package, naming[0].Packages);
                }
            }
        }
    }

    [Fact]
    public void R010_EveryComponentHasAHeadingInTheNoticesFile()
    {
        string notices = File.ReadAllText(Path.Combine(TestData.FindRepoRoot(), "THIRD-PARTY-NOTICES.md"));
        string[] headings = Heading().Matches(notices).Select(m => m.Groups[1].Value).ToArray();
        foreach (ThirdPartyComponent c in ThirdParty.Components)
        {
            Assert.True(headings.Any(h => h.Contains(c.Name, StringComparison.Ordinal)), $"THIRD-PARTY-NOTICES.md has no heading for {c.Name}");
        }
    }

    [Fact]
    public void EveryComponentNamesItsLicenceAndUrl()
    {
        // Three since 2026-10-30, when NLog joined for the diagnostic log (ADR 0029); six since
        // 2026-11-01, when the three libraries inside libraw's DLLs got rows of their own (R-075).
        Assert.Equal(6, ThirdParty.Components.Count);
        foreach (ThirdPartyComponent c in ThirdParty.Components)
        {
            Assert.False(string.IsNullOrWhiteSpace(c.Name));
            Assert.False(string.IsNullOrWhiteSpace(c.Role));
            Assert.False(string.IsNullOrWhiteSpace(c.Licence));
            Assert.False(string.IsNullOrWhiteSpace(c.Copyright));
            Assert.StartsWith("https://", c.Url, StringComparison.Ordinal);
            Assert.NotEmpty(c.Packages);
        }
    }

    [Fact]
    public void R010_LibRawIsTakenUnderTheLgplAndCarriesItsCopyright()
    {
        ThirdPartyComponent libraw = Assert.Single(ThirdParty.Components, c => c.Name == "LibRaw");
        Assert.Contains("LGPL-2.1", libraw.Licence, StringComparison.Ordinal);
        Assert.Contains("LibRaw LLC", libraw.Copyright, StringComparison.Ordinal);
    }

    /// <summary>
    /// What the suite's THIRD-PARTY-NOTICES.txt names for LightsCheck, without the .NET runtime
    /// and WPF (R-075, ADR 0030): libraw's wrapper, LibRaw, the three libraries its DLLs call,
    /// and NLog.
    /// </summary>
    [Fact]
    public void R075_TheComponentsAreTheSixTheToolShips_InOrder()
    {
        string[] shipped = ["Sdcb.LibRaw", "LibRaw", "libjpeg-turbo", "Little CMS", "zlib", "NLog"];
        Assert.Equal(shipped, ThirdParty.Components.Select(c => c.Name));
    }

    /// <summary>
    /// The lead counts the rows in words, so a component added without its count turns this red
    /// (the lead said "two" with three rows until 2026-11-01), and points to the licence texts in
    /// words true from the suite folder and from a build of the source alike (R-075).
    /// </summary>
    [Fact]
    public void R075_TheLeadCountsTheComponentsInWordsAndPointsToTheLicenceTexts_InBothLanguages()
    {
        string[] english = ["zero", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten"];
        string[] romanian = ["zero", "una", "doua", "trei", "patru", "cinci", "sase", "sapte", "opt", "noua", "zece"];
        int n = ThirdParty.Components.Count;
        Assert.InRange(n, 2, 10);

        string en, ro;
        using (Ui.Use(UiLanguage.English))
        {
            en = ThirdParty.Lead;
        }

        using (Ui.Use(UiLanguage.Romanian))
        {
            ro = ThirdParty.Lead;
        }

        Assert.StartsWith($"The tool ships {english[n]} components written by others, ", en, StringComparison.Ordinal);
        Assert.StartsWith($"Unealta livreaza {romanian[n]} componente scrise de altii, ", ro, StringComparison.Ordinal);
        foreach (string lead in new[] { en, ro })
        {
            Assert.Contains("THIRD-PARTY-NOTICES.txt", lead, StringComparison.Ordinal);
            Assert.Contains("Astro Darkroom", lead, StringComparison.Ordinal);
            Assert.Contains("AstroDarkroom/THIRD-PARTY-NOTICES.md", lead, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The IJG licence asks the documentation of a program shipped as executables to say that it is
    /// based in part on the work of the Independent JPEG Group; "About" says it in both languages
    /// (R-075).
    /// </summary>
    [Fact]
    public void R075_LibjpegTurbosRowCreditsTheIndependentJpegGroup_InBothLanguages()
    {
        using (Ui.Use(UiLanguage.English))
        {
            ThirdPartyComponent jpeg = Assert.Single(ThirdParty.Components, c => c.Name == "libjpeg-turbo");
            Assert.Contains("this software is based in part on the work of the Independent JPEG Group", jpeg.Role, StringComparison.Ordinal);
        }

        using (Ui.Use(UiLanguage.Romanian))
        {
            ThirdPartyComponent jpeg = Assert.Single(ThirdParty.Components, c => c.Name == "libjpeg-turbo");
            Assert.Contains("programul se bazeaza in parte pe munca Independent JPEG Group", jpeg.Role, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void R004_HelpPrintsTheWarrantyLinesAndTheLicenceUrl()
    {
        string help = Cli.Run("--help").Stdout;
        foreach (string l in RunReport.WarrantyLines)
        {
            Assert.Contains(l, help, StringComparison.Ordinal);
        }

        Assert.Contains(RunReport.LicenseUrl, help, StringComparison.Ordinal);
    }

    [GeneratedRegex(@"<PackageReference\s+Include=""([^""]+)""")]
    private static partial Regex PackageReference();

    [GeneratedRegex(@"^##\s+(.+)$", RegexOptions.Multiline)]
    private static partial Regex Heading();
}
