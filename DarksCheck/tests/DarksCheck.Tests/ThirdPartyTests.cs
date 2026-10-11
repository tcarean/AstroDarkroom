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
/// csproj in both directions and each entry to a heading of THIRD-PARTY-NOTICES.md.
/// </summary>
public partial class ThirdPartyTests
{
    private static string[] CorePackages()
    {
        string csproj = File.ReadAllText(Path.Combine(TestData.FindRepoRoot(), "src", "DarksCheck.Core", "DarksCheck.Core.csproj"));
        return PackageReference().Matches(csproj).Select(m => m.Groups[1].Value).ToArray();
    }

    [Fact]
    public void R010_EveryPackageTheCoreLinksIsListedExactlyOnce()
    {
        string[] linked = CorePackages();
        Assert.NotEmpty(linked);
        string[] claimed = ThirdParty.Components.SelectMany(c => c.Packages).ToArray();

        foreach (string p in linked)
        {
            Assert.True(claimed.Count(c => c == p) == 1, $"package {p} of DarksCheck.Core.csproj is not claimed by exactly one component");
        }

        foreach (string p in claimed)
        {
            Assert.True(linked.Contains(p), $"ThirdParty.Components claims {p}, which is not in DarksCheck.Core.csproj");
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
        Assert.Equal(2, ThirdParty.Components.Count);
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
