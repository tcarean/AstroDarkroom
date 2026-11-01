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
// show a license we no longer use. The tests tie the two together both ways, the native
// DLLs of LibRaw's runtime package to the components that name them, and each entry to
// the notices file.

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
    /// Every package in the csproj is claimed by a component, and every claimed package exists
    /// in the csproj. This is the test that fails at the next dependency added without an entry
    /// - before it reaches the screen without a notice. A package claimed by several components
    /// is one whose native DLLs they divide (LibRaw's win-x64 runtime and the three libraries it
    /// calls, R-095): each of them names its files, so no package is listed twice by mistake.
    /// </summary>
    [Fact]
    public void R095_EveryPackageTheCoreLinksIsListed()
    {
        string[] linked = CorePackages();
        Assert.NotEmpty(linked);

        string[] claimed = ThirdParty.Components.SelectMany(c => c.Packages).ToArray();

        foreach (string p in linked)
            Assert.True(claimed.Contains(p),
                $"the package {p} of FlatCheck.Core.csproj is not claimed by any component of ThirdParty.Components");

        foreach (string p in claimed)
            Assert.True(linked.Contains(p),
                $"ThirdParty.Components claims the package {p}, which is no longer in FlatCheck.Core.csproj");

        foreach (var shared in ThirdParty.Components.SelectMany(c => c.Packages.Select(p => (Package: p, Component: c)))
                                                     .GroupBy(x => x.Package).Where(g => g.Count() > 1))
            foreach (var x in shared)
                Assert.True(x.Component.NativeFiles.Length > 0,
                    $"{x.Component.Name} shares the package {shared.Key} without naming the native files that are its own");
    }

    /// <summary>
    /// What the window lists is what the release ships (R-095): every DLL of the win-x64
    /// runtime in the test output, the files a publish puts beside or inside the exe, is named
    /// by exactly one component, and every file a component names is there. Measured on
    /// 2026-11-01: raw_r.dll (LibRaw), jpeg8.dll (libjpeg-turbo), lcms2.dll (Little CMS) and
    /// zlib1.dll (zlib). A LibRaw build that brings a fifth DLL fails here until it is listed.
    /// </summary>
    [Fact]
    public void R095_EveryNativeDllOfTheRuntimeIsNamedByExactlyOneComponent()
    {
        string native = Path.Combine(AppContext.BaseDirectory, "runtimes", "win-x64", "native");
        string[] shipped = Directory.GetFiles(native, "*.dll")
            .Select(f => Path.GetFileName(f).ToLowerInvariant())
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToArray();
        string[] named = ThirdParty.Components.SelectMany(c => c.NativeFiles)
            .Select(f => f.ToLowerInvariant())
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(new[] { "jpeg8.dll", "lcms2.dll", "raw_r.dll", "zlib1.dll" }, shipped);
        Assert.Equal(shipped, named);
    }

    /// <summary>
    /// The three libraries LibRaw's DLL calls are components of their own, as the Astro
    /// Darkroom folder's notices name them, each with its own licence and copyright (R-095).
    /// libjpeg-turbo's IJG licence asks the documentation of a binary to say the software is
    /// based in part on the IJG's work: the copyright line, which no language changes, ends
    /// with that sentence.
    /// </summary>
    [Fact]
    public void R095_LibRawsThreeLibrariesAreListedWithTheirLicences()
    {
        using (Ui.Use(UiLanguage.English))
        {
            var jpeg = Assert.Single(ThirdParty.Components, c => c.Name == "libjpeg-turbo");
            Assert.Equal(new[] { "jpeg8.dll" }, jpeg.NativeFiles);
            Assert.Equal("IJG, BSD-3-Clause and zlib", jpeg.Licence);
            Assert.EndsWith("This software is based in part on the work of the Independent JPEG Group.", jpeg.Copyright);
            Assert.Contains("Thomas G. Lane", jpeg.Copyright);
            Assert.Contains("D. R. Commander", jpeg.Copyright);

            var lcms = Assert.Single(ThirdParty.Components, c => c.Name == "Little CMS");
            Assert.Equal(new[] { "lcms2.dll" }, lcms.NativeFiles);
            Assert.Equal("MIT", lcms.Licence);
            Assert.Contains("Marti Maria Saguer", lcms.Copyright);

            var zlib = Assert.Single(ThirdParty.Components, c => c.Name == "zlib");
            Assert.Equal(new[] { "zlib1.dll" }, zlib.NativeFiles);
            Assert.Equal("zlib", zlib.Licence);
            Assert.Contains("Jean-loup Gailly and Mark Adler", zlib.Copyright);

            foreach (var c in new[] { jpeg, lcms, zlib })
                Assert.Equal(new[] { "Sdcb.LibRaw.runtime.win64" }, c.Packages);
        }
        using (Ui.Use(UiLanguage.Romanian))
            Assert.EndsWith("Independent JPEG Group.", Assert.Single(ThirdParty.Components, c => c.Name == "libjpeg-turbo").Copyright);
    }

    /// <summary>
    /// ImageSharp's Split License grants the Apache License 2.0 to software under an open-source
    /// licence and asks all documentation to name the granted licence only (R-095): the list
    /// says Apache-2.0, in both languages, and the notices file argues the grant.
    /// </summary>
    [Fact]
    public void R095_ImageSharpIsNamedUnderTheApacheLicenceAlone()
    {
        foreach (UiLanguage language in new[] { UiLanguage.English, UiLanguage.Romanian })
            using (Ui.Use(language))
                Assert.Equal("Apache-2.0", Assert.Single(ThirdParty.Components, c => c.Name == "SixLabors.ImageSharp").Licence);
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

        // The notices file is English (ADR 0069), so the names are taken in English: the test
        // assemblies default to Romanian, where the data component is "lensfun (baza de date)".
        using (Ui.Use(UiLanguage.English))
            foreach (var c in ThirdParty.Components)
                Assert.True(headings.Any(h => h.Contains(c.Name)),
                    $"THIRD-PARTY-NOTICES.md has no heading for {c.Name}");
    }

    /// <summary>What the screen shows cannot be empty: name, role, license, copyright, and
    /// an https link.</summary>
    [Fact]
    public void EveryComponentNamesItsLicenceAndUrl()
    {
        // Five since 2026-10-27, when NLog joined for the diagnostic log (ADR 0070); eight since
        // 2026-11-01, when the three libraries LibRaw's DLL calls joined (ADR 0085).
        Assert.Equal(8, ThirdParty.Components.Count);

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
        Assert.True(File.Exists(Path.Combine(folder, "LICENSE.txt")), "lensfun/LICENSE.txt is missing");
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
        Assert.Equal(new[] { "raw_r.dll" }, libraw.NativeFiles);
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
