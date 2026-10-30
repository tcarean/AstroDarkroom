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
using System.Text.Json;
using System.Text.RegularExpressions;
using ImageVisualCompare.Wpf;

namespace ImageVisualCompare.Tests;

/// <summary>
/// The components written by others cannot lag behind what the application ships (R-075, ADR
/// 0043): a package shipped without an entry would ship without its notice, which BSD-3-Clause
/// asks to travel with a binary. The tests bind the list to the packages of the application's
/// lock file, the ones another package brings included, and to its csproj, in both directions;
/// each entry to its section of THIRD-PARTY-NOTICES.md; and find nothing in the build files that
/// copies the notices beside the executable. The About window's card is
/// <c>AboutWindowTests</c>'s. After the ThirdPartyTests of CopyWcs and FlatCheck.
/// </summary>
public partial class ThirdPartyTests
{
    private static string ApplicationProject() =>
        Path.Combine(TestData.RepoDir(), "src", "ImageVisualCompare.Wpf", "ImageVisualCompare.Wpf.csproj");

    private static string Notices() => File.ReadAllText(Path.Combine(TestData.RepoDir(), "THIRD-PARTY-NOTICES.md"));

    /// <summary>
    /// The packages the application ships: every Direct and Transitive package of its lock file,
    /// the project reference to the core aside, so that a package another one brings is claimed too.
    /// </summary>
    private static string[] ShippedPackages()
    {
        string path = Path.Combine(TestData.RepoDir(), "src", "ImageVisualCompare.Wpf", "packages.lock.json");
        using JsonDocument lockFile = JsonDocument.Parse(File.ReadAllText(path));
        return [.. lockFile.RootElement.GetProperty("dependencies").EnumerateObject()
            .SelectMany(framework => framework.Value.EnumerateObject())
            .Where(package => package.Value.GetProperty("type").GetString() is "Direct" or "Transitive")
            .Select(package => package.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>The section of THIRD-PARTY-NOTICES.md under the heading that names a component, to the next heading.</summary>
    private static string SectionOf(string notices, string name)
    {
        Match[] headings = Heading().Matches(notices).ToArray();
        Match heading = headings.First(h => h.Groups[1].Value.Contains(name, StringComparison.Ordinal));
        Match? next = headings.FirstOrDefault(h => h.Index > heading.Index);
        return notices[heading.Index..(next?.Index ?? notices.Length)];
    }

    [Fact]
    public void R075_EveryPackageTheApplicationShipsIsListedExactlyOnce()
    {
        string[] referenced = [.. PackageReference().Matches(File.ReadAllText(ApplicationProject())).Select(m => m.Groups[1].Value)];
        string[] shipped = ShippedPackages();
        string[] claimed = [.. ThirdParty.Components.SelectMany(c => c.Packages)];

        Assert.NotEmpty(referenced);
        Assert.All(referenced, p => Assert.Contains(p, shipped, StringComparer.OrdinalIgnoreCase));
        Assert.All(shipped, p => Assert.True(
            claimed.Count(c => string.Equals(c, p, StringComparison.OrdinalIgnoreCase)) == 1,
            $"package {p} of the application's lock file is not claimed by exactly one component"));
        Assert.All(claimed, p => Assert.True(
            shipped.Contains(p, StringComparer.OrdinalIgnoreCase),
            $"ThirdParty.Components claims {p}, which the application does not ship"));
    }

    [Fact]
    public void R075_EveryComponentHasASectionInTheNoticesFile_WhichQuotesItsCopyright()
    {
        string notices = Notices();
        string[] headings = [.. Heading().Matches(notices).Select(m => m.Groups[1].Value.Trim())];

        Assert.All(ThirdParty.Components, c =>
        {
            Assert.True(headings.Any(h => h.Contains(c.Name, StringComparison.Ordinal)), $"THIRD-PARTY-NOTICES.md has no heading for {c.Name}");
            Assert.Contains("> " + c.Copyright, SectionOf(notices, c.Name), StringComparison.Ordinal);
        });
    }

    /// <summary>The About window links each component's project; a relative address would not open.</summary>
    [Fact]
    public void R075_EveryComponentLinksAnAbsoluteHttpsAddress()
    {
        Assert.All(ThirdParty.Components, c => Assert.True(
            Uri.TryCreate(c.Url, UriKind.Absolute, out Uri? url) && url.Scheme == Uri.UriSchemeHttps,
            c.Name + " links " + c.Url));
    }

    /// <summary>
    /// NLog and its atomic target are one component under BSD-3-Clause, with the copyright line of
    /// its LICENSE.txt. THIRD-PARTY-NOTICES.md is the one place the licence's whole text is, so
    /// its three conditions and its disclaimer are all checked there.
    /// </summary>
    [Fact]
    public void R075_NLogIsTakenUnderBsd3_ItsWholeTextInTheNotices()
    {
        ThirdPartyComponent nlog = Assert.Single(ThirdParty.Components);
        string section = SectionOf(Notices(), "NLog");

        Assert.Equal("NLog", nlog.Name);
        Assert.Equal("BSD-3-Clause", nlog.Licence);
        Assert.Equal("Copyright (c) 2004-2024 Jaroslaw Kowalski <jaak@jkowalski.net>, Kim Christensen, Julian Verdurmen", nlog.Copyright);
        Assert.Equal("https://nlog-project.org/", nlog.Url);
        Assert.Equal(["NLog", "NLog.Targets.AtomicFile"], nlog.Packages);
        Assert.Contains("Redistributions of source code must retain the above copyright notice,", section, StringComparison.Ordinal);
        Assert.Contains("Redistributions in binary form must reproduce the above copyright notice,", section, StringComparison.Ordinal);
        Assert.Contains("Neither the name of Jaroslaw Kowalski nor the names of its", section, StringComparison.Ordinal);
        Assert.Contains("THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS \"AS IS\"", section, StringComparison.Ordinal);
    }

    /// <summary>
    /// The notices stay beside the source: the suite's release puts every tool in one folder, and
    /// the executable carries its notices in About. The build files are read rather than the build
    /// output, where a copy an earlier build left would outlive the item that made it: no item of
    /// the csproj, nor of the Directory.Build files every project imports, names the notices file,
    /// in any case and with either quote. A wildcard that would sweep it in is not looked for.
    /// </summary>
    [Fact]
    public void R075_TheBuildCopiesNoNoticesFileBesideTheExecutable()
    {
        string[] files =
        [
            ApplicationProject(),
            .. new[] { "Directory.Build.props", "Directory.Build.targets" }
                .Select(f => Path.Combine(TestData.RepoDir(), f))
                .Where(File.Exists),
        ];

        Assert.All(files, f => Assert.DoesNotMatch(NoticesItem(), File.ReadAllText(f)));
    }

    [GeneratedRegex(@"<PackageReference\s+Include=""([^""]+)""")]
    private static partial Regex PackageReference();

    [GeneratedRegex(@"^##\s+(.+)$", RegexOptions.Multiline)]
    private static partial Regex Heading();

    [GeneratedRegex(@"\b(Include|Update|Link)\s*=\s*[""'][^""']*THIRD-PARTY-NOTICES", RegexOptions.IgnoreCase)]
    private static partial Regex NoticesItem();
}
