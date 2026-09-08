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
using System.Runtime.CompilerServices;

namespace ImageVisualCompare.Tests;

/// <summary>
/// GPLv3 cere ca fiecare copie sa poarte notita, deci fiecare fisier sursa incepe cu ea:
/// linia SPDX (varianta citibila de scanere de licente), copyright-ul, si cele trei
/// paragrafe standard. Testul asta e singurul lucru care tine conventia in viata cand se
/// adauga un fisier nou - altfel headerul se pierde tacut in primul fisier scris in graba,
/// si abia la un audit de licenta afli.
///
/// Se verifica si LICENSE: sa existe, si sa fie chiar textul GPL v3, nu o parafrazare.
/// </summary>
public class LicenseHeaderTests
{
    private static string RepoDir([CallerFilePath] string thisFile = "") =>
        Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(thisFile)))!;

    /// <summary>Notita, fara prefixul de comentariu.</summary>
    private static readonly string[] Notice =
    [
        "SPDX-License-Identifier: GPL-3.0-or-later",
        "",
        "Copyright (C) 2026 Tudor Carean",
        "",
        "This program is free software: you can redistribute it and/or modify",
        "it under the terms of the GNU General Public License as published by",
        "the Free Software Foundation, either version 3 of the License, or",
        "(at your option) any later version.",
        "",
        "This program is distributed in the hope that it will be useful,",
        "but WITHOUT ANY WARRANTY; without even the implied warranty of",
        "MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the",
        "GNU General Public License for more details.",
        "",
        "You should have received a copy of the GNU General Public License",
        "along with this program.  If not, see <https://www.gnu.org/licenses/>.",
    ];

    private static readonly string[] Projects =
    [
        Path.Combine("src", "ImageVisualCompare.Core"),
        Path.Combine("src", "ImageVisualCompare.Wpf"),
        Path.Combine("tests", "ImageVisualCompare.Tests"),
    ];

    private static IEnumerable<string> SourceFiles()
    {
        string root = RepoDir();

        foreach (string project in Projects)
        {
            foreach (string file in Directory.EnumerateFiles(
                         Path.Combine(root, project), "*.cs", SearchOption.AllDirectories))
            {
                // bin/ si obj/ contin fisiere generate de MSBuild (AssemblyInfo, .g.cs din
                // XAML) - nu sunt sursa noastra si nu au de ce sa poarte notita.
                string relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                if (relative.Contains("/bin/") || relative.Contains("/obj/"))
                {
                    continue;
                }

                yield return file;
            }
        }
    }

    private static string Expected() =>
        string.Join("\n", Notice.Select(line => ("// " + line).TrimEnd()));

    private static string HeaderOf(string file)
    {
        string text = File.ReadAllText(file).Replace("\r\n", "\n");
        return string.Join("\n", text.Split('\n').Take(Notice.Length));
    }

    [Fact]
    public void EverySourceFileStartsWithTheLicenseNotice()
    {
        List<string> offenders = SourceFiles()
            .Where(f => HeaderOf(f) != Expected())
            .Select(f => Path.GetRelativePath(RepoDir(), f))
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "Fisiere fara notita de licenta la inceput:\n  " + string.Join("\n  ", offenders));
    }

    /// <summary>Daca enumerarea da gres, testul de mai sus ar trece pe zero fisiere.</summary>
    [Fact]
    public void TheScanSeesAllThreeProjects()
    {
        List<string> names = SourceFiles().Select(Path.GetFileName).ToList()!;

        Assert.Contains("Viewport.cs", names);
        Assert.Contains("Version.cs", names);
        Assert.Contains("CompareView.xaml.cs", names);
        Assert.Contains("LicenseHeaderTests.cs", names);
        Assert.True(names.Count > 25, "prea putine fisiere scanate: " + names.Count);
    }

    [Fact]
    public void TheRepoShipsTheFullGplText()
    {
        string license = File.ReadAllText(Path.Combine(RepoDir(), "LICENSE"));

        Assert.Contains("GNU GENERAL PUBLIC LICENSE", license);
        Assert.Contains("Version 3, 29 June 2007", license);
        Assert.Contains("TERMS AND CONDITIONS", license);
        Assert.Contains("How to Apply These Terms to Your New Programs", license);
    }
}
