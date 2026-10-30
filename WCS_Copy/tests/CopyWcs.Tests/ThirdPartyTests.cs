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

namespace CopyWcs.Tests;

/// <summary>
/// The list of third-party components cannot lag behind the csproj (R-035, ADR 0009): a package
/// linked without an entry would ship without its notice, which BSD-3-Clause asks to travel with
/// a binary. The tests bind the list to the core's csproj in both directions and each entry to a
/// heading of THIRD-PARTY-NOTICES.md. After DarksCheck's ThirdPartyTests.
/// </summary>
public partial class ThirdPartyTests
{
    /// <summary>The package IDs <c>CopyWcs.Core.csproj</c> references.</summary>
    private static string[] CorePackages()
    {
        string csproj = File.ReadAllText(Path.Combine(TestData.FindRepoRoot(), "src", "CopyWcs.Core", "CopyWcs.Core.csproj"));
        return PackageReference().Matches(csproj).Select(m => m.Groups[1].Value).ToArray();
    }

    [Fact]
    public void R035_EveryPackageTheCoreLinksIsListedExactlyOnce()
    {
        string[] linked = CorePackages();
        Assert.NotEmpty(linked);
        string[] claimed = ThirdParty.Components.SelectMany(c => c.Packages).ToArray();

        foreach (string p in linked)
        {
            Assert.True(claimed.Count(c => c == p) == 1, $"package {p} of CopyWcs.Core.csproj is not claimed by exactly one component");
        }

        foreach (string p in claimed)
        {
            Assert.True(linked.Contains(p), $"ThirdParty.Components claims {p}, which is not in CopyWcs.Core.csproj");
        }
    }

    [Fact]
    public void R035_EveryComponentHasAHeadingInTheNoticesFile()
    {
        string notices = File.ReadAllText(Path.Combine(TestData.FindRepoRoot(), "THIRD-PARTY-NOTICES.md"));
        string[] headings = Heading().Matches(notices).Select(m => m.Groups[1].Value.Trim()).ToArray();
        foreach (ThirdPartyComponent c in ThirdParty.Components)
        {
            Assert.True(headings.Any(h => h.Contains(c.Name, StringComparison.Ordinal)), $"THIRD-PARTY-NOTICES.md has no heading for {c.Name}");
        }
    }

    [Fact]
    public void EveryComponentNamesItsLicenceAndUrl()
    {
        Assert.Single(ThirdParty.Components);
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

    /// <summary>NLog and its atomic target are one component under BSD-3-Clause, with the
    /// copyright line of its LICENSE.txt, the one THIRD-PARTY-NOTICES.md quotes.</summary>
    [Fact]
    public void R035_NLogIsTakenUnderBsd3AndCarriesItsCopyright()
    {
        ThirdPartyComponent nlog = Assert.Single(ThirdParty.Components, c => c.Name == "NLog");
        Assert.Equal("BSD-3-Clause", nlog.Licence);
        Assert.Collection(nlog.Packages,
            p => Assert.Equal("NLog", p),
            p => Assert.Equal("NLog.Targets.AtomicFile", p));
        string notices = File.ReadAllText(Path.Combine(TestData.FindRepoRoot(), "THIRD-PARTY-NOTICES.md"));
        Assert.Contains("> " + nlog.Copyright, notices, StringComparison.Ordinal);
    }

    [GeneratedRegex(@"<PackageReference\s+Include=""([^""]+)""")]
    private static partial Regex PackageReference();

    [GeneratedRegex(@"^##\s+(.+)$", RegexOptions.Multiline)]
    private static partial Regex Heading();
}
