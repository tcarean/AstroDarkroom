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
// The list in ThirdParty.Components is what the "Despre" window shows, and LGPL-2.1
// section 6 asks that the LibRaw notice appear where the program shows its own
// copyright. A package added to the csproj without an entry here would be a package
// shipped and not shown; a package removed from the csproj with its entry left would
// show a license we no longer use. The tests tie the two together both ways, and each
// entry to the notices file.

using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace FlatCheck.Tests;

public class ThirdPartyTests
{
    static string RepoDir([CallerFilePath] string thisFile = "")
        => Path.GetDirectoryName(Path.GetDirectoryName(thisFile))!;

    /// <summary>The NuGet packages the core links, read from its csproj.</summary>
    static string[] CorePackages()
    {
        string csproj = File.ReadAllText(Path.Combine(RepoDir(), "FlatCheck.Core", "FlatCheck.Core.csproj"));
        return Regex.Matches(csproj, @"<PackageReference\s+Include=""([^""]+)""")
            .Select(m => m.Groups[1].Value)
            .ToArray();
    }

    /// <summary>
    /// Every package in the csproj is claimed by exactly one component, and every claimed
    /// package exists in the csproj. This is the test that fails at the next dependency
    /// added without an entry - before it reaches the screen without a notice.
    /// </summary>
    [Fact]
    public void R095_EveryPackageTheCoreLinksIsListed()
    {
        string[] linked = CorePackages();
        Assert.NotEmpty(linked);

        string[] claimed = ThirdParty.Components.SelectMany(c => c.Packages).ToArray();

        foreach (string p in linked)
            Assert.True(claimed.Count(c => c == p) == 1,
                $"pachetul {p} din FlatCheck.Core.csproj nu e revendicat de exact o componenta din ThirdParty.Components");

        foreach (string p in claimed)
            Assert.True(linked.Contains(p),
                $"ThirdParty.Components revendica pachetul {p}, care nu mai e in FlatCheck.Core.csproj");
    }

    /// <summary>
    /// The GPLv3 compatibility reasons live in THIRD-PARTY-NOTICES.md, so every component
    /// shown must have a heading there. The name is looked for in the headings, not
    /// anywhere in the text - "LibRaw" also appears in the paragraph about Sdcb.LibRaw.
    /// </summary>
    [Fact]
    public void R095_EveryComponentHasAnEntryInTheNoticesFile()
    {
        string notices = File.ReadAllText(Path.Combine(RepoDir(), "THIRD-PARTY-NOTICES.md"));
        string[] headings = Regex.Matches(notices, @"^##\s+(.+)$", RegexOptions.Multiline)
            .Select(m => m.Groups[1].Value)
            .ToArray();

        foreach (var c in ThirdParty.Components)
            Assert.True(headings.Any(h => h.Contains(c.Name)),
                $"THIRD-PARTY-NOTICES.md nu are un heading pentru {c.Name}");
    }

    /// <summary>What the screen shows cannot be empty: name, role, license, copyright, and
    /// an https link.</summary>
    [Fact]
    public void EveryComponentNamesItsLicenceAndUrl()
    {
        Assert.Equal(4, ThirdParty.Components.Count);

        foreach (var c in ThirdParty.Components)
        {
            Assert.False(string.IsNullOrWhiteSpace(c.Name));
            Assert.False(string.IsNullOrWhiteSpace(c.Role));
            Assert.False(string.IsNullOrWhiteSpace(c.Licence));
            Assert.False(string.IsNullOrWhiteSpace(c.Copyright));
            Assert.StartsWith("https://", c.Url);
            // A code component comes through a package; a data one (lensfun) comes as files and
            // claims no package - but it is the only one allowed not to claim one.
            if (!c.IsData) Assert.NotEmpty(c.Packages);
        }
        Assert.Single(ThirdParty.Components, c => c.IsData);
    }

    /// <summary>
    /// CC BY-SA asks for attribution: the lensfun database ships as files beside the exe,
    /// so its notice must appear in the list the program shows, with the license and the
    /// source, and the folder in the repo must carry the license and the copy's version.
    /// </summary>
    [Fact]
    public void TheLensfunDatabaseIsAttributedAndItsFolderCarriesLicenceAndVersion()
    {
        var lf = Assert.Single(ThirdParty.Components, c => c.Name.StartsWith("lensfun"));
        Assert.True(lf.IsData);
        Assert.Contains("CC BY-SA", lf.Licence);
        Assert.Contains("github.com/lensfun", lf.Url);

        string folder = Path.Combine(RepoDir(), ProfileLocator.LensfunFolderName);
        Assert.True(File.Exists(Path.Combine(folder, "LICENSE.txt")), "lensfun/LICENSE.txt lipseste");
        string version = File.ReadAllText(Path.Combine(folder, ProfileLocator.LensfunVersionFile));
        Assert.Contains("upstream commit:", version);
        Assert.Contains("CC BY-SA", version + File.ReadAllText(Path.Combine(folder, "LICENSE.txt")));
        Assert.NotEmpty(Directory.EnumerateFiles(folder, "*.xml"));
    }

    /// <summary>
    /// LGPL-2.1 section 6 is the reason the list is shown: the LibRaw notice must appear
    /// among the program's copyright notices, and it must be known under which of the two
    /// licenses it was taken.
    /// </summary>
    [Fact]
    public void LibRawIsTakenUnderTheLgplAndCarriesItsCopyright()
    {
        var libraw = Assert.Single(ThirdParty.Components, c => c.Name == "LibRaw");

        Assert.Contains("LGPL-2.1", libraw.Licence);
        Assert.Contains("LibRaw LLC", libraw.Copyright);
    }

    /// <summary>
    /// --help prints the warranty notice from RunReport, line by line. Golden/help.txt
    /// keeps the text; this test says directly what broke if someone undoes the loop.
    /// </summary>
    [Fact]
    public void HelpPrintsTheSharedWarrantyLines()
    {
        var sw = new StringWriter();
        FlatCheckCli.Run(new[] { "--help" }, sw, sw);
        string[] lines = sw.ToString().Replace("\r\n", "\n").Split('\n');

        foreach (string l in RunReport.WarrantyLines)
            Assert.Contains(l, lines);

        Assert.Contains(RunReport.LicenseUrl, sw.ToString());
    }
}
