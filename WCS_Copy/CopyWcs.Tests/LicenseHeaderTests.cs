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

using System.Runtime.CompilerServices;

namespace CopyWcs.Tests;

/// <summary>
/// GPLv3 cere ca fiecare copie sa poarte notita, deci fiecare fisier sursa incepe cu ea:
/// linia SPDX (varianta citibila de scanere de licente), copyright-ul, si cele trei
/// paragrafe standard. Testul asta e singurul lucru care tine conventia in viata cand
/// se adauga un fisier nou - altfel headerul se pierde tacut in primul fisier scris in
/// graba, si abia la un audit de licenta afli.
///
/// Se verifica si LICENSE: sa existe, si sa fie chiar textul GPL v3, nu o parafrazare.
/// </summary>
public class LicenseHeaderTests
{
    private static string RepoDir([CallerFilePath] string thisFile = "")
        => Path.GetDirectoryName(Path.GetDirectoryName(thisFile))!;

    /// <summary>Notita, fara prefixul de comentariu - acela difera de la C# la Python.</summary>
    private static readonly string[] Notice =
    {
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
    };

    private static IEnumerable<string> SourceFiles()
    {
        var root = RepoDir();

        foreach (var project in new[] { "CopyWcs", "CopyWcs.Tests" })
            foreach (var f in Directory.EnumerateFiles(
                         Path.Combine(root, project), "*.cs", SearchOption.AllDirectories))
            {
                // bin/ si obj/ contin fisiere generate de MSBuild (AssemblyInfo etc.) -
                // nu sunt sursa noastra si nu au de ce sa poarte notita.
                var rel = Path.GetRelativePath(root, f).Replace('\\', '/');
                if (rel.Contains("/bin/") || rel.Contains("/obj/")) continue;
                yield return f;
            }

        foreach (var f in Directory.EnumerateFiles(root, "*.py", SearchOption.TopDirectoryOnly))
            yield return f;
    }

    /// <summary>Randeaza notita cu prefixul de comentariu potrivit limbajului.</summary>
    private static string Expected(string prefix)
        => string.Join("\n", Notice.Select(l => (prefix + " " + l).TrimEnd()));

    private static string HeaderOf(string file)
    {
        var text = File.ReadAllText(file).Replace("\r\n", "\n");
        var lines = text.Split('\n').ToList();

        // Shebang-ul din .py trebuie sa ramana pe prima linie, deci notita vine sub el.
        if (lines.Count > 0 && lines[0].StartsWith("#!")) lines.RemoveAt(0);

        return string.Join("\n", lines.Take(Notice.Length));
    }

    [Fact]
    public void EverySourceFileStartsWithTheLicenseNotice()
    {
        var offenders = SourceFiles()
            .Where(f => HeaderOf(f) != Expected(Path.GetExtension(f) == ".py" ? "#" : "//"))
            .Select(f => Path.GetRelativePath(RepoDir(), f))
            .ToList();

        Assert.True(offenders.Count == 0,
            "Fisiere fara notita de licenta la inceput:\n  " + string.Join("\n  ", offenders));
    }

    /// <summary>Daca enumerarea da gres, testul de mai sus ar trece pe zero fisiere.</summary>
    [Fact]
    public void TheScanSeesBothProjectsAndThePythonPrototype()
    {
        var names = SourceFiles().Select(Path.GetFileName).ToList();

        Assert.Contains("CopyWcsCli.cs", names);
        Assert.Contains("LicenseHeaderTests.cs", names);
        Assert.Contains("copy_wcs.py", names);
        Assert.True(names.Count > 8, "prea putine fisiere scanate: " + names.Count);
    }

    [Fact]
    public void TheRepoShipsTheFullGplText()
    {
        var license = File.ReadAllText(Path.Combine(RepoDir(), "LICENSE"));

        Assert.Contains("GNU GENERAL PUBLIC LICENSE", license);
        Assert.Contains("Version 3, 29 June 2007", license);
        Assert.Contains("TERMS AND CONDITIONS", license);
        Assert.Contains("How to Apply These Terms to Your New Programs", license);
    }
}
