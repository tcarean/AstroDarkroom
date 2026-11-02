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

namespace LightsCheck.Tests;

/// <summary>
/// The console in English (R-062). The five golden scenarios run again with <c>--lang en</c>,
/// pinned as <c>Golden/en_*.txt</c>, the place where the English wording is read and reviewed,
/// and each is checked against the same run with <c>--lang ro</c>: the same exit code and the
/// same numbers, the same number of times, so a translation cannot move a figure. The Romanian
/// goldens stay the reference for the figures. Named so that the golden filter of CLAUDE.md
/// (`--filter GoldenOutputTests`, a substring) regenerates these goldens too, and in the same
/// collection as <see cref="GoldenOutputTests"/>, since both write frame.txt and help.txt when
/// the goldens are regenerated.
/// </summary>
[Collection("golden")]
public class EnglishGoldenOutputTests
{
    /// <summary>The scenarios, by the name of their Romanian golden.</summary>
    public static TheoryData<string> Scenarios => ["inventory", "frame", "series", "series-classes", "help"];

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void R062_EveryScenarioPrintsTheSameNumbersAndExitCodeInEnglish(string name)
    {
        (int enCode, string en, string enErr) = Run(name, "en");
        (int roCode, string ro, _) = Run(name, "ro");

        GoldenOutputTests.Compare("en_" + name + ".txt", en + "--- stderr ---\n" + enErr + "--- exit " + enCode + " ---\n");
        Assert.Equal(roCode, enCode);
        Assert.Equal(Numbers(ro), Numbers(en));
        AssertNoRomanian(en + enErr);
    }

    /// <summary>
    /// <c>--lang ro</c> on the command line prints what the Romanian golden pins: the option
    /// changes nothing against the test assembly's default (R-060).
    /// </summary>
    [Theory]
    [InlineData("frame")]
    [InlineData("help")]
    public void R060_LangRoPrintsTheRomanianGolden(string name)
    {
        (int code, string stdout, string stderr) = Run(name, "ro");
        GoldenOutputTests.Compare(name + ".txt", stdout + "--- stderr ---\n" + stderr + "--- exit " + code + " ---\n");
    }

    /// <summary>
    /// Runs a scenario in one language, built as <see cref="GoldenOutputTests"/> builds the golden
    /// of the same name, the output scrubbed the same way.
    /// </summary>
    private static (int Code, string Stdout, string Stderr) Run(string name, string lang)
    {
        SessionFixture? s = name switch
        {
            "inventory" => new SessionFixture().WithLights(5, biases: 3),
            "frame" => new SessionFixture().WithLights(1, withStars: true),
            "series" => new SessionFixture().WithStarSeries(SessionFixture.Pattern, biases: 2),
            "series-classes" => new SessionFixture().WithStarSeries(SessionFixture.ClassesPattern, biases: 2, darks: 2),
            "help" => null,
            _ => throw new ArgumentException("no such scenario: " + name),
        };
        using (s)
        {
            string[] args = name switch
            {
                "inventory" => [s!.Root, "--inventory"],
                "frame" => [s!.Root, "--frame", "1"],
                "series" or "series-classes" => [s!.Root, "--threads", "2"],
                _ => ["--help"],
            };
            if (name == "inventory")
            {
                File.WriteAllBytes(Path.Combine(s!.LightsDir, "DSC_0006.fit"), new byte[100]);
            }
            else if (name == "series-classes")
            {
                File.WriteAllBytes(Path.Combine(s!.LightsDir, "DSC_0009.fit"), new byte[100]);
            }

            (int code, string stdout, string stderr) = Cli.Run(["--lang", lang, .. args]);
            if (name.StartsWith("series", StringComparison.Ordinal))
            {
                // The progress lines arrive in the order the threads finish: not golden material.
                Assert.Matches(lang == "en" ? @"frame (8|10) / \1" : @"cadrul (8|10) / \1", stderr);
                stderr = lang == "en" ? "(progress, left out)\n" : "(progresul, omis)\n";
            }

            return (code, GoldenOutputTests.Scrub(stdout, s?.Root ?? ""), GoldenOutputTests.Scrub(stderr, s?.Root ?? ""));
        }
    }

    private static readonly Regex Number = new(@"\d+(?:\.\d+)?", RegexOptions.Compiled);

    /// <summary>Every number of a text, sorted, so two texts compare as multisets.</summary>
    private static List<string> Numbers(string text)
        => Number.Matches(text).Select(m => m.Value).OrderBy(v => v, StringComparer.Ordinal).ToList();

    // Words that are Romanian and never English, with the spaces or the stems that make them
    // words; the scrubbed placeholders (<sesiune>, <versiune>) are the test's, not the output's.
    private static readonly string[] RomanianWords =
    [
        " si ", " sau ", " nu ", " din ", " cu ", " pe ", " de ", " la ", " fara ", " mai ", " sub ",
        "cadru", "cadre", "fisier", "eroare", "EROARE", "ATENTIE", "alungire", "dara", "dare ", "roua",
        "miscar", "stele", "fond", "zgomot", "necunoscut", "niciun", "SESIUNE", "REZUMAT", "RESPINSE",
        "nedeterminat", "NEDETERMINAT", "pauz", "expunere", "obiectiv", "aparat", "grade", "Folosire",
        "implicit", "pozitii", "acoperire", "intindere", "aliniere", "perechi", "scara", "verde",
        "BUN", "LA LIMITA", "SLAB", "necitit", "inregistr", "Acest program",
        "aliniat", "singure", "locuri", "pozitiilor", "tarie", "apropier", "obicei", "niciodata", "alunec", "dunga", "dungi",
        "latimea", "montura", "fotografiere", "totala", "nivelul", "inseamna", "rotit", "deviata", "gasite", "lumina", "ramane",
        "alinia", "comune", "obisnuit", "semnaleaza", "vecine", "putine", "stivuire", "satelit", "referinta", "adica", "ceata", "scoate",
        "in stiva",
    ];

    /// <summary>The English text holds none of the Romanian words.</summary>
    private static void AssertNoRomanian(string english)
    {
        string text = english.Replace("<sesiune>", "", StringComparison.Ordinal).Replace("<versiune>", "", StringComparison.Ordinal);
        foreach (string word in RomanianWords)
        {
            int at = text.IndexOf(word, StringComparison.Ordinal);
            Assert.True(at < 0, $"Romanian '{word.Trim()}' in the English output: ...{text[Math.Max(0, at - 60)..Math.Min(text.Length, at + 60)]}...");
        }
    }
}
