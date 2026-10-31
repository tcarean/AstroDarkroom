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

// ThirdPartyTests.cs - the list of third-party components cannot fall behind the csproj.
//
// The list in ThirdParty.Components is what the "Despre" window shows. A package added to
// the csproj without an entry here would be a package shipped and not shown; a package
// removed from the csproj with its entry left would show a licence we no longer use. The
// tests tie the two in both directions, and every entry to the notices file.

using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace AstroPhotoAnalyzer.Tests;

public class ThirdPartyTests
{
    static string RepoDir([CallerFilePath] string thisFile = "")
        => Path.GetDirectoryName(Path.GetDirectoryName(thisFile))!;

    static string CoreCsproj => File.ReadAllText(Path.Combine(RepoDir(), "AstroPhotoAnalyzer.Core", "AstroPhotoAnalyzer.Core.csproj"));

    /// <summary>The NuGet packages the core references, read from its csproj.</summary>
    static string[] CorePackages()
        => Regex.Matches(CoreCsproj, @"<PackageReference\s+Include=""([^""]+)""")
            .Select(m => m.Groups[1].Value)
            .ToArray();

    /// <summary>The core's embedded resources (the embedded data), by LogicalName.</summary>
    static string[] CoreResources()
        => Regex.Matches(CoreCsproj, @"<EmbeddedResource\s+Include=""[^""]+""\s+LogicalName=""([^""]+)""")
            .Select(m => m.Groups[1].Value)
            .ToArray();

    /// <summary>
    /// Every package in the csproj is claimed by exactly one component, and every claimed
    /// package exists in the csproj. This is the test that fails at the next dependency
    /// added without an entry - before it reaches the screen without a notice.
    /// </summary>
    [Fact]
    public void R122_EveryPackageTheCoreLinksIsListed()
    {
        string[] linked = CorePackages();
        Assert.NotEmpty(linked);

        string[] claimed = ThirdParty.Components.SelectMany(c => c.Packages).ToArray();

        foreach (string p in linked)
            Assert.True(claimed.Count(c => c == p) == 1,
                $"package {p} of AstroPhotoAnalyzer.Core.csproj is not claimed by exactly one component of ThirdParty.Components");

        foreach (string p in claimed)
            Assert.True(linked.Contains(p),
                $"ThirdParty.Components claims package {p}, which is no longer in AstroPhotoAnalyzer.Core.csproj");
    }

    /// <summary>
    /// The embedded data (the LDN catalogue, ADR 0025) is written by others just like a
    /// package: every resource in the csproj is claimed by exactly one component, and the
    /// other way round.
    /// </summary>
    [Fact]
    public void R122_EveryEmbeddedDataSetIsListed()
    {
        string[] embedded = CoreResources();
        Assert.Contains(DarkNebulaCatalogue.ResourceName, embedded);

        string[] claimed = ThirdParty.Components.SelectMany(c => c.Resources).ToArray();

        foreach (string r in embedded)
            Assert.True(claimed.Count(c => c == r) == 1,
                $"resource {r} of AstroPhotoAnalyzer.Core.csproj is not claimed by exactly one component of ThirdParty.Components");

        foreach (string r in claimed)
            Assert.True(embedded.Contains(r),
                $"ThirdParty.Components claims resource {r}, which is not in AstroPhotoAnalyzer.Core.csproj");

        var ldn = Assert.Single(ThirdParty.Components, c => c.Resources.Contains(DarkNebulaCatalogue.ResourceName));
        Assert.Contains("Lynds", ldn.Name);
        Assert.Contains("1962", ldn.Licence);
        Assert.Contains("VII/7A", ldn.Url);
        Assert.Empty(ldn.Packages);
    }

    /// <summary>
    /// The reasons for the GPLv3 compatibility live in THIRD-PARTY-NOTICES.md, so every
    /// component shown must have a heading there.
    /// </summary>
    [Fact]
    public void R122_EveryComponentHasAnEntryInTheNoticesFile()
    {
        string notices = File.ReadAllText(Path.Combine(RepoDir(), "THIRD-PARTY-NOTICES.md"));
        string[] headings = Regex.Matches(notices, @"^##\s+(.+)$", RegexOptions.Multiline)
            .Select(m => m.Groups[1].Value)
            .ToArray();

        foreach (var c in ThirdParty.Components)
            Assert.True(headings.Any(h => h.Contains(c.Name)),
                $"THIRD-PARTY-NOTICES.md has no heading for {c.Name}");
    }

    /// <summary>
    /// What the screen shows cannot be empty: name, role, licence, copyright, and an https
    /// link.
    /// </summary>
    [Fact]
    public void R122_EveryComponentNamesItsLicenceAndUrl()
    {
        Assert.Equal(4, ThirdParty.Components.Count);   // ImageSharp, Unicolour, NLog, the Lynds catalogue

        foreach (var c in ThirdParty.Components)
        {
            Assert.False(string.IsNullOrWhiteSpace(c.Name));
            Assert.False(string.IsNullOrWhiteSpace(c.Role));
            Assert.False(string.IsNullOrWhiteSpace(c.Licence));
            Assert.False(string.IsNullOrWhiteSpace(c.Copyright));
            Assert.StartsWith("https://", c.Url);
            Assert.True(c.Packages.Length + c.Resources.Length > 0, c.Name + " brings neither a package nor a resource");
        }
    }

    /// <summary>
    /// The ImageSharp licence is conditional: the Apache 2.0 terms apply to open source
    /// software, and the tool is GPLv3. The entry must say under which terms it is taken,
    /// so that the next reader does not assume it is a plain permissive licence.
    /// </summary>
    [Fact]
    public void R122_ImageSharpIsTakenUnderTheOpenSourceTermsAndCarriesItsCopyright()
    {
        var sharp = Assert.Single(ThirdParty.Components, c => c.Name == "SixLabors.ImageSharp");

        Assert.Contains("Apache 2.0", sharp.Licence);
        Assert.Contains("Six Labors", sharp.Copyright);
    }

    /// <summary>
    /// --help prints the warranty notice from RunReport, line by line. This test says
    /// directly what broke if someone undoes the loop.
    /// </summary>
    [Fact]
    public void R004_HelpPrintsTheSharedWarrantyLines()
    {
        var sw = new StringWriter();
        AnalyzerCli.Run(new[] { "--help", "--lang", "ro" }, sw, sw);
        string[] lines = sw.ToString().Replace("\r\n", "\n").Split('\n');

        foreach (string l in RunReport.WarrantyLines)
            Assert.Contains(l, lines);

        Assert.Contains(RunReport.LicenseUrl, sw.ToString());
    }
}
