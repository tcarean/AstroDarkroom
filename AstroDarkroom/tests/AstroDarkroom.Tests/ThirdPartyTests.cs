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
using AstroDarkroom.Core;

namespace AstroDarkroom.Tests;

/// <summary>
/// The list of third-party components cannot lag behind the project files (R-015, ADR 0013): a
/// package linked without an entry would ship without its notice in "Despre". The tests bind the
/// list to the packages of every project under src/ in both directions, and each entry to a
/// section of THIRD-PARTY-NOTICES.md that carries its copyright line. LightsCheck's and
/// FlatCheck's tests of their lists are the model.
/// </summary>
public partial class ThirdPartyTests
{
    [Fact]
    public void R015_EveryPackageTheLauncherLinksIsClaimedByExactlyOneComponent()
    {
        string[] linked = LinkedPackages();
        Assert.NotEmpty(linked);
        string[] claimed = ThirdParty.Components.SelectMany(c => c.Packages).ToArray();

        foreach (string package in linked)
        {
            Assert.Single(claimed, c => c == package);
        }

        foreach (string package in claimed)
        {
            Assert.Contains(package, linked);
        }
    }

    [Fact]
    public void R015_EveryComponentHasASectionInTheNoticesFileWithItsCopyright()
    {
        string notices = File.ReadAllText(Path.Combine(TestData.FindRepoRoot(), "THIRD-PARTY-NOTICES.md"));
        string[] headings = Heading().Matches(notices).Select(m => m.Groups[1].Value).ToArray();

        foreach (ThirdPartyComponent c in ThirdParty.Components)
        {
            Assert.Contains(headings, h => h.Contains(c.Name, StringComparison.Ordinal));
            Assert.Contains(c.Copyright, notices, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// NLog's section holds the BSD-3-Clause text whole, as R-015 says: its three conditions and
    /// its disclaimer, which "Despre" does not show, are what the binary condition asks to go with
    /// the program; a section reduced to the copyright line would pass the test above.
    /// </summary>
    [Fact]
    public void R015_TheNoticesFileHoldsNLogsWholeBsdText()
    {
        string notices = File.ReadAllText(Path.Combine(TestData.FindRepoRoot(), "THIRD-PARTY-NOTICES.md"));
        int start = notices.IndexOf("## NLog", StringComparison.Ordinal);
        Assert.True(start >= 0, "THIRD-PARTY-NOTICES.md has no section ## NLog");
        int end = notices.IndexOf("\n## ", start + 1, StringComparison.Ordinal);
        string section = end < 0 ? notices[start..] : notices[start..end];
        string[] lines =
        [
            "Redistributions of source code must retain the above copyright notice,",
            "Redistributions in binary form must reproduce the above copyright notice,",
            "Neither the name of Jaroslaw Kowalski nor the names of its",
            "THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS \"AS IS\"",
            "THE POSSIBILITY OF SUCH DAMAGE.",
        ];

        foreach (string line in lines)
        {
            Assert.Contains(line, section, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void R015_NLogIsListedWithItsLicenceCopyrightAndAddress_ItsRoleInBothLanguages()
    {
        ThirdPartyComponent nlog = Assert.Single(ThirdParty.Components);
        Assert.Equal("NLog", nlog.Name);
        Assert.Equal("BSD-3-Clause", nlog.Licence);
        Assert.Equal("Copyright (c) 2004-2024 Jaroslaw Kowalski <jaak@jkowalski.net>, Kim Christensen, Julian Verdurmen", nlog.Copyright);
        Assert.Equal("https://nlog-project.org/", nlog.Url);
        string[] packages = ["NLog", "NLog.Targets.AtomicFile"];
        Assert.Equal(packages, nlog.Packages);

        string romanian = nlog.Role;   // the tests run in Romanian
        using IDisposable scope = Ui.Use(UiLanguage.English);
        string english = Assert.Single(ThirdParty.Components).Role;
        Assert.Contains("jurnal", romanian, StringComparison.Ordinal);
        Assert.Contains("log", english, StringComparison.Ordinal);
        Assert.NotEqual(romanian, english);
    }

    /// <summary>The packages every project under src/ references, one entry per reference.</summary>
    private static string[] LinkedPackages()
    {
        string src = Path.Combine(TestData.FindRepoRoot(), "src");
        return Directory.EnumerateFiles(src, "*.csproj", SearchOption.AllDirectories)
            .SelectMany(f => PackageReference().Matches(File.ReadAllText(f)).Select(m => m.Groups[1].Value))
            .ToArray();
    }

    [GeneratedRegex(@"<PackageReference\s+Include=""([^""]+)""")]
    private static partial Regex PackageReference();

    [GeneratedRegex(@"^##\s+(.+)$", RegexOptions.Multiline)]
    private static partial Regex Heading();
}
