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
using DarksCheck.Core;

namespace DarksCheck.Tests;

/// <summary>
/// The list of third-party components cannot lag behind the csproj (R-010, ADR 0003): a
/// package linked without an entry would ship undisplayed, and LGPL-2.1 §6 wants LibRaw's
/// notice where the program shows its own copyright. The tests bind the list to the Core
/// csproj in both directions and each entry to a heading of THIRD-PARTY-NOTICES.md; and the
/// list is what the program ships (R-120, ADR 0027): the components the suite's notices name
/// for DarksCheck, one for each DLL of LibRaw's Windows package.
/// </summary>
public partial class ThirdPartyTests
{
    private static string[] CorePackages()
    {
        string csproj = File.ReadAllText(Path.Combine(TestData.FindRepoRoot(), "src", "DarksCheck.Core", "DarksCheck.Core.csproj"));
        return PackageReference().Matches(csproj).Select(m => m.Groups[1].Value).ToArray();
    }

    /// <summary>
    /// Every package linked is claimed and every package claimed is linked; a package is one
    /// component's, but LibRaw's Windows package, whose four DLLs are four components.
    /// </summary>
    [Fact]
    public void R010_EveryPackageTheCoreLinksIsClaimed_OnlyLibRawsWindowsPackageByFour()
    {
        string[] linked = CorePackages();
        Assert.NotEmpty(linked);
        string[] claimed = ThirdParty.Components.SelectMany(c => c.Packages).ToArray();

        foreach (string p in linked)
        {
            Assert.True(claimed.Contains(p), $"package {p} of DarksCheck.Core.csproj is claimed by no component");
        }

        foreach (string p in claimed)
        {
            Assert.True(linked.Contains(p), $"ThirdParty.Components claims {p}, which is not in DarksCheck.Core.csproj");
        }

        Assert.All(ThirdParty.Components, c => Assert.Equal(c.Packages.Count, c.Packages.Distinct().Count()));
        IGrouping<string, string> shared = Assert.Single(
            ThirdParty.Components.SelectMany(c => c.Packages.Select(p => (Package: p, c.Name))).GroupBy(x => x.Package, x => x.Name),
            g => g.Count() > 1);
        Assert.Equal("Sdcb.LibRaw.runtime.win64", shared.Key);
        Assert.Equal(["LibRaw", "libjpeg-turbo", "Little CMS", "zlib"], shared);
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
        // Three since 2026-10-30: NLog joined for the diagnostic log (ADR 0025). Six since
        // 2026-11-01: the three libraries LibRaw's DLLs call are listed apart (ADR 0027).
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
    /// The list is the components the Astro Darkroom suite's notices name for DarksCheck, in
    /// R-120's order, but the platform the suite carries for every program (.NET 8 and WPF, ADR
    /// 0027): a component the suite ships for DarksCheck cannot be missing from About, nor About
    /// list one the suite does not ship for it. The file is the one About points to.
    /// </summary>
    [Fact]
    public void R120_TheComponentsAreTheOnesTheSuitesNoticesNameForDarksCheck()
    {
        string suite = Path.GetFullPath(Path.Combine(TestData.FindRepoRoot(), "..", "AstroDarkroom", "THIRD-PARTY-NOTICES.md"));
        Assert.True(File.Exists(suite), "the suite's notices are not beside DarksCheck: " + suite);

        string[] named = SectionsNaming("DarksCheck", File.ReadAllLines(suite)).Where(n => n != ".NET 8 and WPF").ToArray();

        Assert.Equal(named.Order(StringComparer.Ordinal), ThirdParty.Components.Select(c => c.Name).Order(StringComparer.Ordinal));
        Assert.Equal(["Sdcb.LibRaw", "LibRaw", "libjpeg-turbo", "Little CMS", "zlib", "NLog"], ThirdParty.Components.Select(c => c.Name));
    }

    /// <summary>
    /// Each DLL of LibRaw's Windows package, as the build puts it beside the program, is named by
    /// the role of exactly one component, in either language, so a new DLL in a later package
    /// cannot ship unlisted; and every Romanian role and licence is ASCII (ADR 0021).
    /// </summary>
    [Fact]
    public void R120_EachDllOfLibRawsWindowsPackageIsNamedByOneComponent_InBothLanguages()
    {
        string native = Path.Combine(AppContext.BaseDirectory, "runtimes", "win-x64", "native");
        string[] dlls = Directory.GetFiles(native, "*.dll").Select(f => Path.GetFileName(f)).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(["jpeg8.dll", "lcms2.dll", "raw_r.dll", "zlib1.dll"], dlls);

        foreach (UiLanguage language in Languages)
        {
            using IDisposable _ = Ui.Use(language);
            IReadOnlyList<ThirdPartyComponent> components = ThirdParty.Components;
            foreach (string dll in dlls)
            {
                Assert.Single(components, c => c.Role.Contains(dll, StringComparison.Ordinal));
            }

            Assert.Equal("LibRaw", Assert.Single(components, c => c.Role.Contains("raw_r.dll", StringComparison.Ordinal)).Name);
            Assert.All(components.SelectMany(c => c.Role + c.Licence), ch => Assert.True(ch < 128, $"'{ch}' is not ASCII in {language}"));
        }
    }

    /// <summary>
    /// The IJG licence asks the documentation of a program distributed as executables to state
    /// that it is based in part on the work of the Independent JPEG Group; the single file has no
    /// other documentation than About, so libjpeg-turbo's licence line quotes it, in both languages.
    /// </summary>
    [Fact]
    public void R120_LibjpegTurbosLicenceLineQuotesTheIjgStatement_InBothLanguages()
    {
        foreach (UiLanguage language in Languages)
        {
            using IDisposable _ = Ui.Use(language);
            ThirdPartyComponent jpeg = Assert.Single(ThirdParty.Components, c => c.Name == "libjpeg-turbo");

            Assert.Contains("\"this software is based in part on the work of the Independent JPEG Group\"", jpeg.Licence, StringComparison.Ordinal);
            Assert.Contains("BSD-3-Clause", jpeg.Licence, StringComparison.Ordinal);
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

    /// <summary>The two languages of the interface (R-090).</summary>
    private static readonly UiLanguage[] Languages = [UiLanguage.English, UiLanguage.Romanian];

    /// <summary>
    /// The names of the sections of the suite's notices whose <c>- Programs:</c> item names a
    /// program: the heading <c>## Name</c>, and the item read with its indented continuation
    /// lines, split at its commas.
    /// </summary>
    private static IEnumerable<string> SectionsNaming(string program, string[] lines)
    {
        string? section = null;
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("## ", StringComparison.Ordinal))
            {
                section = lines[i][3..].Trim();
            }
            else if (section is not null && lines[i].StartsWith("- Programs:", StringComparison.Ordinal))
            {
                string item = lines[i]["- Programs:".Length..];
                while (i + 1 < lines.Length && lines[i + 1].StartsWith("  ", StringComparison.Ordinal))
                {
                    item += " " + lines[++i];
                }

                if (item.Split(',').Select(p => p.Trim()).Contains(program))
                {
                    yield return section;
                }
            }
        }
    }

    [GeneratedRegex(@"<PackageReference\s+Include=""([^""]+)""")]
    private static partial Regex PackageReference();

    [GeneratedRegex(@"^##\s+(.+)$", RegexOptions.Multiline)]
    private static partial Regex Heading();
}
