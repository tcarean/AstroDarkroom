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

using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using AstroDarkroom.Core;

namespace AstroDarkroom.Tests;

/// <summary>
/// The list of third-party components cannot lag behind the project files (R-015, ADR 0013): a
/// package linked without an entry would ship without its notice in "Despre". The tests bind the
/// list to the packages of every project under src/ in both directions, and each entry to a
/// section of THIRD-PARTY-NOTICES.md that carries its copyright line. LightsCheck's and
/// FlatCheck's tests of their lists are the model. The file is also the suite's: the launcher
/// publishes it as THIRD-PARTY-NOTICES.txt, and each of its sections holds a licence's text
/// (R-017, ADR 0014); which packages it must cover is SuiteTests', which reads the siblings.
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

    /// <summary>
    /// The lead points at the file the suite folder carries, under the name the launcher's publish
    /// gives it (checked below), not at the repository's Markdown, which no user has.
    /// </summary>
    [Fact]
    public void R015_TheLeadNamesTheNoticesFileBesideTheProgram_InBothLanguages()
    {
        Assert.Equal("THIRD-PARTY-NOTICES.txt", ThirdParty.NoticesFile);
        string romanian = ThirdParty.Lead;   // the tests run in Romanian
        using IDisposable scope = Ui.Use(UiLanguage.English);
        Assert.Equal("Each licence's whole text is in THIRD-PARTY-NOTICES.txt, beside the program.", ThirdParty.Lead);
        Assert.Equal("Textul intreg al fiecarei licente e in THIRD-PARTY-NOTICES.txt, langa program.", romanian);
    }

    /// <summary>
    /// Phrases of each licence text a section must hold, its conditions and its disclaimer, as the
    /// file's words (Notices.Words: quote markers and line breaks taken out). A text cut short, or
    /// a section reduced to its copyright line, loses one of them.
    /// </summary>
    public static TheoryData<string, string[]> LicenceTexts => new()
    {
        { "NLog", [.. Bsd, "Copyright (c) 2004-2024 Jaroslaw Kowalski <jaak@jkowalski.net>, Kim Christensen, Julian Verdurmen",
                   "Neither the name of Jaroslaw Kowalski nor the names of its contributors"] },
        { "Sdcb.LibRaw", [.. Mit, "Zhou Jie"] },
        { "LibRaw", [.. Lgpl, .. Bsd, .. Mit, "LibRaw: Raw images processing library",
                     "Copyright (C) 2008-2021 LibRaw LLC (http://www.libraw.org, info@libraw.org)",
                     "Copyright (C) 2010, Jacek Gozdz (cuniek@kft.umcs.lublin.pl)",
                     "Neither the name of the author nor the names of its contributors",
                     "Copyright (c) 2005 Adobe Systems Incorporated",
                     "https://github.com/LibRaw/LibRaw/tree/0.21.1",
                     "https://github.com/LibRaw/LibRaw-cmake/tree/6e26c9e73677dc04f9eb236a97c6a4dc225ba7e8",
                     "https://github.com/microsoft/vcpkg/tree/2e3b958ff00e01b26e82d139bb4765586f349f07/ports/libraw"] },
        { "libjpeg-turbo", [.. Bsd, "This software is based in part on the work of the Independent JPEG Group.",
                            "This software is copyright (C) 1991-2020, Thomas G. Lane, Guido Vollbeding.",
                            "(2) If only executable code is distributed, then the accompanying documentation must state that",
                            "Copyright (C)2009-2022 D. R. Commander. All Rights Reserved.",
                            "Neither the name of the libjpeg-turbo Project nor the names of its contributors"] },
        { "Little CMS", [.. Mit, "Copyright (c) 1998-2020 Marti Maria Saguer"] },
        { "zlib", ["Copyright (C) 1995-2017 Jean-loup Gailly and Mark Adler",
                   "1. The origin of this software must not be misrepresented; you must not claim that you wrote the original software.",
                   "2. Altered source versions must be plainly marked as such, and must not be misrepresented as being the original software.",
                   "3. This notice may not be removed or altered from any source distribution."] },
        { "SixLabors.ImageSharp", [.. Apache, .. Bsd, .. Mit, "Six Labors Split License", "Version 1.0, June 2022",
                                   "You are consuming the Work in for use in software licensed under an Open Source or Source Available license.",
                                   "ImageSharp uses third-party libraries or other resources that may be distributed under licenses different than ImageSharp itself.",
                                   "Copyright © 2000-2018 SharpZipLib Contributors",
                                   "Copyright 2017 The Chromium Authors",
                                   "Neither the name of Google LLC nor the names of its contributors"] },
        { "Wacton.Unicolour", [.. Mit, "Copyright (c) 2022-2026 William Acton"] },
        { "The lensfun database", ["Creative Commons Attribution-Share Alike 3.0", "Copyright (C) the lensfun contributors",
                                   "https://creativecommons.org/licenses/by-sa/3.0/"] },
        { "Lynds' Catalogue of Dark Nebulae", ["Lynds B. T., 1962; machine-readable version NASA/ADC, 1979; distributed by CDS, Strasbourg.",
                                               "VII/7A"] },
        { ".NET 8 and WPF", ["DOTNET-LICENSE.txt", "DOTNET-THIRD-PARTY-NOTICES.txt", "Microsoft.NETCore.App"] },
    };

    [Theory]
    [MemberData(nameof(LicenceTexts))]
    public void R017_EachSectionHoldsItsLicencesText(string heading, string[] phrases)
    {
        Dictionary<string, string> sections = Notices.Sections();
        Assert.True(sections.TryGetValue(heading, out string? section), $"THIRD-PARTY-NOTICES.md has no section ## {heading}");
        string words = Notices.Words(section);
        foreach (string phrase in phrases)
        {
            Assert.True(words.Contains(phrase, StringComparison.Ordinal), $"## {heading} lacks \"{phrase}\"");
        }
    }

    /// <summary>
    /// Every section of the file is one whose licence text the theory above checks, and the
    /// reverse: a section added for a new package, with only its Packages and Programs lines,
    /// would satisfy SuiteTests and no test would ask for its licence (R-017).
    /// </summary>
    [Fact]
    public void R017_EverySectionOfTheFileHasItsLicenceTextChecked()
    {
        IEnumerable<string> checkedHeadings = LicenceTexts.Select(row => (string)row[0]);
        Assert.Equal(checkedHeadings.Order(StringComparer.Ordinal), Notices.Sections().Keys.Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// The LibRaw package ships four native libraries, each a project of its own under its own
    /// licence, so each has its own section, which names the file (R-017).
    /// </summary>
    [Theory]
    [InlineData("raw_r.dll", "LibRaw")]
    [InlineData("jpeg8.dll", "libjpeg-turbo")]
    [InlineData("lcms2.dll", "Little CMS")]
    [InlineData("zlib1.dll", "zlib")]
    public void R017_EachNativeLibraryOfTheLibRawPackage_IsOnTheFilesLineOfItsSection(string file, string heading)
    {
        Dictionary<string, string> sections = Notices.Sections();
        Assert.True(sections.TryGetValue(heading, out string? section), $"THIRD-PARTY-NOTICES.md has no section ## {heading}");
        string files = Notices.Field(section, "Files") ?? "";
        Assert.Contains(file, Notices.Named(files).Select(n => n.Name));
    }

    /// <summary>
    /// The launcher's window publishes the notices file under the name the lead gives, like
    /// LICENSE, and a target of its own adds the runtime pack's two texts to the self-contained
    /// publish under their suite names, with an error when they are missing (R-017; the window
    /// has no tests, ADR 0001). What the publish would copy is the next test's.
    /// </summary>
    [Fact]
    public void R017_TheLaunchersPublishCarriesTheNoticesFileAndTheRuntimesTwoTexts()
    {
        XDocument csproj = XDocument.Load(Path.Combine(TestData.FindRepoRoot(), "src", "AstroDarkroom.Wpf", "AstroDarkroom.Wpf.csproj"));
        XElement notices = Assert.Single(csproj.Descendants("None"),
            e => ((string?)e.Attribute("Include"))?.EndsWith("THIRD-PARTY-NOTICES.md", StringComparison.Ordinal) == true);
        Assert.Equal(ThirdParty.NoticesFile, (string?)notices.Attribute("Link"));
        Assert.Equal("PreserveNewest", (string?)notices.Attribute("CopyToOutputDirectory"));

        XElement target = Assert.Single(csproj.Descendants("Target"), t => t.Descendants("ResolvedFileToPublish").Any());
        Dictionary<string, string> published = target.Descendants("ResolvedFileToPublish")
            .ToDictionary(e => (string?)e.Attribute("RelativePath") ?? "", e => (string?)e.Attribute("Include") ?? "", StringComparer.Ordinal);
        Assert.True(published.TryGetValue("DOTNET-LICENSE.txt", out string? licence), "no DOTNET-LICENSE.txt in the publish");
        Assert.EndsWith("LICENSE.TXT", licence, StringComparison.Ordinal);
        Assert.True(published.TryGetValue("DOTNET-THIRD-PARTY-NOTICES.txt", out string? runtimeNotices), "no DOTNET-THIRD-PARTY-NOTICES.txt in the publish");
        Assert.EndsWith("THIRD-PARTY-NOTICES.TXT", runtimeNotices, StringComparison.Ordinal);
        Assert.NotEmpty(target.Descendants("Error"));
    }

    /// <summary>
    /// What the launcher's self-contained publish would copy, asked of MSBuild without publishing:
    /// the window project's publish list for win-x64, as release.publish builds it, about a second
    /// once the project is built (R-017). It holds the notices file under its suite name and the
    /// runtime pack's two texts under theirs, each copied, the two from files of the
    /// Microsoft.NETCore.App runtime pack that exist; a target that stopped running, or an item
    /// no longer copied, which the project file's test above cannot see, turns it red.
    /// </summary>
    [Fact]
    public async Task R017_ThePublishListHoldsTheNoticesFileAndTheRuntimesTwoTexts()
    {
        ProcessStartInfo start = new(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        {
            WorkingDirectory = TestData.FindRepoRoot(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        string[] arguments =
        [
            "build", Path.Combine("src", "AstroDarkroom.Wpf", "AstroDarkroom.Wpf.csproj"), "-c", "Release", "--no-restore", "-nologo",
            "-t:ComputeResolvedFilesToPublishList", "-p:RuntimeIdentifier=win-x64", "-p:SelfContained=true", "-getItem:ResolvedFileToPublish",
        ];
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(start)!;
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> errors = process.StandardError.ReadToEndAsync();
        using CancellationTokenSource timeout = new(TimeSpan.FromMinutes(3));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        string json = await output;
        Assert.True(process.ExitCode == 0, $"MSBuild exited {process.ExitCode}:\n{json}\n{await errors}");
        using JsonDocument list = JsonDocument.Parse(json);
        Dictionary<string, (string Source, string Copy)> published = new(StringComparer.Ordinal);
        foreach (JsonElement item in list.RootElement.GetProperty("Items").GetProperty("ResolvedFileToPublish").EnumerateArray())
        {
            string relative = item.TryGetProperty("RelativePath", out JsonElement r) ? r.GetString() ?? "" : "";
            string copy = item.TryGetProperty("CopyToPublishDirectory", out JsonElement c) ? c.GetString() ?? "" : "";
            published[relative] = (item.GetProperty("Identity").GetString() ?? "", copy);
        }

        string[] copied = ["PreserveNewest", "Always"];
        Assert.True(published.TryGetValue(ThirdParty.NoticesFile, out (string Source, string Copy) notices), $"no {ThirdParty.NoticesFile} in the publish list");
        Assert.EndsWith("THIRD-PARTY-NOTICES.md", notices.Source, StringComparison.Ordinal);
        Assert.Contains(notices.Copy, copied);
        foreach ((string name, string packFile) in new[] { ("DOTNET-LICENSE.txt", "LICENSE.TXT"), ("DOTNET-THIRD-PARTY-NOTICES.txt", "THIRD-PARTY-NOTICES.TXT") })
        {
            Assert.True(published.TryGetValue(name, out (string Source, string Copy) text), $"no {name} in the publish list");
            Assert.Equal(packFile, Path.GetFileName(text.Source));
            Assert.Contains("microsoft.netcore.app.runtime.win-x64", text.Source, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(text.Source), $"{name} comes from {text.Source}, which does not exist");
            Assert.Contains(text.Copy, copied);
        }
    }

    /// <summary>BSD-3-Clause's two conditions on copies, the start of its disclaimer and its last words.</summary>
    private static readonly string[] Bsd =
    [
        "Redistributions of source code must retain the above copyright notice",
        "Redistributions in binary form must reproduce the above copyright notice",
        "THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS",
        "EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.",
    ];

    /// <summary>MIT's grant, its one condition and the start of its disclaimer.</summary>
    private static readonly string[] Mit =
    [
        "Permission is hereby granted, free of charge, to any person obtaining a copy",
        "The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.",
        "THE SOFTWARE IS PROVIDED \"AS IS\", WITHOUT WARRANTY OF ANY KIND",
    ];

    /// <summary>LGPL-2.1's title, its version line, its terms' heading, its last term and its end.</summary>
    private static readonly string[] Lgpl =
    [
        "GNU LESSER GENERAL PUBLIC LICENSE",
        "Version 2.1, February 1999",
        "TERMS AND CONDITIONS FOR COPYING, DISTRIBUTION AND MODIFICATION",
        "16. IN NO EVENT UNLESS REQUIRED BY APPLICABLE LAW OR AGREED TO IN WRITING",
        "END OF TERMS AND CONDITIONS",
    ];

    /// <summary>Apache-2.0's title, its version line, its terms' heading, its last term and its end.</summary>
    private static readonly string[] Apache =
    [
        "Apache License",
        "Version 2.0, January 2004",
        "TERMS AND CONDITIONS FOR USE, REPRODUCTION, AND DISTRIBUTION",
        "9. Accepting Warranty or Additional Liability.",
        "END OF TERMS AND CONDITIONS",
    ];

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
