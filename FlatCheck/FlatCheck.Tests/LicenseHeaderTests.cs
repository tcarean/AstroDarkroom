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

namespace FlatCheck.Tests;

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
    static string RepoDir([CallerFilePath] string thisFile = "")
        => Path.GetDirectoryName(Path.GetDirectoryName(thisFile))!;

    /// <summary>Notita, fara prefixul de comentariu - acela difera de la C# la Python.</summary>
    static readonly string[] Notice =
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

    /// <summary>
    /// Proiectele scanate. Lista e explicita, nu descoperita din solutie, tocmai ca
    /// adaugarea unui proiect nou sa fie o decizie: un proiect uitat aici ar face ca
    /// fisierele lui sa scape de notita fara ca nimic sa se planga.
    /// </summary>
    static readonly string[] Projects =
    { "FlatCheck", "FlatCheck.Core", "FlatCheck.Tests", "FlatCheck.Wpf", "FlatCheck.Wpf.Tests" };

    /// <summary>
    /// Extensiile de sursa, cu forma de comentariu potrivita fiecareia. XAML-ul e sursa
    /// la fel ca C#-ul - descrie interfata, e scris de mana, nu e generat - deci poarta
    /// si el notita.
    /// </summary>
    static readonly (string Ext, string Open, string? Close)[] Kinds =
    {
        (".cs",   "//",   null),
        (".xaml", "<!--", "-->"),
        (".py",   "#",    null),
    };

    static IEnumerable<string> SourceFiles()
    {
        string root = RepoDir();

        foreach (string project in Projects)
        {
            string dir = Path.Combine(root, project);
            if (!Directory.Exists(dir)) continue;

            foreach (string f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                if (!Kinds.Any(k => k.Ext == Path.GetExtension(f))) continue;

                // bin/ si obj/ contin fisiere generate de MSBuild (AssemblyInfo, XAML-ul
                // intermediar) - nu sunt sursa noastra si nu au de ce sa poarte notita.
                string rel = Path.GetRelativePath(root, f).Replace('\\', '/');
                if (rel.Contains("/bin/") || rel.Contains("/obj/")) continue;
                yield return f;
            }
        }

        foreach (string f in Directory.EnumerateFiles(root, "*.py", SearchOption.TopDirectoryOnly))
            yield return f;

        // Generatorul icoanei nu apartine niciunui proiect, dar e sursa scrisa de mana.
        string icon = Path.Combine(root, "Icon");
        if (Directory.Exists(icon))
            foreach (string f in Directory.EnumerateFiles(icon, "*.py", SearchOption.TopDirectoryOnly))
                yield return f;

        // La fel scriptul de actualizare a bazei lensfun; XML-urile de langa el sunt datele
        // altcuiva (CC BY-SA) si nu poarta notita noastra.
        string lensfun = Path.Combine(root, "lensfun");
        if (Directory.Exists(lensfun))
            foreach (string f in Directory.EnumerateFiles(lensfun, "*.py", SearchOption.TopDirectoryOnly))
                yield return f;
    }

    /// <summary>
    /// Randeaza notita in forma pe care o cere limbajul. C# si Python prefixeaza fiecare
    /// linie; XAML nu poate - "&lt;!--" nu se repeta pe fiecare rand fara sa inchida
    /// comentariul - deci acolo notita sta intre o linie de deschidere si una de
    /// inchidere, cu textul neatins intre ele.
    /// </summary>
    static string Expected((string Ext, string Open, string? Close) kind)
        => kind.Close == null
            ? string.Join("\n", Notice.Select(l => (kind.Open + " " + l).TrimEnd()))
            : kind.Open + "\n" + string.Join("\n", Notice) + "\n" + kind.Close;

    static (string Ext, string Open, string? Close) KindOf(string file)
        => Kinds.First(k => k.Ext == Path.GetExtension(file));

    static string HeaderOf(string file)
    {
        var kind = KindOf(file);
        string text = File.ReadAllText(file).Replace("\r\n", "\n");
        var lines = text.Split('\n').ToList();

        // Shebang-ul din .py trebuie sa ramana pe prima linie, deci notita vine sub el.
        if (lines.Count > 0 && lines[0].StartsWith("#!")) lines.RemoveAt(0);

        // La fel declaratia XML, daca exista: sta obligatoriu inaintea comentariului.
        if (lines.Count > 0 && lines[0].StartsWith("<?xml")) lines.RemoveAt(0);

        return string.Join("\n", lines.Take(Notice.Length + (kind.Close == null ? 0 : 2)));
    }

    [Fact]
    public void R096_EverySourceFileStartsWithTheLicenseNotice()
    {
        var offenders = SourceFiles()
            .Where(f => HeaderOf(f) != Expected(KindOf(f)))
            .Select(f => Path.GetRelativePath(RepoDir(), f))
            .ToList();

        Assert.True(offenders.Count == 0,
            "Fisiere fara notita de licenta la inceput:\n  " + string.Join("\n  ", offenders));
    }

    /// <summary>Daca enumerarea da gres, testul de mai sus ar trece pe zero fisiere.</summary>
    [Fact]
    public void TheScanSeesEveryProjectAndBothPythonFiles()
    {
        var names = SourceFiles().Select(Path.GetFileName).ToList();

        Assert.Contains("Version.cs", names);              // FlatCheck.Core
        Assert.Contains("FlatCheckCli.cs", names);         // FlatCheck
        Assert.Contains("LicenseHeaderTests.cs", names);   // FlatCheck.Tests
        Assert.Contains("flatcheck.py", names);
        Assert.Contains("crosscheck.py", names);
        Assert.True(names.Count > 30, "prea putine fisiere scanate: " + names.Count);
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
